using Microsoft.EntityFrameworkCore;
using MpParserAPI.Common;
using MpParserAPI.DbContext;
using MpParserAPI.Enums;
using MpParserAPI.Interfaces;
using MpParserAPI.Models;
using MpParserAPI.Models.Dtos;
using TL;
using WTelegram;

namespace MpParserAPI.Services
{
    public class ParserAuthoriseService : IParserAuthentificate
    {
        private readonly ICloudinaryService _cloudinaryService;
        private readonly IGenerator _generatorService;
        private readonly IDbContextFactory<ParserDbContext> _dbContextFactory;
        private readonly IParserDataStorage _parserStorage;
        public ParserAuthoriseService(ICloudinaryService cloudinaryService,
                             IGenerator generator,
                             IDbContextFactory<ParserDbContext> dbContextFactory,
                             IParserDataStorage parserStorage
                            )
        {
            _cloudinaryService = cloudinaryService;
            _generatorService = generator;
            _dbContextFactory = dbContextFactory;
            _parserStorage = parserStorage;
    
        }
        public async Task<OperationResult<Guid>> RequestLoginAsync(string phone)
        {
            if (_parserStorage.TryGetTempClientByPhone(phone, out var existingClient))
            {
                existingClient.Dispose();
                _parserStorage.RemoveTempClientByPhone(phone);
            }
            var tempAuthId = Guid.NewGuid();

            _parserStorage.AddOrUpdateTemporaryAuthData(tempAuthId, new TemporaryAuthData { Phone = phone });

            if (_parserStorage.TryGetTempClient(tempAuthId, out var oldClient))
            {
                oldClient.Dispose();
                _parserStorage.RemoveTempClient(tempAuthId);
            }

            var tempClient = new Client(what => Config(what, tempAuthId, isTemp: true));

            _parserStorage.AddTempClient(tempAuthId, tempClient);

            var loginResult = await tempClient.Login(phone);
            switch (loginResult)
            {
                case "verification_code":
                    return OperationResult<Guid>.Ok(tempAuthId, ErrorCodes.NeedVerificationCode);
                case "password":
                    return OperationResult<Guid>.Ok(tempAuthId, ErrorCodes.NeedTwoFactorPassword);
                case null:
                    var finalizeResult = await FinalizeParserCreation(tempAuthId);
                    if (!finalizeResult.Success)
                        return OperationResult<Guid>.Fail(finalizeResult.Message);

                    var parserId = finalizeResult.Data.ParserId;
                    return OperationResult<Guid>.Ok(parserId, finalizeResult.Message);
                default:
                    return OperationResult<Guid>.Fail("Неожиданный результат: " + loginResult);
            }
        }



        public async Task<OperationResult<ParserAuthResultDto>> SubmitVerificationCodeFromTelegram(Guid tempAuthId, int verificationCode)
        {
            if (!_parserStorage.TryGetTempClient(tempAuthId, out var tempClient))
                return OperationResult<ParserAuthResultDto>.Fail("Временная авторизация не найдена.");

            if (_parserStorage.TryGetTemporaryAuthData(tempAuthId, out var tempData))
                tempData.VerificationCode = verificationCode;       
            else
                return OperationResult<ParserAuthResultDto>.Fail("Данные временной авторизации не найдены.");

            try
            {
                var result = await tempClient.Login(verificationCode.ToString());

                if (result == "password")
                {
                    return OperationResult<ParserAuthResultDto>.Fail(ErrorCodes.NeedTwoFactorPassword);
                }
                else if (result == null)
                {
                    return await FinalizeParserCreation(tempAuthId);
                }
                else
                {
                    return OperationResult<ParserAuthResultDto>.Fail("Неизвестный результат авторизации: " + result);
                }
            }
            catch (RpcException ex) when (ex.Code == 400 && ex.Message == "PHONE_CODE_INVALID")
            {
                tempClient.Reset(); 

                tempData.VerificationCode = null;
                _parserStorage.AddOrUpdateTemporaryAuthData(tempAuthId, tempData);
                return OperationResult<ParserAuthResultDto>.Fail(ErrorCodes.InvalidVerificationCode);
            }
            catch (RpcException ex) when (ex.Code == 420) 
            {
                var waitSeconds = int.Parse(ex.Message.Split('_').Last());
                var waitTime = TimeSpan.FromSeconds(waitSeconds);

                return OperationResult<ParserAuthResultDto>.Fail(
                    $"Слишком много запросов. Повторите через {waitTime:mm\\:ss} минут");
            }
        }
        public async Task<OperationResult<bool>> ResendVerificationCode(Guid tempAuthId)
        {
            if (!_parserStorage.TryGetTempClient(tempAuthId, out var client))
                return OperationResult<bool>.Fail("Клиент не найден");

            if (!_parserStorage.TryGetTemporaryAuthData(tempAuthId, out var tempData))
                return OperationResult<bool>.Fail("Данные авторизации утеряны");

            try
            {
                tempData.VerificationCode = null;
                _parserStorage.AddOrUpdateTemporaryAuthData(tempAuthId, tempData);

                client.Reset(); 

                var loginResult = await client.Login(tempData.Phone);

                if (loginResult == "verification_code")
                    return OperationResult<bool>.Ok(true, "Новый код отправлен");

                return OperationResult<bool>.Fail("Не удалось запросить код: " + loginResult);
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.Fail("Ошибка: " + ex.Message);
            }
        }


        public async Task<OperationResult<ParserAuthResultDto>> SubmitTwoFactorPassword(Guid tempAuthId, string twoFactorPassword)
        {
            if (!_parserStorage.TryGetTempClient(tempAuthId, out var tempClient))
                return OperationResult<ParserAuthResultDto>.Fail("Временная авторизация не найдена.");

            if (!_parserStorage.TryGetTemporaryAuthData(tempAuthId, out var tempData))
                return OperationResult<ParserAuthResultDto>.Fail("Временные данные для двухфакторного пароля не найдены.");

            tempData.TwoFactorPassword = twoFactorPassword;

            var result = await tempClient.Login(twoFactorPassword);

            if (result == null)
            {
                return await FinalizeParserCreation(tempAuthId);
            }
            else
            {
                return OperationResult<ParserAuthResultDto>.Fail("Неожиданный результат авторизации: " + result);
            }
        }

        private async Task<OperationResult<ParserAuthResultDto>> FinalizeParserCreation(Guid tempAuthId)
        {
            if (!_parserStorage.TryGetTempClient(tempAuthId, out var tempClient))
                return OperationResult<ParserAuthResultDto>.Fail("Временная сессия не найдена.");

            _parserStorage.TryGetTemporaryAuthData(tempAuthId, out var tempData);
            var phone = tempData.Phone;
            if (string.IsNullOrWhiteSpace(phone))
                return OperationResult<ParserAuthResultDto>.Fail("Телефон не найден.");

            if (IsPhoneAlreadyInUse(phone))
                return OperationResult<ParserAuthResultDto>.Fail("Этот аккаунт уже используется. Войдите в старую сессию или используйте другой аккаунт.");

            var parserId = Guid.NewGuid();
            var password = _generatorService.GenerateRandomPassword();

            if (tempClient is IDisposable disposableClient)
            {
                disposableClient.Dispose(); 
            }
            else if (tempClient is IAsyncDisposable asyncDisposableClient)
            {
                await asyncDisposableClient.DisposeAsync();
            }

            _parserStorage.RemoveTempClient(tempAuthId);

            var sessionPath = GetSessionPath(phone, isTemp: true);
            var finalSessionPath = GetSessionPath(phone, isTemp: false);

            if (File.Exists(sessionPath))
            {
                if (File.Exists(finalSessionPath))
                    File.Delete(finalSessionPath);
                File.Move(sessionPath, finalSessionPath);
            }

            var parser = new ParserData(parserId, password, phone);
         
            _parserStorage.AddOrUpdateParser(parserId, parser);

            parser.Client = new Client(what => Config(what, parserId));
            await parser.Client.LoginUserIfNeeded();

            parser.AuthState = TelegramAuthState.Authorized;


            _parserStorage.TryRemoveTemporaryAuthData(tempAuthId);

            var newparserStateModel = new ParserStateTable()
            {
                ParserId = parserId,
                Password = password,
                Keywords = new string[] { },
                Phone = phone,
                SpamWords = new List<string>(),
                TotalParsingMinutes = TimeSpan.FromHours(30),
                TargetGroups = new List<GroupReference>()
            };

            using var database = await _dbContextFactory.CreateDbContextAsync();
            database.ParsersStates.Add(newparserStateModel);
            await database.SaveChangesAsync();

            return OperationResult<ParserAuthResultDto>.Ok(new ParserAuthResultDto
            {
                ParserId = parserId,
                Password = password
            }, "Успешно вошли в Telegram.");
        }


        private string Config(string what, Guid id, bool isTemp = false)
        {
            var isParserId = _parserStorage.ContainsParser(id);
            var phone = isParserId
                ? (_parserStorage.TryGetParser(id, out var parser) ? parser.Phone : null)
                : (_parserStorage.TryGetTemporaryAuthData(id, out var temp) ? temp.Phone : null);

            if (what == "session_pathname")
            {
                return GetSessionPath(phone, isTemp);
            }

            return what switch
            {
                "api_id" => "22262339",
                "api_hash" => "fc15371db5ea0ba274b93faf572aec6b",
                "phone_number" => phone,
                "verification_code" => _parserStorage.TryGetTemporaryAuthData(id, out var tempData)
                    ? tempData.VerificationCode?.ToString()
                    : null,
                "password" => _parserStorage.TryGetTemporaryAuthData(id, out var pwdData)
                    ? pwdData.TwoFactorPassword
                    : null,
                _ => null
            };
        }

        private string GetSessionPath(string phone, bool isTemp)
        {
            var cleanedPhone = new string(phone.Where(char.IsDigit).ToArray());
            var sessionsFolder = Path.Combine(AppContext.BaseDirectory, "sessions");
            if (!Directory.Exists(sessionsFolder))
                Directory.CreateDirectory(sessionsFolder);
            return Path.Combine(sessionsFolder, $"{(isTemp ? "temp_session" : "session")}_{cleanedPhone}.session");
        }

        public bool IsPhoneAlreadyInUse(string phone)
        {
            return _parserStorage.GetAllParsers().Any(p => p.Phone == phone && p.Client != null);
        }

        public Task<OperationResult<ParserAuthResultDto>> EnterToSessionByKeyAndPassword(Guid parserId, string sessionPassword)
        {
            if (_parserStorage.TryGetParser(parserId, out var parser) && parser.Password == sessionPassword)
            {
                return Task.FromResult(OperationResult<ParserAuthResultDto>.Ok(new ParserAuthResultDto
                {
                    ParserId = parserId,
                    Password = sessionPassword
                }, "Вход в сессию выполнен."));
            }

            return Task.FromResult(OperationResult<ParserAuthResultDto>.Fail("Неверный ключ сессии или пароль."));
        }
    }
}
