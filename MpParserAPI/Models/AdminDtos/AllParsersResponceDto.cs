namespace MpParserAPI.Models.AdminDtos
{
    public class AllParsersResponceDto
    {
        public string TgNickname { get; set; }
        public string ParserId { get; set; }
        public string Password { get; set; }
        public string SubscriptionRate { get; set; }
        public string TotalParsingTime { get; set; }
        public string ProxyAddress { get; set; }
    }
}
