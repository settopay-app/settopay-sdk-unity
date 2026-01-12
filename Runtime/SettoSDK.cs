using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Networking;

namespace Setto.SDK
{
    // MARK: - Types

    public enum SettoEnvironment
    {
        Dev,
        Prod
    }

    [Serializable]
    public class SettoConfig
    {
        public string merchantId;
        public SettoEnvironment environment;
        public string idpToken; // IdP 토큰 (있으면 자동로그인)
        public bool debug;

        public string BaseURL => environment == SettoEnvironment.Dev
            ? "https://dev-wallet.settopay.com"
            : "https://wallet.settopay.com";

        /// <summary>
        /// URL Scheme (딥링크 콜백용)
        /// 형식: setto-{merchantId}://callback
        /// </summary>
        public string URLScheme => $"setto-{merchantId}";
    }

    public enum PaymentStatus
    {
        Success = 0,
        Failed = 1,
        Cancelled = 2
    }

    [Serializable]
    public class PaymentResult
    {
        public PaymentStatus status;
        public string paymentId;
        public string txHash;
        public string error;
    }

    [Serializable]
    public class PaymentInfo
    {
        public string paymentId;
        public string status;
        public string amount;
        public string currency;
        public string txHash;
        public long createdAt;
        public long completedAt;
    }

    // MARK: - SDK

    public class SettoSDK : MonoBehaviour
    {
        private static SettoSDK _instance;
        public static SettoSDK Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("SettoSDK");
                    _instance = go.AddComponent<SettoSDK>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private SettoConfig _config;
        private Action<PaymentResult> _pendingCallback;

        // MARK: - Platform Native Imports

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void SettoSDK_OpenPayment(string url, string callbackObjectName);

        [DllImport("__Internal")]
        private static extern void SettoSDK_ClosePayment();
#endif

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void SettoSDK_iOS_OpenPayment(string url, string callbackObjectName);

        [DllImport("__Internal")]
        private static extern void SettoSDK_iOS_ClosePayment();

        [DllImport("__Internal")]
        private static extern void SettoSDK_iOS_HandleURL(string url);
#endif

        // MARK: - Public API

        /// <summary>
        /// SDK 초기화
        /// </summary>
        public void Initialize(SettoConfig config)
        {
            _config = config;
            DebugLog($"Initialized - merchantId: {config.merchantId}, environment: {config.environment}");

#if UNITY_ANDROID && !UNITY_EDITOR
            DebugLog("Android: Make sure to register URL Scheme in AndroidManifest.xml");
#elif UNITY_IOS && !UNITY_EDITOR
            DebugLog("iOS: Make sure to register URL Scheme in Info.plist");
#endif
        }

        /// <summary>
        /// 결제 요청
        /// IdP Token 유무에 따라 자동로그인 여부가 결정됩니다.
        /// - IdP Token 없음: Setto 로그인 필요
        /// - IdP Token 있음: PaymentToken 발급 후 자동로그인
        /// </summary>
        public void OpenPayment(string amount, string orderId, Action<PaymentResult> callback)
        {
            if (_config == null)
            {
                callback?.Invoke(new PaymentResult
                {
                    status = PaymentStatus.Failed,
                    error = "SDK not initialized. Call Initialize() first."
                });
                return;
            }

            if (!string.IsNullOrEmpty(_config.idpToken))
            {
                // IdP Token 있음 → PaymentToken 발급 → Fragment로 전달
                DebugLog("Requesting PaymentToken for auto-login...");
                StartCoroutine(RequestPaymentTokenAndOpen(amount, orderId, callback));
            }
            else
            {
                // IdP Token 없음 → Query param으로 직접 전달
                var url = BuildPaymentURL(amount, orderId);
                DebugLog($"Opening payment (Setto login required): {url}");
                OpenPaymentInternal(url, callback);
            }
        }

        /// <summary>
        /// 초기화 여부 확인
        /// </summary>
        public bool IsInitialized => _config != null;

        /// <summary>
        /// 결제 취소 (수동)
        /// </summary>
        public void ClosePayment()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            SettoSDK_ClosePayment();
#elif UNITY_IOS && !UNITY_EDITOR
            SettoSDK_iOS_ClosePayment();
#elif UNITY_ANDROID && !UNITY_EDITOR
            using (var plugin = new AndroidJavaClass("com.setto.sdk.SettoSDK"))
            {
                plugin.CallStatic("closePayment");
            }
#endif
            _pendingCallback?.Invoke(new PaymentResult { status = PaymentStatus.Cancelled });
            _pendingCallback = null;
        }

        /// <summary>
        /// URL Scheme 딥링크 처리 (iOS/Android)
        /// AppDelegate(iOS) 또는 Activity(Android)에서 호출
        /// </summary>
        public void HandleDeepLink(string url)
        {
            DebugLog($"HandleDeepLink: {url}");

#if UNITY_IOS && !UNITY_EDITOR
            SettoSDK_iOS_HandleURL(url);
#elif UNITY_ANDROID && !UNITY_EDITOR
            using (var plugin = new AndroidJavaClass("com.setto.sdk.SettoSDK"))
            {
                plugin.CallStatic("handleURL", url);
            }
#endif
        }

        // MARK: - Internal

        private string BuildPaymentURL(string amount, string orderId)
        {
            var url = $"{_config.BaseURL}/pay/wallet?merchant_id={Uri.EscapeDataString(_config.merchantId)}&amount={Uri.EscapeDataString(amount)}";

            if (!string.IsNullOrEmpty(orderId))
            {
                url += $"&order_id={Uri.EscapeDataString(orderId)}";
            }

            // 모바일: 콜백 URL Scheme 추가
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
            url += $"&callback_scheme={Uri.EscapeDataString(_config.URLScheme)}";
#endif

            return url;
        }

        private IEnumerator RequestPaymentTokenAndOpen(string amount, string orderId, Action<PaymentResult> callback)
        {
            var tokenUrl = $"{_config.BaseURL}/api/external/payment/token";

            var body = new PaymentTokenRequest
            {
                merchantId = _config.merchantId,
                amount = amount,
                orderId = orderId,
                idpToken = _config.idpToken
            };
            var jsonBody = JsonUtility.ToJson(body);

            using (var request = new UnityWebRequest(tokenUrl, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    DebugLog($"PaymentToken request failed: {request.responseCode} - {request.error}");
                    callback?.Invoke(new PaymentResult
                    {
                        status = PaymentStatus.Failed,
                        error = $"Token request failed: {request.responseCode}"
                    });
                    yield break;
                }

                var response = JsonUtility.FromJson<PaymentTokenResponse>(request.downloadHandler.text);

                if (string.IsNullOrEmpty(response.paymentToken))
                {
                    DebugLog("PaymentToken is empty in response");
                    callback?.Invoke(new PaymentResult
                    {
                        status = PaymentStatus.Failed,
                        error = "PaymentToken not received"
                    });
                    yield break;
                }

                // Fragment로 전달 (보안: 서버 로그에 남지 않음)
                var url = $"{_config.BaseURL}/pay/wallet#pt={Uri.EscapeDataString(response.paymentToken)}";

                // 모바일: 콜백 URL Scheme 추가 (Fragment 뒤에)
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
                url += $"&callback_scheme={Uri.EscapeDataString(_config.URLScheme)}";
#endif

                DebugLog("Opening payment with auto-login");
                OpenPaymentInternal(url, callback);
            }
        }

        [Serializable]
        private class PaymentTokenRequest
        {
            public string merchantId;
            public string amount;
            public string orderId;
            public string idpToken;
        }

        [Serializable]
        private class PaymentTokenResponse
        {
            public string paymentToken;
        }

        private void OpenPaymentInternal(string url, Action<PaymentResult> callback)
        {
            _pendingCallback = callback;

#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL: iframe으로 열기
            SettoSDK_OpenPayment(url, gameObject.name);

#elif UNITY_IOS && !UNITY_EDITOR
            // iOS: SFSafariViewController로 열기
            SettoSDK_iOS_OpenPayment(url, gameObject.name);

#elif UNITY_ANDROID && !UNITY_EDITOR
            // Android: Chrome Custom Tabs로 열기
            using (var plugin = new AndroidJavaClass("com.setto.sdk.SettoSDK"))
            {
                plugin.CallStatic("openPayment", url, gameObject.name);
            }

#else
            // Editor / Desktop: 시스템 브라우저로 열기
            DebugLog("Opening in system browser (callback not supported in Editor)");
            Application.OpenURL(url);

            // Editor에서는 콜백을 받을 수 없으므로 바로 취소 처리
            callback?.Invoke(new PaymentResult
            {
                status = PaymentStatus.Cancelled,
                error = "Payment opened in browser. Callback not supported in Editor."
            });
            _pendingCallback = null;
#endif
        }

        /// <summary>
        /// 네이티브 플러그인에서 호출되는 콜백
        /// </summary>
        public void OnPaymentResult(string json)
        {
            try
            {
                DebugLog($"OnPaymentResult: {json}");
                var result = JsonUtility.FromJson<PaymentResult>(json);
                _pendingCallback?.Invoke(result);
            }
            catch (Exception e)
            {
                DebugLog($"Error parsing payment result: {e.Message}");
                _pendingCallback?.Invoke(new PaymentResult
                {
                    status = PaymentStatus.Failed,
                    error = $"Parse error: {e.Message}"
                });
            }
            finally
            {
                _pendingCallback = null;
            }
        }

        private void DebugLog(string message)
        {
            if (_config?.debug == true)
            {
                Debug.Log($"[SettoSDK] {message}");
            }
        }

        // MARK: - Deep Link Handling (Unity 2019.3+)

        private void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus)
            {
                // 앱이 foreground로 돌아올 때
                // Android에서는 onNewIntent가 자동으로 호출되지 않을 수 있음
                DebugLog("App resumed from background");
            }
        }
    }
}
