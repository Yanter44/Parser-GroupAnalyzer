using Telegram.Bot.Types;

namespace MpBossParserNotification.Interfaces
{
    public interface IStorage
    {
        void SaveSubscription(string parserId, long chatId);
        bool TryGetChatId(string parserId, out long chatId);
        bool RemoveSubscriptionByChatId(long chatId);
        bool IsUserSubscribedToParser(long chatId);
    }
}
