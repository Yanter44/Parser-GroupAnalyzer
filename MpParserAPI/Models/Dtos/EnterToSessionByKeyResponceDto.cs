namespace MpParserAPI.Models.Dtos
{
    public class EnterToSessionByKeyResponceDto
    {
        public ParserDataResponceDto parserDataResponceDto { get; set; }
        public List<ParserLogs> parserLogs { get; set; }
    }
}
