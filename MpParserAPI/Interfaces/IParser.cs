using MpParserAPI.Common;
using MpParserAPI.Models.Dtos;

namespace MpParserAPI.Interfaces
{
    public interface IParser
    {
        Task<bool> IsParserAuthValid(Guid parserId, string password);
        Task<OperationResult<ParserAuthResultDto>> LoginAndStartParser(AuthentificateDto authdata);
        Task<OperationResult<object>> SetKeywordsFromText(Guid clientId, string text);
        Task<OperationResult<object>> SetGroupsNamesForParser(Guid clientId, IEnumerable<string> groupNames);
        Task<OperationResult<ParserAuthResultDto>> SubmitVerificationCodeFromTelegram(Guid parserId, int verificationCode);
        Task<OperationResult<object>> SubmitTwoFactorPassword(Guid clientId, int twofactorpassword);
        Task<OperationResult<EnterToSessionByKeyResponceDto>> EnterToSessionByKeyAndPassword(Guid parserId, string sessionPassword);

        void StartParsing(Guid parserId);
        void StopParsing(Guid parserId);
        void DisposeParser(Guid clientId);

    }
}
