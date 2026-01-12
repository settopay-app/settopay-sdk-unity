using System;

namespace Setto.SDK
{
    /// <summary>
    /// 결제 상태
    /// </summary>
    public enum PaymentStatus
    {
        Success,
        Failed,
        Cancelled
    }

    /// <summary>
    /// 결제 결과
    /// </summary>
    [Serializable]
    public class PaymentResult
    {
        /// <summary>
        /// 결제 상태 (JSON 역직렬화용)
        /// </summary>
        public string status;

        /// <summary>
        /// 블록체인 트랜잭션 해시 (성공 시)
        /// </summary>
        public string txId;

        /// <summary>
        /// Setto 결제 ID
        /// </summary>
        public string paymentId;

        /// <summary>
        /// 에러 메시지 (실패 시)
        /// </summary>
        public string error;

        /// <summary>
        /// 결제 상태 (enum)
        /// </summary>
        public PaymentStatus Status => status switch
        {
            "success" => PaymentStatus.Success,
            "cancelled" => PaymentStatus.Cancelled,
            _ => PaymentStatus.Failed
        };
    }

    /// <summary>
    /// 결제 요청 파라미터
    /// </summary>
    public class PaymentParams
    {
        /// <summary>
        /// 주문 ID
        /// </summary>
        public string OrderId { get; set; }

        /// <summary>
        /// 결제 금액
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// 통화 (기본: USD)
        /// </summary>
        public string Currency { get; set; }

        /// <summary>
        /// WebGL 전용: IdP Token
        /// </summary>
        public string IdpToken { get; set; }
    }
}
