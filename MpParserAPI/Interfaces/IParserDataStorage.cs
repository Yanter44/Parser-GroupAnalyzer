using MpParserAPI.Models;

namespace MpParserAPI.Interfaces
{
    public interface IParserDataStorage
    {
        void AddOrUpdateParser(Guid parserId, ParserData data);
        bool TryGetParser(Guid parserId, out ParserData data);
        void TryRemoveParser(Guid parserId);
        bool ContainsParser(Guid parserId);

        void AddOrUpdateTemporaryAuthData(Guid parserId, TemporaryAuthData temporarydata);
        bool TryGetTemporaryAuthData(Guid parserId, out TemporaryAuthData tempData);
        TemporaryAuthData GetOrCreateTemporaryAuthData(Guid parserId);
        void TryRemoveTemporaryAuthData(Guid parserId);
    }
}
