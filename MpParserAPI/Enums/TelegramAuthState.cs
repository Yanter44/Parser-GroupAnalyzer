public enum TelegramAuthState
{
    None,
    Unauthorized,
    SessionExpired,
    NeedPhoneNumber,
    NeedVerificationCode,
    NeedPassword,
    Authorized
}
