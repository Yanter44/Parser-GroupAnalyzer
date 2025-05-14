using MpParserAPI.Common;
using MpParserAPI.Models.Dtos;

namespace MpParserAPI.Interfaces
{
    public interface IParser
    {
        Task<OperationResult> LoginAndStartParser(AuthentificateDto authdata);
        Task<OperationResult> SetKeywordsFromText(Guid clientId, string text);
        Task<OperationResult> SetGroupsNamesForParser(Guid clientId, IEnumerable<string> groupNames);
        Task<OperationResult> SubmitVerificationCodeFromTelegram(Guid clientId, int verificationCode);
        Task<OperationResult> SubmitTwoFactorPassword(Guid clientId, int twofactorpassword);
        Task<OperationResult> EnterToSessionByKeyAndPassword(LoginToSessionDto logindata);
        void StopParser(Guid clientId);
    }
}
