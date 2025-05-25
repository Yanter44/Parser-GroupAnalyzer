namespace MpParserAPI.Models.Dtos
{
    public class AuthResultDto
    {
        public Guid? TempAuthId { get; set; }
        public Guid? ParserId { get; set; }
        public string? Password { get; set; }
    }
}
