namespace MpParserAPI.Models.Dtos
{
    public class VerificationCodeDto
    {
        public Guid ParserId { get; set; }
        public int TelegramCode { get; set; }
    }
}
