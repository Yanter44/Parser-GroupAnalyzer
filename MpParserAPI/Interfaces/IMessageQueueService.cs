using MpParserAPI.Models;
using TL;

namespace MpParserAPI.Interfaces
{
    public interface IMessageQueueService
    {
        Task<bool> EnqueueMessageAsync(Guid parserId, UpdatesBase update, ParserData parserData);
        Task<UpdateData?> DequeueMessageAsync(Guid parserId);
        int GetQueueSize(Guid parserId);
        bool HasMessages(Guid parserId);
        void ClearQueue(Guid parserId);
    }
}
