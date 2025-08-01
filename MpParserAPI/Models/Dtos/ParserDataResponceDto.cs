namespace MpParserAPI.Models.Dtos
{
    public class ParserDataResponceDto
    {
        public string ProfileImageUrl { get; set; }
        public string ProfileNickName { get; set; }
        public bool IsParsingStarted { get; set; }
        public string[] Parserkeywords { get; set; }
        public string[] TargetGroups { get; set; }

        public Guid ParserId { get; set; }          
        public string ParserPassword { get; set; }

        public List<string> UserGroupsList { get; set; }
        public string RemainingParsingTimeHoursMinutes { get; set; }
        public string TotalParsingMinutes { get; set; }

    }
}
