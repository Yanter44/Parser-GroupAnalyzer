using MpParserAPI.Common;
using MpParserAPI.Models.Dtos;

namespace MpParserAPI.Interfaces
{
    public interface IParser
    {
        Task<bool> IsParserAuthValid(Guid parserId, string password);
        Task<OperationResult<object>> SetKeywordsFromText(Guid parserId, string text);
        Task<OperationResult<object>> SetGroupsNamesForParser(Guid parserId, IEnumerable<string> groupNames);
      
        Task <GetParserStateResponceDto> GetParserState(Guid parserId);
        void StartParsing(Guid parserId);
        void StopParsing(Guid parserId);
        void DisposeParser(Guid parserId);

    }
}
