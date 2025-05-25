using MpParserAPI.Interfaces;
using MpParserAPI.Models;
using System.Collections.Concurrent;
using TL;

namespace MpParserAPI.Services
{
    public  class ParserDataStorage : IParserDataStorage
    {
        private readonly ConcurrentDictionary<Guid, ParserData> _parsers = new();
     
        private readonly ConcurrentDictionary<Guid, TemporaryAuthData> _temporaryauthData = new(); //parserId - tempAuthData
        private readonly ConcurrentDictionary<Guid, WTelegram.Client> _tempClients = new(); //tempAuthId - wtelegram.Client

        private readonly Dictionary<Guid, Func<IObject, Task>> _handlers = new();
        public void AddOrUpdateParser(Guid parserId, ParserData data)
        {
            _parsers[parserId] = data;
        }
        public ICollection<ParserData> GetAllParsers()
        {
            return _parsers.Values;
        }
        public bool TryGetParser(Guid parserId, out ParserData data)
        {
            return _parsers.TryGetValue(parserId, out data);
        }
        public ParserData GetParser(Guid parserId)
        {
            _parsers.TryGetValue(parserId, out var parser);
            return parser;
        }

        public void TryRemoveParser(Guid parserId)
        {
            _parsers.TryRemove(parserId, out _);
            _temporaryauthData.TryRemove(parserId, out _);
        }
        public bool ContainsParser(Guid parserId)
        {
            return _parsers.ContainsKey(parserId);
        }
        public void AddOrUpdateTemporaryAuthData(Guid parserId, TemporaryAuthData temporarydata)
        {
            _temporaryauthData[parserId] = temporarydata;
        }
        public bool TryGetTemporaryAuthData(Guid parserId, out TemporaryAuthData tempData)
        {
            return _temporaryauthData.TryGetValue(parserId, out tempData);
        }
        public TemporaryAuthData GetOrCreateTemporaryAuthData(Guid parserId)
        {
            return _temporaryauthData.GetOrAdd(parserId, _ => new TemporaryAuthData());
        }
        public void TryRemoveTemporaryAuthData(Guid parserId)
        {
            _temporaryauthData.TryRemove(parserId, out _);
        }

        public void AddHandler(Guid parserId, Func<IObject, Task> handler)
        {
            _handlers[parserId] = handler; 
        }

        public bool TryGetHandler(Guid parserId, out Func<IObject, Task> handler)
        {

            return _handlers.TryGetValue(parserId, out handler);
        }

        public void RemoveHandler(Guid parserId)
        {
            if (_handlers.TryGetValue(parserId, out var handler))
            {
                Console.WriteLine($"RemoveHandler: {parserId}, Hash: {handler.GetHashCode()}");
                _handlers.Remove(parserId);
            }
        }


    }
}
