using MpParserAPI.Models;

namespace MpParserAPI.Interfaces
{
    public interface IParserDataStorage
    {
        void AddOrUpdateParser(Guid parserId, ParserData data);
        bool TryGetParser(Guid parserId, out ParserData data);
        void RemoveParser(Guid parserId);
    }
}
