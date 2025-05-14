using Microsoft.EntityFrameworkCore;
using MpParserAPI.Common;
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
    private readonly ParserDbContext _parserDatabase;
    private readonly IParserDataStorage _parserStorage;
    public ParserService(ICloudinaryService cloudinaryService, 
                         ParserDbContext parserDb,
                         IParserDataStorage parserStorage)
    {
        _cloudinaryService = cloudinaryService;
        _parserDatabase = parserDb;
        _parserStorage = parserStorage;
    }
    public async Task<OperationResult> LoginAndStartParser(AuthentificateDto logindata)
    {
        if (string.IsNullOrWhiteSpace(logindata.apiid) ||
        string.IsNullOrWhiteSpace(logindata.apihash) ||
        string.IsNullOrWhiteSpace(logindata.phone))
        {
            return OperationResult.Fail("Данные о логине были пусты.");
        }
        var parserId = Guid.NewGuid();
        var parserData = new ParserData(parserId, logindata.phone, logindata.apiid, logindata.apihash);

        _parserStorage.AddOrUpdateParser(parserId, parserData);
        await InitializeClient(parserId);
         _parserStorage.TryGetParser(parserId, out var parser);   
        switch (parser.AuthState)
        {
            case TelegramAuthState.NeedVerificationCode:
                return OperationResult.Fail($"Ваш созданный ParserId {parserId} Введите код который вам прислал телеграм: /Auth/SendVerificationCodeFromTelegram");

            case TelegramAuthState.NeedPassword:
                return OperationResult.Fail($"Ваш созданный ParserId {parserId} Введите двухфакторный пароль от аккаунта: /Auth/SendATwoFactorPassword");

            case TelegramAuthState.Authorized:
                return OperationResult.Ok($"Парсер запущен успешно. Ваш ParserId {parserId}");

            default:
                return OperationResult.Fail("Ошибка авторизации.");
        }
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
                client.OnUpdates += async update => await HandleUpdate(parserId, update);
                return;
        }
    }



    public void StopParser(Guid parserId)
    {
        if (_parserStorage.TryGetParser(parserId, out var parser))
        {
            parser.DisposeData();
            _parserStorage.TryRemoveParser(parserId);
        }
    }

    private string Config(string what, Guid parserId)
    {
        _parserStorage.TryGetParser(parserId, out var parser);
        _parserStorage.TryGetTemporaryAuthData(parserId, out var temporarydata);

        return what switch
        {
            "api_id" => parser?.ApiId,
            "api_hash" => parser?.ApiHash,
            "phone_number" => parser?.Phone,
            "verification_code" => temporarydata?.VerificationCode?.ToString(),
            "password" => temporarydata?.TwoFactorPassword.ToString(),
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

                                    var existingTelegramUser = await _parserDatabase.TelegramUsers
                                        .FirstOrDefaultAsync(x => x.TelegramUserId == userId);

                                    string imageUrl = null;

                                    if (existingTelegramUser == null || existingTelegramUser.ProfilePhotoId != userPhotoId)
                                    {
                                        var userProfileImageBytes = new MemoryStream();
                                        await parserData.Client.DownloadProfilePhotoAsync(user, userProfileImageBytes, true, true);
                                        userProfileImageBytes.Position = 0;

                                        if (userProfileImageBytes.Length > 0)
                                        {
                                            imageUrl = await _cloudinaryService.UploadImageAsync(userProfileImageBytes.ToArray());
                                        }
                                    }
                                    if (existingTelegramUser == null)
                                    {
                                        existingTelegramUser = new TelegramUser
                                        {
                                            TelegramUserId = user.id,
                                            FirstName = user.first_name,
                                            LastName = user.last_name,
                                            Username = user.username,
                                            Phone = user.phone,
                                            ProfileImageUrl = imageUrl,
                                            ProfilePhotoId = userPhotoId 
                                        };
                                        _parserDatabase.TelegramUsers.Add(existingTelegramUser);
                                    }
                                    else
                                    {
                                        existingTelegramUser.FirstName = user.first_name;
                                        existingTelegramUser.LastName = user.last_name;
                                        existingTelegramUser.Username = user.username;
                                        existingTelegramUser.Phone = user.phone;

                                        if (!string.IsNullOrEmpty(imageUrl))
                                        {
                                            existingTelegramUser.ProfileImageUrl = imageUrl;
                                        }

                                        existingTelegramUser.ProfilePhotoId = userPhotoId; 
                                        _parserDatabase.TelegramUsers.Update(existingTelegramUser);
                                    }

                                    var parserlog = new ParserLogs
                                    {
                                        ParserId = parserId,
                                        TelegramUser = existingTelegramUser,
                                        MessageText = msg.message
                                    };

                                    _parserDatabase.ParserLogsTable.Add(parserlog);
                                    await _parserDatabase.SaveChangesAsync();

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




    public async Task<OperationResult> SetGroupsNamesForParser(Guid parserId, IEnumerable<string> groupNames)
    {
        if (groupNames == null || !groupNames.Any())
        {
            return OperationResult.Fail("Список групп не может быть пустым.");
        }
        if (!_parserStorage.ContainsParser(parserId))
        {
            return OperationResult.Fail($"Парсер с id {parserId} не найден.");
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
        return OperationResult.Ok("Группы успешно установлены.");
    }


    public async Task<OperationResult> SubmitVerificationCodeFromTelegram(Guid parserId, int verificationCode)
    {
        if (!_parserStorage.ContainsParser(parserId))
            return OperationResult.Fail($"Парсер с Id {parserId} не найден.");

        _parserStorage.TryGetParser(parserId, out var parser);
        var client = parser.Client;
        var temp = _parserStorage.GetOrCreateTemporaryAuthData(parserId);
        temp.VerificationCode = verificationCode;

        var loginResult = await client.Login(parser.Phone);

        switch (loginResult)
        {
            case "password":
                parser.AuthState = TelegramAuthState.NeedPassword;
                return OperationResult.Fail("Нужен двухфакторный пароль. Отправьте его через /Auth/SendATwoFactorPassword");

            case null:
                parser.AuthState = TelegramAuthState.Authorized;
                client.OnUpdates += async update => await HandleUpdate(parserId, update);
                _parserStorage.TryRemoveTemporaryAuthData(parserId);
                return OperationResult.Ok("Успешно вошли в Telegram.");
        }
        return OperationResult.Fail("Неизвестный результат авторизации.");
    }


    public async Task<OperationResult> SubmitTwoFactorPassword(Guid parserId, int twofactorpassword)
    {
        if (!_parserStorage.ContainsParser(parserId))
        {
            return OperationResult.Fail($"Парсер с Id {parserId} не найден.");
        }
        _parserStorage.TryGetParser(parserId, out var parser);
        var client = parser.Client;
        var temp = _parserStorage.GetOrCreateTemporaryAuthData(parserId);
        temp.TwoFactorPassword = twofactorpassword;

        var loginResult = await client.Login(parser.Phone);

        switch (loginResult)
        {
            case "password":
                parser.AuthState = TelegramAuthState.NeedPassword;
                return OperationResult.Fail("Нужен двухфакторный пароль. Отправьте его через /Auth/SubmitTwoFactorPassword");

            case "verification_code":
                parser.AuthState = TelegramAuthState.NeedVerificationCode;
                return OperationResult.Fail("Необходим код верификации, который вам прислал Telegram. Отправьте его через /Auth/SendVerificationCodeFromTelegram");

            case null:
                parser.AuthState = TelegramAuthState.Authorized;
                client.OnUpdates += async update => await HandleUpdate(parserId, update);
                _parserStorage.TryRemoveTemporaryAuthData(parserId);
                return OperationResult.Ok("Успешно вошли в Telegram.");
        }

        return OperationResult.Fail("Неизвестный результат авторизации.");
    }

    public async Task<OperationResult> SetKeywordsFromText(Guid parserId, string text)
    {
        if (!_parserStorage.ContainsParser(parserId))
            return OperationResult.Fail($"Парсер с Id {parserId} не найден.");

        _parserStorage.TryGetParser(parserId, out var parser);
        var keywords = text.Split(" ", StringSplitOptions.RemoveEmptyEntries)
                           .Select(k => k.Trim().ToLower())
                           .Where(k => !string.IsNullOrWhiteSpace(k))
                           .ToArray();

        if (keywords.Length == 0)
            return OperationResult.Fail("Не удалось извлечь ключевые слова из текста.");

        parser.Keywords = keywords;
        return OperationResult.Ok("Ключевые слова успешно установлены.");
    }
    
    public async Task<OperationResult> EnterToSessionByKeyAndPassword(LoginToSessionDto logindata)
    {
        if (_parserStorage.TryGetParser(logindata.SessionKey, out var parser))
        {
            var existParserLogs = await _parserDatabase.ParserLogsTable.Where(x => x.ParserId == logindata.SessionKey).ToListAsync();
        }
        return OperationResult.Ok("fsdf");
    }
}
