using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MpBossParserNotification.Interfaces;

namespace MpBossParserNotification.Services
{
    public class ParserSubscriptionStorage : IStorage
    { 
        private readonly ConcurrentDictionary<string, long> _subscriptions = new();

        public void SaveSubscription(string parserId, long chatId)
        {
            _subscriptions[parserId] = chatId;
        }

        public bool TryGetChatId(string parserId, out long chatId)
        {
            return _subscriptions.TryGetValue(parserId, out chatId);
        }
        public bool RemoveSubscriptionByChatId(long chatId)
        {
            var item = _subscriptions.FirstOrDefault(kv => kv.Value == chatId);
            if (!item.Equals(default(KeyValuePair<string, long>)))
            {
                return _subscriptions.TryRemove(item.Key, out _);
            }
            return false;
        }

        public bool IsUserSubscribedToParser(long chatId)
        {
            return _subscriptions.Values.Contains(chatId);
        }
    }
}
