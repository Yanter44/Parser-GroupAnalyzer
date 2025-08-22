using System.ComponentModel.DataAnnotations.Schema;
using MpParserAPI.Enums;

namespace MpParserAPI.Models
{
    public class ParserStateTable
    {
        public int Id { get; set; }
        public Guid ParserId { get; set; }
        public string Password { get; set; }
        public string[] Keywords { get; set; }
        public string Phone { get; set; }

        public SubscriptionType SubscriptionType { get; set; }
        public DateTime SubscriptionEndDate { get; set; }
        public TimeSpan? TotalParsingMinutes { get; set; }

        [Column(TypeName = "jsonb")]
        public List<string> SpamWords { get; set; }

        [Column(TypeName = "jsonb")]
        public List<GroupReference> TargetGroups { get; set; }

    }
}
