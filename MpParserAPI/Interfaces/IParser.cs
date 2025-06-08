using MpParserAPI.Common;
using MpParserAPI.Models.Dtos;

namespace MpParserAPI.Interfaces
{
    public interface IParser
    {
        Task<bool> IsParserAuthValid(Guid parserId, string password);
        Task<OperationResult<object>> SetKeywords(Guid parserId, List<string> keywords);
        Task<OperationResult<object>> SetGroupsNames(Guid parserId, IEnumerable<string> groupNames);
        Task<OperationResult<object>> AddTimeParsing(Guid parserId, TimeParsingDto timeParsingDto);
        Task <GetParserStateResponceDto> GetParserState(Guid parserId);
        void StartParsing(Guid parserId);
        void StopParsing(Guid parserId);
        void DisposeParser(Guid parserId);

    }
}
