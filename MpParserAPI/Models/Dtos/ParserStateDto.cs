namespace MpParserAPI.Models.Dtos
{
    public class ParserStateDto
    {
        public bool Initialized { get; set; }
        public bool HasLoginData { get; set; }
        public bool HasKeywords { get; set; }
        public bool HasGroups { get; set; }
    }
}
