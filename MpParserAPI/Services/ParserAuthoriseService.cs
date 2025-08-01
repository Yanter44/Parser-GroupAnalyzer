using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MpParserAPI.Common;
using MpParserAPI.DbContext;
using MpParserAPI.Enums;
using MpParserAPI.Interfaces;
using MpParserAPI.Models;
using MpParserAPI.Models.Dtos;
using Starksoft.Net.Proxy;
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
        private readonly ILogger<ParserAuthoriseService> _logger;
        private readonly ISpaceProxy _spaceProxyService;
        public ParserAuthoriseService(ICloudinaryService cloudinaryService,
                             IGenerator generator,
                             IDbContextFactory<ParserDbContext> dbContextFactory,
                             IParserDataStorage parserStorage,
                             ILogger<ParserAuthoriseService> logger,
                             ISpaceProxy spaceProxyService
                            )
        {
            _cloudinaryService = cloudinaryService;
            _generatorService = generator;
            _dbContextFactory = dbContextFactory;
            _parserStorage = parserStorage;
            _logger = logger;
            _spaceProxyService = spaceProxyService;
    
        }
        public async Task<bool> LoadAllParsersFromDbAsync()
        {
            try
            {
                using var database = await _dbContextFactory.CreateDbContextAsync();
                var allParserStatesInDb = await database.ParsersStates.ToListAsync();

                foreach (var parserState in allParserStatesInDb)
                {
                    var parserData = new ParserData(parserState.ParserId, parserState.Password, parserState.Phone)
                    {
                        Keywords = parserState.Keywords,
                        TargetGroups = parserState.TargetGroups
                                            .Select(g => new InputPeerChannel(g.ChatId, g.AccessHash))
                                            .Cast<InputPeer>()
                                            .ToList(),
                        TargetGroupTitles = parserState.TargetGroups.Select(x => x.Title).ToList(),
                        IsParsingStarted = false,
                        TotalParsingMinutes = parserState.TotalParsingMinutes,
                    };

                    _parserStorage.AddOrUpdateParser(parserState.ParserId, parserData);

                    parserData.Client = new Client(what =>
                    {
                        if (what == "session_pathname")
                            return GetSessionPath(parserState.Phone, isTemp: false);

                        return what switch
                        {
                            "api_id" => "22262339",
                            "api_hash" => "fc15371db5ea0ba274b93faf572aec6b",
                            "phone_number" => parserState.Phone,
                            _ => null
                        };
                    });
                    var gettedAvailableProxy = await _spaceProxyService.GetAndSetAvailableProxy(parserData.Id);
                    if(gettedAvailableProxy != null)
                    {
                        parserData.Client.TcpHandler = async (address, port) =>
                        {

                            var proxy = new Leaf.xNet.Socks5ProxyClient(gettedAvailableProxy.IpAddress, gettedAvailableProxy.Socks5Port)
                            {
                                Username = gettedAvailableProxy.Username,
                                Password = gettedAvailableProxy.Password
                            };
                            return proxy.CreateConnection(address, port);
                        };
                    }
                  
                    Environment.SetEnvironmentVariable("WTG_LOG", "ALL");
                    await parserData.Client.LoginUserIfNeeded();

                    parserData.AuthState = TelegramAuthState.Authorized;

                    _parserStorage.AddOrUpdateParser(parserState.ParserId, parserData);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка при загрузке парсеров из БД: {ex.Message}");
                return false;
            }
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

            if ( await IsPhoneAlreadyInUse(phone))
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

            var gettedAvailableProxy = await _spaceProxyService.GetAndSetAvailableProxy(parser.Id);
            if (gettedAvailableProxy != null)
            {
                parser.Client.TcpHandler = async (address, port) =>
                {
                    var proxy = new Leaf.xNet.Socks5ProxyClient(gettedAvailableProxy.IpAddress, gettedAvailableProxy.Socks5Port)
                    {
                        Username = gettedAvailableProxy.Username,
                        Password = gettedAvailableProxy.Password
                    };
                    return proxy.CreateConnection(address, port);
                };
            }
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
                TotalParsingMinutes = TimeSpan.FromMinutes(30),
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

            _parserStorage.TryGetParser(id, out var parser);
            _parserStorage.TryGetTemporaryAuthData(id, out var tempData);
            var phone = isParserId
                ? parser?.Phone
                : tempData?.Phone;

            return what switch
            {
                "session_pathname" => GetSessionPath(phone, isTemp),
                "api_id" => "22262339",
                "api_hash" => "fc15371db5ea0ba274b93faf572aec6b",
                "phone_number" => phone,
                "verification_code" => tempData?.VerificationCode?.ToString(),
                "password" => tempData?.TwoFactorPassword,
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

        public async Task<bool> IsPhoneAlreadyInUse(string phone)
        {
            var allparsers = await _parserStorage.GetAllParsers();
             var isphonealreadyused = allparsers.Any(p => p.Phone == phone && p.Client != null);
            return isphonealreadyused;
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
