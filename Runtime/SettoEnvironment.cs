namespace Setto.SDK
{
    /// <summary>
    /// Setto SDK 환경 설정
    /// </summary>
    public enum SettoEnvironment
    {
        /// <summary>
        /// 개발 환경
        /// </summary>
        Development,

        /// <summary>
        /// 프로덕션 환경
        /// </summary>
        Production
    }

    public static class SettoEnvironmentExtensions
    {
        /// <summary>
        /// 환경에 해당하는 Base URL
        /// </summary>
        public static string GetBaseUrl(this SettoEnvironment environment)
        {
            return environment switch
            {
                SettoEnvironment.Development => "https://dev-wallet.settopay.com",
                SettoEnvironment.Production => "https://wallet.settopay.com",
                _ => "https://wallet.settopay.com"
            };
        }
    }
}
