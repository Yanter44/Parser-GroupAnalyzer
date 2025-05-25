namespace MpParserAPI.Models
{
    public class TemporaryAuthData
    {
        public string Phone { get; set; }
        public int? VerificationCode { get; set; }
        public string? TwoFactorPassword { get; set; }
    }
}
