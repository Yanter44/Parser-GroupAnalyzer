using MpParserAPI.Models;
using TL;
using WTelegram;

namespace MpParserAPI.Interfaces
{
    public interface IParserDataStorage
    {
        public ICollection<ParserData> GetAllParsers();
        void AddOrUpdateParser(Guid parserId, ParserData data);
        bool TryGetParser(Guid parserId, out ParserData data);
        ParserData GetParser(Guid parserId);
        void TryRemoveParser(Guid parserId);
        bool ContainsParser(Guid parserId);

        void AddOrUpdateTemporaryAuthData(Guid parserId, TemporaryAuthData temporarydata);
        bool TryGetTemporaryAuthData(Guid parserId, out TemporaryAuthData tempData);
        TemporaryAuthData GetOrCreateTemporaryAuthData(Guid parserId);
        void TryRemoveTemporaryAuthData(Guid parserId);

        void AddHandler(Guid parserId, Func<IObject, Task> handler);
        bool TryGetHandler(Guid parserId, out Func<IObject, Task> handler);
        void RemoveHandler(Guid parserId);
        void AddTempClient(Guid tempAuthId, Client client);
        bool TryGetTempClient(Guid tempAuthId, out Client client);
        void RemoveTempClient(Guid tempAuthId);
    }
}
