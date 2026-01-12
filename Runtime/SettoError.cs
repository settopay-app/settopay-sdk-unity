using System;

namespace Setto.SDK
{
    /// <summary>
    /// Setto SDK 에러 코드
    /// </summary>
    public enum SettoErrorCode
    {
        // 사용자 액션
        UserCancelled,

        // 결제 실패
        PaymentFailed,
        InsufficientBalance,
        TransactionRejected,

        // 네트워크/시스템
        NetworkError,
        SessionExpired,

        // 파라미터
        InvalidParams,
        InvalidMerchant
    }

    /// <summary>
    /// Setto SDK 에러
    /// </summary>
    public class SettoException : Exception
    {
        public SettoErrorCode ErrorCode { get; }

        public SettoException(SettoErrorCode errorCode, string message = null)
            : base(message ?? GetDefaultMessage(errorCode))
        {
            ErrorCode = errorCode;
        }

        /// <summary>
        /// Deep Link error 파라미터로부터 에러 생성
        /// </summary>
        public static SettoException FromErrorCode(string code)
        {
            var errorCode = ParseErrorCode(code);
            return new SettoException(errorCode, GetDefaultMessage(errorCode));
        }

        private static SettoErrorCode ParseErrorCode(string code)
        {
            return code switch
            {
                "USER_CANCELLED" => SettoErrorCode.UserCancelled,
                "PAYMENT_FAILED" => SettoErrorCode.PaymentFailed,
                "INSUFFICIENT_BALANCE" => SettoErrorCode.InsufficientBalance,
                "TRANSACTION_REJECTED" => SettoErrorCode.TransactionRejected,
                "NETWORK_ERROR" => SettoErrorCode.NetworkError,
                "SESSION_EXPIRED" => SettoErrorCode.SessionExpired,
                "INVALID_PARAMS" => SettoErrorCode.InvalidParams,
                "INVALID_MERCHANT" => SettoErrorCode.InvalidMerchant,
                _ => SettoErrorCode.PaymentFailed
            };
        }

        private static string GetDefaultMessage(SettoErrorCode errorCode)
        {
            return errorCode switch
            {
                SettoErrorCode.UserCancelled => "사용자가 결제를 취소했습니다.",
                SettoErrorCode.InsufficientBalance => "잔액이 부족합니다.",
                SettoErrorCode.TransactionRejected => "트랜잭션이 거부되었습니다.",
                SettoErrorCode.NetworkError => "네트워크 오류가 발생했습니다.",
                SettoErrorCode.SessionExpired => "세션이 만료되었습니다.",
                SettoErrorCode.InvalidParams => "잘못된 파라미터입니다.",
                SettoErrorCode.InvalidMerchant => "유효하지 않은 고객사입니다.",
                _ => "결제에 실패했습니다."
            };
        }
    }
}
