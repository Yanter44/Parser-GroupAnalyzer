namespace MpParserAPI.Models.Dtos
{
    public class ParserDataResponceDto
    {
        public string ProfileImageUrl { get; set; }
        public string ProfileNickName { get; set; }
        public bool IsParsingStarted { get; set; }
        public string[] Parserkeywords { get; set; }
        public List<string> TargetGroups { get; set; }

        public Guid ParserId { get; set; }          
        public string ParserPassword { get; set; }
    }
}
