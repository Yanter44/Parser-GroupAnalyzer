namespace MpParserAPI.Models
{
    public class ParserLogs
    {
        public int Id { get; set; }

        public Guid ParserId { get; set; }

        // Foreign Key
        public long TelegramUserId { get; set; }
        public TelegramUser TelegramUser { get; set; }
        public string MessageText { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
