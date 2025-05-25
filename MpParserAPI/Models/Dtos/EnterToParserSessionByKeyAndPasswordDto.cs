namespace MpParserAPI.Models.Dtos
{
    public class EnterToParserSessionByKeyAndPasswordDto
    {
        public Guid ParserId { get; set; }
        public string ParserPassword { get; set; }

    }
}
