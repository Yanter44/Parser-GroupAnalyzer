using MpParserAPI.Interfaces;
using MpParserAPI.Models;
using System.Collections.Concurrent;

namespace MpParserAPI.Services
{
    public  class ParserDataStorage : IParserDataStorage
    {
        private readonly ConcurrentDictionary<Guid, ParserData> _parsers = new();
        private readonly ConcurrentDictionary<Guid, TemporaryAuthData> _temporaryauthData = new();

        public void AddOrUpdateParser(Guid parserId, ParserData data)
        {
            _parsers[parserId] = data;
        }
       
        public bool TryGetParser(Guid parserId, out ParserData data)
        {
            return _parsers.TryGetValue(parserId, out data);
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
    }
}
