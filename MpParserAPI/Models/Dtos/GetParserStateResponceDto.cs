namespace MpParserAPI.Models.Dtos
{
    public class GetParserStateResponceDto
    {
        public ParserDataResponceDto parserDataResponceDto { get; set; }
        public List<ParserLogsResponceDto> parserLogs { get; set; }
    }
}
