namespace MpParserAPI.Models.AdminDtos
{
    public class SetSubscriptionTypeModelDto
    {
        public string ParserId { get; set; }
        public string SubscriptionType { get; set; }
        public int DaysSubscription { get; set; }
    }
}
