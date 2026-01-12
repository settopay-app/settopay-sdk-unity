using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Networking;

namespace Setto.SDK
{
    /// <summary>
    /// Setto Unity SDK
    ///
    /// 앱/PC: 시스템 브라우저 + Custom URL Scheme
    /// WebGL: iframe + postMessage
    /// </summary>
    public class SettoSDK : MonoBehaviour
    {
        private static SettoSDK _instance;

        private string _merchantId;
        private string _returnScheme;
        private SettoEnvironment _environment;
        private Action<PaymentResult> _onComplete;

        #region WebGL External Functions

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void SettoOpenPayment(string merchantId, string orderId, string amount, string currency, string idpToken, string baseUrl);

        [DllImport("__Internal")]
        private static extern void SettoClosePayment();
#endif

        #endregion

        /// <summary>
        /// 싱글톤 인스턴스
        /// </summary>
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

        /// <summary>
        /// SDK 초기화
        /// </summary>
        /// <param name="merchantId">고객사 ID</param>
        /// <param name="environment">환경 설정</param>
        /// <param name="returnScheme">Custom URL Scheme (WebGL에서는 무시됨)</param>
        public void Initialize(string merchantId, SettoEnvironment environment, string returnScheme = null)
        {
            _merchantId = merchantId;
            _environment = environment;
            _returnScheme = returnScheme ?? "";
        }

        /// <summary>
        /// 결제 창을 열고 결제를 진행합니다.
        /// </summary>
        /// <param name="params">결제 파라미터</param>
        /// <param name="onComplete">결제 완료 콜백</param>
        public void OpenPayment(PaymentParams @params, Action<PaymentResult> onComplete)
        {
            _onComplete = onComplete;

#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL: iframe + postMessage
            SettoOpenPayment(
                _merchantId,
                @params.OrderId,
                @params.Amount.ToString(),
                @params.Currency ?? "",
                @params.IdpToken ?? "",
                _environment.GetBaseUrl()
            );
#else
            // 앱/PC: 시스템 브라우저 + Scheme
            var url = BuildPaymentUrl(@params);
            Application.OpenURL(url);
#endif
        }

        /// <summary>
        /// Deep Link 처리
        /// 앱/PC에서 Deep Link로 결과를 수신할 때 호출합니다.
        /// </summary>
        /// <param name="url">Deep Link URL (예: mygame://setto-result?status=success&txId=xxx)</param>
        public void HandleDeepLink(string url)
        {
            var queryParams = ParseQueryString(url);

            var result = new PaymentResult
            {
                status = queryParams.TryGetValue("status", out var status) ? status : "failed",
                txId = queryParams.TryGetValue("txId", out var txId) ? txId : null,
                paymentId = queryParams.TryGetValue("paymentId", out var paymentId) ? paymentId : null,
                error = queryParams.TryGetValue("error", out var error) ? error : null
            };

            _onComplete?.Invoke(result);
            _onComplete = null;
        }

        /// <summary>
        /// WebGL에서 JavaScript가 호출하는 콜백
        /// (jslib에서 SendMessage로 호출됨)
        /// </summary>
        public void OnPaymentResult(string json)
        {
            var result = JsonUtility.FromJson<PaymentResult>(json);
            _onComplete?.Invoke(result);
            _onComplete = null;
        }

        private string BuildPaymentUrl(PaymentParams @params)
        {
            var baseUrl = _environment.GetBaseUrl();
            var encodedMerchantId = UnityWebRequest.EscapeURL(_merchantId);
            var encodedOrderId = UnityWebRequest.EscapeURL(@params.OrderId);
            var encodedScheme = UnityWebRequest.EscapeURL(_returnScheme);

            var url = $"{baseUrl}/pay?merchantId={encodedMerchantId}&orderId={encodedOrderId}&amount={@params.Amount}&returnScheme={encodedScheme}";

            if (!string.IsNullOrEmpty(@params.Currency))
            {
                var encodedCurrency = UnityWebRequest.EscapeURL(@params.Currency);
                url += $"&currency={encodedCurrency}";
            }

            return url;
        }

        /// <summary>
        /// URL 쿼리 파라미터 파싱
        /// </summary>
        private static Dictionary<string, string> ParseQueryString(string url)
        {
            var result = new Dictionary<string, string>();

            int queryStart = url.IndexOf('?');
            if (queryStart < 0) return result;

            string query = url.Substring(queryStart + 1);
            foreach (var pair in query.Split('&'))
            {
                var parts = pair.Split('=');
                if (parts.Length == 2)
                {
                    string key = UnityWebRequest.UnEscapeURL(parts[0]);
                    string value = UnityWebRequest.UnEscapeURL(parts[1]);
                    result[key] = value;
                }
            }

            return result;
        }
    }
}
