using MpParserAPI.Interfaces;
using MpParserAPI.Models;
using System.Collections.Concurrent;

namespace MpParserAPI.Services
{
    public  class ParserDataStorage : IParserDataStorage
    {
        private readonly ConcurrentDictionary<Guid, ParserData> _parsers = new();
        private readonly ConcurrentDictionary<Guid, TemporaryAuthData> _authData = new();

        public void AddOrUpdateParser(Guid parserId, ParserData data)
        {
            _parsers[parserId] = data;
        }
       
        public bool TryGetParser(Guid parserId, out ParserData data)
        {
            return _parsers.TryGetValue(parserId, out data);
        }

        public void RemoveParser(Guid parserId)
        {
            _parsers.TryRemove(parserId, out _);
            _authData.TryRemove(parserId, out _);
        }

    }
}
