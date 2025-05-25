using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MpParserAPI.Common;
using MpParserAPI.Controllers;
using MpParserAPI.DbContext;
using MpParserAPI.Interfaces;
using MpParserAPI.Models;
using MpParserAPI.Models.Dtos;
using MpParserAPI.Services;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using TL;
using WTelegram;
using static Microsoft.Extensions.Logging.EventSource.LoggingEventSource;

public class ParserService : IParser
{
    private readonly ICloudinaryService _cloudinaryService;
    private readonly IGenerator _generatorService;
    private readonly IDbContextFactory<ParserDbContext> _dbContextFactory;
    private readonly IParserDataStorage _parserStorage;
    private readonly IHubContext<ParserHub> _parserHubContext;
    public ParserService(ICloudinaryService cloudinaryService,
                         IGenerator generator,
                         IDbContextFactory<ParserDbContext> dbContextFactory,
                         IParserDataStorage parserStorage,
                         IHubContext<ParserHub> parserhubContext)
    {
        _cloudinaryService = cloudinaryService;
        _generatorService = generator;
        _dbContextFactory = dbContextFactory;
        _parserStorage = parserStorage;
        _parserHubContext = parserhubContext;
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


    private async Task HandleUpdate(Guid parserId, IObject updateObj)
    {
        if (!_parserStorage.ContainsParser(parserId))
            return;

        _parserStorage.TryGetParser(parserId, out var parser);
      
        var parserData = parser;

        if (updateObj is UpdatesBase updates)
        {
            foreach (var upd in updates.UpdateList)
            {
                if (upd is UpdateNewMessage unm && unm.message is Message msg)
                {
                    if (_parserStorage.ContainsParser(parserId) &&
                        parser.TargetGroups.Any(peer => msg.peer_id.ID == peer.ID))
                    {
                        if (msg.from_id is PeerUser peerUser)
                        {
                            var wordsInMessage = Regex.Split(msg.message.ToLower(), @"\W+");
                            var keywords = parser.Keywords;

                            if (keywords.Any(kw => wordsInMessage.Contains(kw.Trim().ToLower())))
                            {
                                var userId = peerUser.user_id;
                                var dialogs = await parserData.Client.Messages_GetAllDialogs();

                                if (dialogs.users.TryGetValue(userId, out var user))
                                {
                                    var userPhotoId = user.photo?.photo_id;
                                    await using var database = _dbContextFactory.CreateDbContext();
                                    var existingTelegramUser = await database.TelegramUsers
                                        .FirstOrDefaultAsync(x => x.TelegramUserId == userId);

                                    string imageUrl = null;

                                    if (existingTelegramUser == null || existingTelegramUser.ProfilePhotoId != userPhotoId)
                                    {
                                        if (user.photo != null)
                                        {
                                            await using var userProfileImageBytes = new MemoryStream();
                                            await parserData.Client.DownloadProfilePhotoAsync(user, userProfileImageBytes, true, true);
                                            userProfileImageBytes.Position = 0;

                                            if (userProfileImageBytes.Length > 0)
                                            {
                                                imageUrl = await _cloudinaryService.UploadImageAsync(userProfileImageBytes.ToArray());
                                            }
                                        }

                                        if (string.IsNullOrEmpty(imageUrl))
                                        {
                                            imageUrl = "https://res.cloudinary.com/ddg6n36uq/image/upload/v1747355623/c31bd024-f78b-459b-b96a-438eeb186eeb.png";
                                        }
                                    }
                                    else { imageUrl = existingTelegramUser.ProfileImageUrl; }

                                    if (existingTelegramUser == null)
                                    {
                                        existingTelegramUser = new TelegramUser
                                        {
                                            TelegramUserId = user.id,
                                            FirstName = user.first_name ?? "Отсутствует",
                                            LastName = user.last_name ?? "Отсутствует",
                                            Username = user.username ?? "Отсутствует",
                                            Phone = user.phone ?? "Отсутствует",
                                            ProfileImageUrl = imageUrl,
                                            ProfilePhotoId = userPhotoId 
                                        };
                                        database.TelegramUsers.Add(existingTelegramUser);
                                    }
                                    else
                                    {
                                        existingTelegramUser.FirstName = user.first_name ?? "Отсутствует";
                                        existingTelegramUser.LastName = user.last_name ?? "Отсутствует";
                                        existingTelegramUser.Username = user.username ?? "Отсутствует";
                                        existingTelegramUser.Phone = user.phone ?? "Отсутствует";

                                        if (!string.IsNullOrEmpty(imageUrl))
                                        {
                                            existingTelegramUser.ProfileImageUrl = imageUrl;
                                        }

                                        existingTelegramUser.ProfilePhotoId = userPhotoId; 
                                        database.TelegramUsers.Update(existingTelegramUser);
                                    }

                                    var parserlog = new ParserLogs
                                    {
                                        ParserId = parserId,
                                        TelegramUser = existingTelegramUser,
                                        MessageText = msg.message
                                    };


                                    database.ParserLogsTable.Add(parserlog);
                                    await database.SaveChangesAsync();

                                    await _parserHubContext.Clients.Group(parserId.ToString()).SendAsync("ReceiveMessage", new
                                    {
                                        ProfileImageUrl = imageUrl,
                                        Name = user.first_name,
                                        Username = user.username,
                                        MessageText = msg.message
                                    });
                                    Console.WriteLine("Сообщение из отслеживаемой группы:");
                                    Console.WriteLine($"ID: {user.id}");
                                    Console.WriteLine($"Имя: {user.first_name} {user.last_name}");
                                    Console.WriteLine($"Никнейм: @{user.username}");
                                    Console.WriteLine($"Телефон: {user.phone}");
                                    Console.WriteLine($"Текст: {msg.message}");
                                    Console.WriteLine(new string('-', 50));
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    public async Task<OperationResult<object>> SetGroupsNamesForParser(Guid parserId, IEnumerable<string> groupNames)
    {
        if (!_parserStorage.ContainsParser(parserId))
        {
            return OperationResult<object>.Fail($"Парсер с id {parserId} не найден.");
        }
        _parserStorage.TryGetParser(parserId, out var parser);
        var client = parser.Client;

        var dialogs = await client.Messages_GetAllDialogs();
        var newGroups = new List<InputPeer>();

        foreach (var groupName in groupNames)
        {
            var group = dialogs.chats.Values.OfType<ChatBase>().FirstOrDefault(c => c.Title == groupName);
            if (group != null)
            {
                newGroups.Add(group.ToInputPeer());
            }
        }

        if (_parserStorage.ContainsParser(parserId))
        {
            parser.TargetGroups.Clear();
            parser.TargetGroups.AddRange(newGroups);
        }
        else
        {
            parser.TargetGroups = newGroups;
        }
        return OperationResult<object>.Ok("Группы успешно установлены.");
    }


    public async Task<OperationResult<ParserAuthResultDto>> SubmitVerificationCodeFromTelegram(Guid parserId, int verificationCode)
    {
        if (!_parserStorage.ContainsParser(parserId))
            return OperationResult<ParserAuthResultDto>.Fail("Парсер не найден.");

        _parserStorage.TryGetParser(parserId, out var parser);
        var client = parser.Client;

        var temp = _parserStorage.GetOrCreateTemporaryAuthData(parserId);
        temp.VerificationCode = verificationCode;

        string loginResult;
        try
        {
            ResetClient(parserId);
            parser = _parserStorage.GetParser(parserId);
            client = parser.Client;
            loginResult = await client.Login(parser.Phone);
        }
        catch (TL.RpcException ex) when (ex.Message == "PHONE_CODE_INVALID")
        {
            temp.VerificationCode = null;
            return OperationResult<ParserAuthResultDto>.Fail("Неверный код подтверждения.");
        }

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
                    new ParserAuthResultDto
                    {
                        ParserId = parserId,
                        Password = parser.Password
                    },
                    "Успешно вошли в Telegram."
                );

            default:
                return OperationResult<ParserAuthResultDto>.Fail("Неизвестный результат авторизации.");
        }
    }
    public void ResetClient(Guid parserId)
    {
        _parserStorage.TryGetParser(parserId, out var parser);
        parser.Client.Dispose();
        parser.Client = new WTelegram.Client(what => Config(what, parserId));
    }
    public async Task<OperationResult<ParserAuthResultDto>> SubmitTwoFactorPassword(Guid parserId, int twofactorpassword)
    {
        if (!_parserStorage.ContainsParser(parserId))
            return OperationResult<ParserAuthResultDto>.Fail($"Парсер с Id {parserId} не найден.");

        _parserStorage.TryGetParser(parserId, out var parser);
        var client = parser.Client;
        var temp = _parserStorage.GetOrCreateTemporaryAuthData(parserId);
        temp.TwoFactorPassword = twofactorpassword;

        try
        {
            ResetClient(parserId);
            parser = _parserStorage.GetParser(parserId);
            client = parser.Client;

            var loginResult = await client.Login(parser.Phone);

             parser.AuthState = TelegramAuthState.Authorized;
             _parserStorage.TryRemoveTemporaryAuthData(parserId);

            return OperationResult<ParserAuthResultDto>.Ok(new ParserAuthResultDto {ParserId = parserId, Password = parser.Password},"Успешно вошли в Telegram.");
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("PASSWORD_HASH_INVALID") || ex.Message.Contains("invalid password"))
            {
                return OperationResult<ParserAuthResultDto>.Fail("Неверный двухфакторный пароль.");
            }
            return OperationResult<ParserAuthResultDto>.Fail("Ошибка авторизации: " + ex.Message);
        }
    }



    public async Task<OperationResult<object>> SetKeywordsFromText(Guid parserId, string text)
    {
        if (!_parserStorage.ContainsParser(parserId))
            return OperationResult<object>.Fail($"Парсер с Id {parserId} не найден.");

        _parserStorage.TryGetParser(parserId, out var parser);
        var keywords = text.Split(" ", StringSplitOptions.RemoveEmptyEntries)
                           .Select(k => k.Trim().ToLower())
                           .Where(k => !string.IsNullOrWhiteSpace(k))
                           .ToArray();

        if (keywords.Length == 0)
            return OperationResult<object>.Fail("Не удалось извлечь ключевые слова из текста.");

        parser.Keywords = keywords;
        return OperationResult<object>.Ok("Ключевые слова успешно установлены.");
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


    public async Task<bool> IsParserAuthValid(Guid parserId, string password)
    {

        if (_parserStorage.TryGetParser(parserId, out var parser))
        {
            if (parser.Password == password)
                return true;
            
        }
        return false;
    }

    public async Task<GetParserStateResponceDto> GetParserState(Guid parserId)
    {
        if (!_parserStorage.TryGetParser(parserId, out var parser) || parser.Client == null)
            return null;

        await using var db = _dbContextFactory.CreateDbContext();

        string profileImageUrl = null;
        string profileNickName = parser.Client.User.username ?? "Ник отсутствует";

        var photo = parser.Client.User.photo;
        var photoId = photo?.photo_id;
        var telegramUserId = parser.Client.User.ID;

        var existingUser = await db.TelegramUsers.FirstOrDefaultAsync(x => x.TelegramUserId == telegramUserId);

        if (photo == null)
        {
            profileImageUrl = "https://res.cloudinary.com/ddg6n36uq/image/upload/v1747355623/c31bd024-f78b-459b-b96a-438eeb186eeb.png";
        }
        else if (existingUser == null || existingUser.ProfilePhotoId != photoId)
        {
            using var userProfileImageBytes = new MemoryStream();
            var result = await parser.Client.DownloadProfilePhotoAsync(parser.Client.User, userProfileImageBytes, true, true);
            userProfileImageBytes.Position = 0;

            profileImageUrl = userProfileImageBytes.Length > 0
                ? await _cloudinaryService.UploadImageAsync(userProfileImageBytes.ToArray())
                : "https://res.cloudinary.com/ddg6n36uq/image/upload/v1747355623/c31bd024-f78b-459b-b96a-438eeb186eeb.png";
        }
        else
        {
            profileImageUrl = existingUser.ProfileImageUrl;
        }

        var parserLogsRaw = await db.ParserLogsTable
            .Where(x => x.ParserId == parserId)
            .Include(x => x.TelegramUser)
            .Select(x => new ParserLogsResponceDto
            {
                MessageText = x.MessageText,
                FirstName = x.TelegramUser.FirstName,
                Username = x.TelegramUser.Username,
                ProfileImageUrl = x.TelegramUser.ProfileImageUrl
            })
            .ToListAsync();

        var targetGroupsDisplay = parser.TargetGroups?.Select(g => g.ToString()).ToList() ?? new();

        var response = new GetParserStateResponceDto
        {
            parserDataResponceDto = new ParserDataResponceDto
            {
                IsParsingStarted = parser.IsParsingStarted,
                Parserkeywords = parser.Keywords,
                ProfileImageUrl = profileImageUrl,
                ProfileNickName = profileNickName,
                TargetGroups = targetGroupsDisplay,
                ParserId = parser.Id, 
                ParserPassword = parser.Password 
            },
            parserLogs = parserLogsRaw
        };

        return response;
    }
    public void StartParsing(Guid parserId)
    {
        if (_parserStorage.TryGetParser(parserId, out var parser) && parser.Client != null)
        {
            Func<IObject, Task> handler = async update => await HandleUpdate(parserId, update);

            parser.Client.OnUpdates += handler;
            _parserStorage.AddHandler(parserId, handler);
            parser.IsParsingStarted = true;
        }
    }

    public void StopParsing(Guid parserId)
    {
        if (_parserStorage.TryGetParser(parserId, out var parser) && parser.Client != null &&
            _parserStorage.TryGetHandler(parserId, out var handler))
        {
            parser.Client.OnUpdates -= handler;
            _parserStorage.RemoveHandler(parserId);
            parser.IsParsingStarted = false;
        }
    }
    public void DisposeParser(Guid parserId)
    {
        if (_parserStorage.TryGetParser(parserId, out var parser))
        {
            parser.DisposeData();
            _parserStorage.TryRemoveParser(parserId);
        }
    }
}
