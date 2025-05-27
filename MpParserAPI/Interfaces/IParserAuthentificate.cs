using MpParserAPI.Common;
using MpParserAPI.Models.Dtos;

namespace MpParserAPI.Interfaces
{
    public interface IParserAuthentificate
    {
        Task<OperationResult<Guid>> RequestLoginAsync(string phone);
        Task<OperationResult<ParserAuthResultDto>> SubmitVerificationCodeFromTelegram(Guid parserId, int verificationCode);
        Task<OperationResult<ParserAuthResultDto>> SubmitTwoFactorPassword(Guid parserId, string twofactorpassword);
        Task<OperationResult<ParserAuthResultDto>> EnterToSessionByKeyAndPassword(Guid parserId, string sessionPassword);

    }
}
