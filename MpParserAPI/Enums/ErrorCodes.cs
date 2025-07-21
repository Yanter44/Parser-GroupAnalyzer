namespace MpParserAPI.Enums
{
    public static class ErrorCodes
    {
        public const string NeedVerificationCode = "NEED_VERIFICATION_CODE";
        public const string NeedTwoFactorPassword = "NEED_TWO_FACTOR_PASSWORD";
        public const string InvalidVerificationCode = "INVALID_VERIFICATION_CODE";
        public const string InvalidTwoFactorPassword = "INVALID_TWO_FACTOR_PASSWORD";
        public const string SessionExpired = "SESSION_EXPIRED";
        public const string PhoneAlreadyInUse = "PHONE_ALREADY_IN_USE";
    }
}
