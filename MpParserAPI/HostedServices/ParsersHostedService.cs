using Microsoft.EntityFrameworkCore;
using MpParserAPI.DbContext;
using MpParserAPI.Interfaces;

namespace MpParserAPI.HostedServices
{
    public class ParsersHostedService : BackgroundService, IHostedService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IParserDataStorage _parserDataStorage;
        public ParsersHostedService(IServiceScopeFactory scopeFactory, IParserDataStorage parserDataStorage)
        {
            _scopeFactory = scopeFactory;
            _parserDataStorage = parserDataStorage;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _scopeFactory.CreateScope();
                var database = scope.ServiceProvider.GetRequiredService<ParserDbContext>();

                var allParsersData = await _parserDataStorage.GetAllParsers();
                var now = DateTime.UtcNow;

                var parserStates = await database.ParsersStates
                    .Where(p => allParsersData.Select(d => d.Id).Contains(p.ParserId))
                    .ToDictionaryAsync(p => p.ParserId, stoppingToken);

                foreach (var parserData in allParsersData.Where(p => p.IsParsingStarted && p.ParsingStartedAt.HasValue))
                {
                    var timeElapsed = now - parserData.ParsingStartedAt.Value;
                    if (timeElapsed <= TimeSpan.Zero)
                        continue;

                    if (parserStates.TryGetValue(parserData.Id, out var state))
                    {
                        state.TotalParsingTime = state.TotalParsingTime - timeElapsed;
                        if (state.TotalParsingTime < TimeSpan.Zero)
                            state.TotalParsingTime = TimeSpan.Zero;

                        if (state.TotalParsingTime == TimeSpan.Zero)
                        {
                            parserData.IsParsingStarted = false;
                        }

                        parserData.TotalParsingTime = state.TotalParsingTime;
                        parserData.ParsingStartedAt = now;
                    }
                }

                await database.SaveChangesAsync(stoppingToken);

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

    }
}
