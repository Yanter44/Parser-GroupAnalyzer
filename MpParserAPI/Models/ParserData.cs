using TL;
using WTelegram;

namespace MpParserAPI.Models
{
    public class ParserData
    {
        public Guid Id { get; set; }
        public string Password { get; set; }
        public string[] Keywords { get; set; }
        public string ApiId { get; set; }
        public string ApiHash { get; set; }
        public string Phone { get; set; }
        public Client Client { get; set; } //TELEGRAM CLIENT
        public List<InputPeer> TargetGroups { get; set; }
        public TelegramAuthState AuthState { get; set; }

        public ParserData(Guid _clientId,string phone, string apiId, string apiHash)
        {
            Id = _clientId;
            Phone = phone;
            ApiId = apiId;
            ApiHash = apiHash;
            AuthState = TelegramAuthState.None;
            TargetGroups = new List<InputPeer>();
        }
        public void DisposeData()
        {
            Client?.Dispose();
            Client = null;
            Keywords = null;
            TargetGroups?.Clear();
            TargetGroups = null;
            AuthState = TelegramAuthState.None;
        }
    }
}
