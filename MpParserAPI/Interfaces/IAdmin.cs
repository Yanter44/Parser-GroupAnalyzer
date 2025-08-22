using MpParserAPI.Common;
using MpParserAPI.Models.AdminDtos;

namespace MpParserAPI.Interfaces
{
    public interface IAdmin
    {
        Task<OperationResult<object>> SetSubscriptionType(SetSubscriptionTypeModelDto modelDto);
        Task<OperationResult<object>> DeleteUserAndParser(DeleteUserAndParserDto modelDto);
    }
}
