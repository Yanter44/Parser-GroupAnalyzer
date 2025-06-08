using TL;
using WTelegram;

namespace MpParserAPI.Models
{
    public class ParserData
    {
        public Guid Id { get; set; }
        public string Password { get; set; }
        public string[] Keywords { get; set; }
        public string Phone { get; set; }
        public bool IsParsingStarted { get; set; }

        public Client Client { get; set; } //TELEGRAM CLIENT
        public List<InputPeer> TargetGroups { get; set; }
        public List<string> TargetGroupTitles { get; set; } = new();

        public TelegramAuthState AuthState { get; set; }
        public Timer ParsingTimer { get; set; }
        public TimeSpan? ParsingDelay { get; set; }

        public ParserData(Guid _parserId, string password,string phone)
        {
            Id = _parserId;
            Password = password;
            Phone = phone;
            AuthState = TelegramAuthState.None;
            TargetGroups = new List<InputPeer>();
        }
        public void DisposeData()
        {
            Client?.Dispose();
            ParsingTimer?.Dispose();
            Client = null;
            Keywords = null;
            TargetGroups?.Clear();
            TargetGroups = null;
            AuthState = TelegramAuthState.None;
        }
    }
}
