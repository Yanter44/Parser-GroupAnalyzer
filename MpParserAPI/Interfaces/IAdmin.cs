using MpParserAPI.Common;
using MpParserAPI.Models.AdminDtos;

namespace MpParserAPI.Interfaces
{
    public interface IAdmin
    {
        Task<OperationResult<object>> AddTimeParsing(AddTimeParsingModelDto modelDto);
        Task<OperationResult<object>> DeleteUserAndParser(DeleteUserAndParserDto modelDto);
    }
}
