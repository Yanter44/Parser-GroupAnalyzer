using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MpParserAPI.Common;
using MpParserAPI.Controllers;
using MpParserAPI.DbContext;
using MpParserAPI.Interfaces;
using MpParserAPI.Models;
using MpParserAPI.Models.Dtos;
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
        public async Task<OperationResult<ParserAuthResultDto>> LoginAndStartParser(AuthentificateDto logindata)
        {
            if (string.IsNullOrWhiteSpace(logindata.phone))
                return OperationResult<ParserAuthResultDto>.Fail("Данные о логине были пусты.");

            var parserId = Guid.NewGuid();
            var generatedPassword = _generatorService.GenerateRandomPassword();

            if (IsPhoneAlreadyInUse(logindata.phone))
                return OperationResult<ParserAuthResultDto>.Fail("Этот аккаунт уже используется. Войдите в старую сессию или используйте другой аккаунт.");

            var parserData = new ParserData(parserId, generatedPassword, logindata.phone);

            _parserStorage.AddOrUpdateParser(parserId, parserData);

            await InitializeClient(parserId);
            _parserStorage.TryGetParser(parserId, out var parser);

            switch (parser.AuthState)
            {
                case TelegramAuthState.NeedVerificationCode:
                    return OperationResult<ParserAuthResultDto>.Fail(
                        new ParserAuthResultDto { ParserId = parserId },
                        $"Введите код, который вам прислал Telegram. Используйте /Auth/SendVerificationCodeFromTelegram"
                    );

                case TelegramAuthState.NeedPassword:
                    return OperationResult<ParserAuthResultDto>.Fail(
                        new ParserAuthResultDto { ParserId = parserId },
                        $"Введите двухфакторный пароль от аккаунта. Используйте /Auth/SendATwoFactorPassword"
                    );

                case TelegramAuthState.Authorized:
                    return OperationResult<ParserAuthResultDto>.Ok(
                        new ParserAuthResultDto { ParserId = parserId, Password = generatedPassword },
                        "Парсер запущен успешно"
                    );

                default:
                    return OperationResult<ParserAuthResultDto>.Fail("Ошибка авторизации.");
            }
        }

        public bool IsPhoneAlreadyInUse(string phone)
        {
            return _parserStorage.GetAllParsers().Any(p => p.Phone == phone && p.Client != null);
        }


        private async Task InitializeClient(Guid parserId)
        {
            var client = new Client(what => Config(what, parserId));
            _parserStorage.TryGetParser(parserId, out var parser);
            parser.Client = client;

            var loginResult = await client.Login(parser.Phone);

            switch (loginResult)
            {
                case "verification_code":
                    parser.AuthState = TelegramAuthState.NeedVerificationCode;
                    return;

                case "password":
                    parser.AuthState = TelegramAuthState.NeedPassword;
                    return;

                case null:
                    parser.AuthState = TelegramAuthState.Authorized;
                    return;
            }
        }
        public async Task<OperationResult<ParserAuthResultDto>> SubmitVerificationCodeFromTelegram(Guid parserId, int verificationCode)
        {
            if (!_parserStorage.ContainsParser(parserId))
                return OperationResult<ParserAuthResultDto>.Fail("Парсер не найден.");

            _parserStorage.TryGetParser(parserId, out var parser);
            var client = parser.Client;

            try
            {
                var loginResult = await client.Login(verificationCode.ToString());

                switch (loginResult)
                {
                    case "password":
                        parser.AuthState = TelegramAuthState.NeedPassword;
                        return OperationResult<ParserAuthResultDto>.Fail(
                            new ParserAuthResultDto { ParserId = parserId },
                            "Нужен двухфакторный пароль. Отправьте его через /Auth/SendATwoFactorPassword"
                        );

                    case null:  
                        parser.AuthState = TelegramAuthState.Authorized;
                        _parserStorage.TryRemoveTemporaryAuthData(parserId);
                        return OperationResult<ParserAuthResultDto>.Ok(
                            new ParserAuthResultDto { ParserId = parserId, Password = parser.Password },
                            "Успешно вошли в Telegram."
                        );

                    default:
                        return OperationResult<ParserAuthResultDto>.Fail("Неизвестный результат авторизации: " + loginResult);
                }
            }
            catch (TL.RpcException ex) when (ex.Message == "PHONE_CODE_INVALID")
            {
                return OperationResult<ParserAuthResultDto>.Fail("Неверный код подтверждения.");
            }
        }

        public async Task<OperationResult<ParserAuthResultDto>> SubmitTwoFactorPassword(Guid parserId, string twofactorpassword)
        {
            if (!_parserStorage.ContainsParser(parserId))
                return OperationResult<ParserAuthResultDto>.Fail($"Парсер с Id {parserId} не найден.");

            _parserStorage.TryGetParser(parserId, out var parser);
            var client = parser.Client;

            try
            {
                var loginResult = await client.Login(twofactorpassword);

                if (loginResult == null)
                {
                    parser.AuthState = TelegramAuthState.Authorized;
                    _parserStorage.TryRemoveTemporaryAuthData(parserId);
                    return OperationResult<ParserAuthResultDto>.Ok(
                        new ParserAuthResultDto { ParserId = parserId, Password = parser.Password },
                        "Успешно вошли в Telegram."
                    );
                }
                else
                {
                    return OperationResult<ParserAuthResultDto>.Fail("Неожиданный результат авторизации: " + loginResult);
                }
            }
            catch (TL.RpcException ex) when (ex.Message.Contains("PASSWORD_HASH_INVALID") || ex.Message.Contains("invalid password"))
            {
                return OperationResult<ParserAuthResultDto>.Fail("Неверный двухфакторный пароль.");
            }
        }



        public void ResetClient(Guid parserId)
        {
            _parserStorage.TryGetParser(parserId, out var parser);
            parser.Client.Dispose();
            parser.Client = new WTelegram.Client(what => Config(what, parserId));
        }
        private string Config(string what, Guid parserId)
        {
            _parserStorage.TryGetParser(parserId, out var parser);
            _parserStorage.TryGetTemporaryAuthData(parserId, out var temporarydata);

            if (what == "session_pathname")
            {
                var sessionsFolder = Path.Combine(AppContext.BaseDirectory, "sessions");
                if (!Directory.Exists(sessionsFolder))
                    Directory.CreateDirectory(sessionsFolder);

                var phone = parser?.Phone;
                if (string.IsNullOrEmpty(phone))
                    return null;

                var cleanedPhone = new string(phone.Where(char.IsDigit).ToArray());

                return Path.Combine(sessionsFolder, $"session_{cleanedPhone}.session");
            }

            return what switch
            {
                "api_id" => "22262339",
                "api_hash" => "fc15371db5ea0ba274b93faf572aec6b",
                "phone_number" => parser?.Phone,
                "verification_code" => temporarydata?.VerificationCode != null
                    ? temporarydata.VerificationCode.ToString()
                    : null,
                "password" => temporarydata?.TwoFactorPassword != null
                    ? temporarydata.TwoFactorPassword.ToString()
                    : null,
                _ => null
            };
        }
        public async Task<OperationResult<ParserAuthResultDto>> EnterToSessionByKeyAndPassword(Guid parserId, string sessionPassword)
        {
            if (_parserStorage.TryGetParser(parserId, out var parser))
            {
                if (parser.Password == sessionPassword)
                {
                    return OperationResult<ParserAuthResultDto>.Ok(new ParserAuthResultDto
                    {
                        ParserId = parserId,
                        Password = sessionPassword
                    }, "Вход в сессию выполнен.");
                }
            }
            return OperationResult<ParserAuthResultDto>.Fail("Неверный ключ сессии или пароль.");
        }
    }
}
