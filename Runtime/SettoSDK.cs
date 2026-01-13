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
        public SettoEnvironment environment;
        public bool debug;

        public string BaseURL => environment == SettoEnvironment.Dev
            ? "https://dev-wallet.settopay.com"
            : "https://wallet.settopay.com";
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
        /// <summary>결제자 지갑 주소 (서버에서 반환)</summary>
        public string fromAddress;
        /// <summary>결산 수신자 주소 (pool이 아닌 최종 수신자, 서버에서 반환)</summary>
        public string toAddress;
        /// <summary>결제 금액 (USD, 예: "10.00", 서버에서 반환)</summary>
        public string amount;
        /// <summary>체인 ID (예: 8453, 56, 900001, 서버에서 반환)</summary>
        public int chainId;
        /// <summary>토큰 심볼 (예: "USDC", "USDT", 서버에서 반환)</summary>
        public string tokenSymbol;
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
            DebugLog($"Initialized - environment: {config.environment}");

#if UNITY_ANDROID && !UNITY_EDITOR
            DebugLog("Android: Make sure to register URL Scheme in AndroidManifest.xml");
#elif UNITY_IOS && !UNITY_EDITOR
            DebugLog("iOS: Make sure to register URL Scheme in Info.plist");
#endif
        }

        /// <summary>
        /// 결제 요청
        /// 항상 PaymentToken을 발급받아서 결제 페이지로 전달합니다.
        /// - IdP Token 없음: Setto 로그인 필요
        /// - IdP Token 있음: 자동로그인
        /// </summary>
        /// <param name="merchantId">머천트 ID</param>
        /// <param name="amount">결제 금액</param>
        /// <param name="idpToken">IdP 토큰 (선택, 있으면 자동로그인)</param>
        /// <param name="callback">결제 결과 콜백</param>
        public void OpenPayment(string merchantId, string amount, string idpToken, Action<PaymentResult> callback)
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

            DebugLog("Requesting PaymentToken...");
            StartCoroutine(RequestPaymentTokenAndOpen(merchantId, amount, idpToken, callback));
        }

        /// <summary>
        /// 결제 요청 (IdP Token 없이)
        /// </summary>
        public void OpenPayment(string merchantId, string amount, Action<PaymentResult> callback)
        {
            OpenPayment(merchantId, amount, null, callback);
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

        private IEnumerator RequestPaymentTokenAndOpen(string merchantId, string amount, string idpToken, Action<PaymentResult> callback)
        {
            var tokenUrl = $"{_config.BaseURL}/api/external/payment/token";

            var body = new PaymentTokenRequest
            {
                merchant_id = merchantId,
                amount = amount,
                idp_token = idpToken
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

                DebugLog($"Response: {request.downloadHandler.text}");
                var response = JsonUtility.FromJson<PaymentTokenResponse>(request.downloadHandler.text);

                if (string.IsNullOrEmpty(response.payment_token))
                {
                    DebugLog($"PaymentToken is empty. Raw response: {request.downloadHandler.text}");
                    callback?.Invoke(new PaymentResult
                    {
                        status = PaymentStatus.Failed,
                        error = "PaymentToken not received"
                    });
                    yield break;
                }

                // Fragment로 전달 (보안: 서버 로그에 남지 않음)
                var url = $"{_config.BaseURL}/pay/wallet#pt={Uri.EscapeDataString(response.payment_token)}";

                // 모바일: 콜백 URL Scheme 추가 (Fragment 뒤에)
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
                url += $"&callback_scheme={Uri.EscapeDataString($"setto-{merchantId}")}";
#endif

                DebugLog("Opening payment page");
                OpenPaymentInternal(url, callback);
            }
        }

        [Serializable]
        private class PaymentTokenRequest
        {
            public string merchant_id;
            public string amount;
            public string idp_token;
        }

        [Serializable]
        private class PaymentTokenResponse
        {
            public string payment_token;
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
