namespace MpParserAPI.Models.Dtos
{
    public class GetParserStateResponceDto
    {
        public ParserDataResponceDto parserDataResponceDto { get; set; }
        public List<ParserLogsResponceDto> parserLogs { get; set; }
        public string ErrorCode { get; set; }
    }
}
