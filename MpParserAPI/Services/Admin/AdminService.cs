using Microsoft.EntityFrameworkCore;
using MpParserAPI.Common;
using MpParserAPI.DbContext;
using MpParserAPI.Enums;
using MpParserAPI.Interfaces;
using MpParserAPI.Models;
using MpParserAPI.Models.AdminDtos;

namespace MpParserAPI.Services.Admin
{
    public class AdminService : IAdmin
    {
        private readonly IDbContextFactory<ParserDbContext> _dbContextFactory;
        private readonly IParserDataStorage _parserDataStorage;
        private readonly IParserDataStorage _parserStorage;
        private readonly ILogger<AdminService> _logger;
        public AdminService(IDbContextFactory<ParserDbContext> dbContextFactory, IParserDataStorage parserDataStorage,
            IParserDataStorage parserdataStorage, ILogger<AdminService> logger)
        {
            _dbContextFactory = dbContextFactory;
            _parserDataStorage = parserDataStorage;
            _parserStorage = parserdataStorage;
            _logger = logger;
        }
        public async Task<OperationResult<object>> SetSubscriptionType(SetSubscriptionTypeModelDto modelDto)
        {
            _logger.LogInformation("Начало изменения подписки для парсера: {ParserId}", modelDto.ParserId);

            if (!Guid.TryParse(modelDto.ParserId, out var parserId))
                return OperationResult<object>.Fail("Неверный формат ParserId");

            if (!Enum.TryParse<SubscriptionType>(modelDto.SubscriptionType, out var subscriptionType))
                return OperationResult<object>.Fail("Неверный тип подписки");

            if (modelDto.DaysSubscription <= 0)
                return OperationResult<object>.Fail("Длительность подписки должна быть больше 0 дней");

            using var db = await _dbContextFactory.CreateDbContextAsync();

            var dbParser = await db.ParsersStates.FirstOrDefaultAsync(x => x.ParserId == parserId);
            if (dbParser == null)
                return OperationResult<object>.Fail("Парсер не найден");

            dbParser.SubscriptionType = subscriptionType;
            dbParser.SubscriptionEndDate = DateTime.UtcNow.AddDays(modelDto.DaysSubscription);

            var timeUntilEnd = dbParser.SubscriptionEndDate - DateTime.UtcNow;
            dbParser.TotalParsingMinutes = timeUntilEnd;

            if (_parserDataStorage.TryGetParser(parserId, out var parser))
            {
                parser.SubscriptionType = subscriptionType;
                parser.SubscriptionEndDate = dbParser.SubscriptionEndDate;
                parser.TotalParsingMinutes = timeUntilEnd;
            }

            await db.SaveChangesAsync();

            _logger.LogInformation(
            "Подписка обновлена. Тип: {SubscriptionType}, До конца: {TimeUntilEnd:dd\\.hh\\:mm}",
             subscriptionType,
             timeUntilEnd);
            return OperationResult<object>.Ok();
        }

        public async Task<OperationResult<object>> DeleteUserAndParser(DeleteUserAndParserDto modelDto)
        {
            try
            {
                Guid parserId = Guid.Parse(modelDto.ParserId);
                using var database = await _dbContextFactory.CreateDbContextAsync();

                if (!_parserDataStorage.TryGetParser(parserId, out var parser))
                    return OperationResult<object>.Fail("Парсер не найден");

                await StopAndCleanupParser(parser);


                DeleteSessionFiles(parser.Phone);

                var existParser = await database.ParsersStates.FirstOrDefaultAsync(x => x.ParserId == parserId);
                if (existParser != null)
                {
                    database.ParsersStates.Remove(existParser);
                    await database.SaveChangesAsync(); 
                }

                _parserDataStorage.TryRemoveParser(parserId);

                return OperationResult<object>.Ok("Пользователь удален успешно");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Произошла ошибка при удаления пользователя и парсера");
                return OperationResult<object>.Fail("Internal server error");
            }
        }

        private async Task StopAndCleanupParser(ParserData parserData)
        {
            try
            {
  
                parserData.ParsingTimer?.Change(Timeout.Infinite, 0);


                if (parserData.Client != null && parserData.AuthState != TelegramAuthState.None)
                {
                    await parserData.Client.DisposeAsync();
                }
                parserData.DisposeData();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Произошла ошибка при попытки удаления парсера");
            }
        }

        private void DeleteSessionFiles(string phone)
        {
            try
            {
                var mainSession = GetSessionPath(phone, false);
                var tempSession = GetSessionPath(phone, true);

                if (File.Exists(mainSession))
                    File.Delete(mainSession);

                if (File.Exists(tempSession))
                    File.Delete(tempSession);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Произошла ошибка при попытке удаления сессионного файла");
            }
        }
        private string GetSessionPath(string phone, bool isTemp)
        {
            var cleanedPhone = new string(phone.Where(char.IsDigit).ToArray());
            var sessionsFolder = Path.Combine(AppContext.BaseDirectory, "sessions");
            if (!Directory.Exists(sessionsFolder))
                Directory.CreateDirectory(sessionsFolder);
            return Path.Combine(sessionsFolder, $"{(isTemp ? "temp_session" : "session")}_{cleanedPhone}.session");
        }
    }
}
