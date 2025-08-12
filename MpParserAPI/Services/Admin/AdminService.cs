using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MpParserAPI.Common;
using MpParserAPI.Controllers;
using MpParserAPI.DbContext;
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
        public async Task<OperationResult<object>> AddTimeParsing(AddTimeParsingModelDto modelDto)
        {
            _logger.LogInformation("Начинаем добавление времени парсинга клиенту:");
            _logger.LogInformation($"Переданные данные из модели: ParserId = {modelDto.ParserId}, Hours = {modelDto.Hours}, Minutes = {modelDto.Minutes}");
            using var database = await _dbContextFactory.CreateDbContextAsync();
            var mappedParserIdToGuid = Guid.Parse(modelDto.ParserId);
            var existParserInDb = await database.ParsersStates.FirstOrDefaultAsync(x => x.ParserId == mappedParserIdToGuid);
            if (existParserInDb != null)
            {
                existParserInDb.PaidMinutes = TimeSpan.FromHours(modelDto.Hours) + TimeSpan.FromMinutes(modelDto.Minutes);
                existParserInDb.TotalParsingMinutes += TimeSpan.FromHours(modelDto.Hours) + TimeSpan.FromMinutes(modelDto.Minutes);
                _logger.LogInformation($"Записанное время в бд: Оплаченное время: {existParserInDb.PaidMinutes}, Общее время парсинга: {existParserInDb.TotalParsingMinutes}");
                var existInParserDataparser = _parserDataStorage.TryGetParser(mappedParserIdToGuid, out var parserData);
                if (existInParserDataparser)
                {
                    
                    parserData.TotalParsingMinutes += TimeSpan.FromHours(modelDto.Hours) + TimeSpan.FromMinutes(modelDto.Minutes);
                    _logger.LogInformation($"Записанное общее время в ParserData: {parserData.TotalParsingMinutes}, {TimeSpan.FromHours(modelDto.Hours) + TimeSpan.FromMinutes(modelDto.Minutes)}");
                }
            }
            await database.SaveChangesAsync();
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
