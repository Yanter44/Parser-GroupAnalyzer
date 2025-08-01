using Microsoft.EntityFrameworkCore;
using MpParserAPI.Common;
using MpParserAPI.DbContext;
using MpParserAPI.Interfaces;
using MpParserAPI.Models.AdminDtos;

namespace MpParserAPI.Services.Admin
{
    public class AdminService : IAdmin
    {
        private readonly IDbContextFactory<ParserDbContext> _dbContextFactory;
        private readonly IParserDataStorage _parserDataStorage;
        public AdminService(IDbContextFactory<ParserDbContext> dbContextFactory, IParserDataStorage parserDataStorage)
        {
            _dbContextFactory = dbContextFactory;
            _parserDataStorage = parserDataStorage;
        }
        public async Task<OperationResult<object>> AddTimeParsing(AddTimeParsingModelDto modelDto)
        {
            using var database = await _dbContextFactory.CreateDbContextAsync();
            var mappedParserIdToGuid = Guid.Parse(modelDto.ParserId);
            var existParserInDb = await database.ParsersStates.FirstOrDefaultAsync(x => x.ParserId == mappedParserIdToGuid);
            if (existParserInDb != null)
            {
                existParserInDb.TotalParsingMinutes += TimeSpan.FromHours(modelDto.Hours) + TimeSpan.FromMinutes(modelDto.Minutes);
                var existInParserDataparser = _parserDataStorage.TryGetParser(mappedParserIdToGuid, out var parserData);
                if (existInParserDataparser)
                {
                    parserData.TotalParsingMinutes += TimeSpan.FromHours(modelDto.Hours) + TimeSpan.FromMinutes(modelDto.Minutes);
                }
            }
            await database.SaveChangesAsync();
            return OperationResult<object>.Ok();
            
        }

        public Task<OperationResult<object>> DeleteUserAndParser(DeleteUserAndParserDto modelDto)
        {
            throw new NotImplementedException();
        }
    }
}
