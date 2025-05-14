using System.ComponentModel.DataAnnotations;

namespace MpParserAPI.Models
{
    public class TelegramUser
    {
        [Key]
        public long TelegramUserId { get; set; }

        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Username { get; set; }
        public string Phone { get; set; }

        public long? ProfilePhotoId { get; set; }
        public string ProfileImageUrl { get; set; }

        public ICollection<ParserLogs> ParserLogs { get; set; }
    }
}
