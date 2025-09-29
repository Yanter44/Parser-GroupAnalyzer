using MpParserAPI.Common;
using MpParserAPI.Models;
using MpParserAPI.Models.Dtos;
using MpParserAPI.Models.SpaceProxyDto;

namespace MpParserAPI.Interfaces
{
    public interface IParser
    {
        Task<bool> IsParserAuthValid(Guid parserId, string password);
        Task<OperationResult<object>> SetKeywords(Guid parserId, List<string> keywords);
        Task<OperationResult<object>> SetGroupsNames(Guid parserId, IEnumerable<string> groupNames);
        Task <GetParserStateResponceDto> GetParserState(Guid parserId);
        Task<OperationResult<object>> AddNewSpamMessage(Guid parserId, AddNewSpamMessageDto modelDto);
        Task<OperationResult<object>> StartParsing(Guid parserId);
        Task<bool> ReconnectWithNewProxy(Guid parserId, ProxyInfo proxy);
        Task<bool> SetNewProxy(Guid parserId, string proxyAddress);
        Task<ProxyInfo> GetAvailableProxyByProxyAdress(Guid parserId, string proxyAddress);
        Task<ParserData> TryGetParserFromDb(Guid parserId);
        Task StopParsing(Guid parserId);
        Task DisposeParser(Guid parserId);

    }
}
