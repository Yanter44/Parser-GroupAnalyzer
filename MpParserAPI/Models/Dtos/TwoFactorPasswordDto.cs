namespace MpParserAPI.Models.Dtos
{
    public class TwoFactorPasswordDto
    {
       public Guid ParserId { get; set; }
       public string TwoFactorPassword { get; set; }
    }
}
