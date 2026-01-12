using UnityEngine;
using UnityEngine.UI;
using Setto.SDK;

namespace Setto.Samples
{
    /// <summary>
    /// Setto SDK 사용 예제
    ///
    /// 이 스크립트를 빈 GameObject에 붙이고, UI 요소들을 연결하세요.
    /// WebGL 빌드에서만 결제가 정상 동작합니다.
    /// </summary>
    public class PaymentExample : MonoBehaviour
    {
        [Header("SDK Configuration")]
        [Tooltip("Setto에서 발급받은 Merchant ID")]
        public string merchantId = "YOUR_MERCHANT_ID";

        [Tooltip("환경 설정 (Dev: 테스트, Prod: 실제 결제)")]
        public SettoEnvironment environment = SettoEnvironment.Dev;

        [Tooltip("IdP Token (선택 - 있으면 자동로그인)")]
        public string idpToken = "";

        [Tooltip("디버그 로그 활성화")]
        public bool debug = true;

        [Header("UI References")]
        public InputField amountInput;
        public InputField orderIdInput;
        public Button payButton;
        public Text statusText;

        private void Start()
        {
            // SDK 초기화
            var config = new SettoConfig
            {
                merchantId = merchantId,
                environment = environment,
                idpToken = string.IsNullOrEmpty(idpToken) ? null : idpToken,
                debug = debug
            };

            SettoSDK.Instance.Initialize(config);

            // 버튼 이벤트 연결
            if (payButton != null)
            {
                payButton.onClick.AddListener(OnPayButtonClicked);
            }

            UpdateStatus("SDK Initialized. Ready to pay.");
        }

        private void OnPayButtonClicked()
        {
            var amount = amountInput != null ? amountInput.text : "10";
            var orderId = orderIdInput != null ? orderIdInput.text : "";

            if (string.IsNullOrEmpty(amount))
            {
                UpdateStatus("Please enter an amount.");
                return;
            }

            UpdateStatus("Opening payment...");
            payButton.interactable = false;

            SettoSDK.Instance.OpenPayment(amount, orderId, OnPaymentResult);
        }

        private void OnPaymentResult(PaymentResult result)
        {
            payButton.interactable = true;

            switch (result.status)
            {
                case PaymentStatus.Success:
                    UpdateStatus($"Payment Success!\nPayment ID: {result.paymentId}\nTx Hash: {result.txHash}");
                    Debug.Log($"[PaymentExample] Success - PaymentId: {result.paymentId}, TxHash: {result.txHash}");
                    break;

                case PaymentStatus.Failed:
                    UpdateStatus($"Payment Failed: {result.error}");
                    Debug.LogError($"[PaymentExample] Failed - Error: {result.error}");
                    break;

                case PaymentStatus.Cancelled:
                    UpdateStatus("Payment Cancelled");
                    Debug.Log("[PaymentExample] Cancelled by user");
                    break;
            }
        }

        private void UpdateStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
            Debug.Log($"[PaymentExample] {message}");
        }
    }
}
