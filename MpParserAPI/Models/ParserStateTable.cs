namespace MpParserAPI.Models
{
    public class ParserStateTable
    {
        public int Id { get; set; }
        public Guid ParserId { get; set; }
        public string Password { get; set; }
        public string[] Keywords { get; set; }
        public string Phone { get; set; }
        public TimeSpan? TotalParsingMinutes { get; set; }
        public List<string> SpamWords { get; set; }
        public List<GroupReference> TargetGroups { get; set; }

    }
}
