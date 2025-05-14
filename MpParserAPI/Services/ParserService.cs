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



    public void StopParser(Guid clientId)
    {
        if (All_ClientsData.TryGetValue(clientId, out var client))
        {
            client.DisposeData();
            All_ClientsData.TryRemove(clientId, out _);
        }
    }

    private string Config(string what, Guid clientId)
    {
        All_ClientsData.TryGetValue(clientId, out var parserData);
        TempAuthData.TryGetValue(clientId, out var tempData);

        return what switch
        {
            "api_id" => parserData?.ApiId,
            "api_hash" => parserData?.ApiHash,
            "phone_number" => parserData?.Phone,
            "verification_code" => tempData?.VerificationCode?.ToString(),
            "password" => tempData?.TwoFactorPassword.ToString(),
            _ => null
        };
    }


    private async Task HandleUpdate(Guid parserId, IObject updateObj)
    {
        if (!All_ClientsData.ContainsKey(parserId))
            return;

        var clientData = All_ClientsData[parserId];
        var clientPhone = clientData.Phone;

        if (updateObj is UpdatesBase updates)
        {
            foreach (var upd in updates.UpdateList)
            {
                if (upd is UpdateNewMessage unm && unm.message is Message msg)
                {
                    if (All_ClientsData.ContainsKey(parserId) &&
                        All_ClientsData[parserId].TargetGroups.Any(peer => msg.peer_id.ID == peer.ID))
                    {
                        if (msg.from_id is PeerUser peerUser)
                        {
                            var wordsInMessage = Regex.Split(msg.message.ToLower(), @"\W+");
                            var keywords = All_ClientsData[parserId].Keywords;

                            if (keywords.Any(kw => wordsInMessage.Contains(kw.Trim().ToLower())))
                            {
                                var userId = peerUser.user_id;
                                var dialogs = await clientData.Client.Messages_GetAllDialogs();

                                if (dialogs.users.TryGetValue(userId, out var user))
                                {
                                    var userPhotoId = user.photo?.photo_id;

                                    var existingTelegramUser = await _parserDatabase.TelegramUsers
                                        .FirstOrDefaultAsync(x => x.TelegramUserId == userId);

                                    string imageUrl = null;

                                    if (existingTelegramUser == null || existingTelegramUser.ProfilePhotoId != userPhotoId)
                                    {
                                        var userProfileImageBytes = new MemoryStream();
                                        await clientData.Client.DownloadProfilePhotoAsync(user, userProfileImageBytes, true, true);
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
        if (!All_ClientsData.ContainsKey(parserId))
        {
            return OperationResult.Fail($"Парсер с id {parserId} не найден.");
        }

        var client = All_ClientsData[parserId].Client;

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

        if (All_ClientsData.ContainsKey(parserId))
        {
            All_ClientsData[parserId].TargetGroups.Clear();
            All_ClientsData[parserId].TargetGroups.AddRange(newGroups);
        }
        else
        {
            All_ClientsData[parserId].TargetGroups = newGroups;
        }
        return OperationResult.Ok("Группы успешно установлены.");
    }


    public async Task<OperationResult> SubmitVerificationCodeFromTelegram(Guid parserId, int verificationCode)
    {
        if (!All_ClientsData.ContainsKey(parserId))
            return OperationResult.Fail($"Парсер с Id {parserId} не найден.");

        var client = All_ClientsData[parserId].Client;

        var temp = TempAuthData.GetOrAdd(parserId, _ => new());
        temp.VerificationCode = verificationCode;

        var loginResult = await client.Login(All_ClientsData[parserId].Phone);

        switch (loginResult)
        {
            case "password":
                All_ClientsData[parserId].AuthState = TelegramAuthState.NeedPassword;
                return OperationResult.Fail("Нужен двухфакторный пароль. Отправьте его через /Auth/SendATwoFactorPassword");

            case null:
                All_ClientsData[parserId].AuthState = TelegramAuthState.Authorized;
                client.OnUpdates += async update => await HandleUpdate(parserId, update);
                TempAuthData.TryRemove(parserId, out _);
                return OperationResult.Ok("Успешно вошли в Telegram.");
        }
        return OperationResult.Fail("Неизвестный результат авторизации.");
    }


    public async Task<OperationResult> SubmitTwoFactorPassword(Guid parserId, int twofactorpassword)
    {
        if (!All_ClientsData.ContainsKey(parserId))
        {
            return OperationResult.Fail($"Парсер с Id {parserId} не найден.");
        }
        var client = All_ClientsData[parserId].Client;
        if (!All_ClientsData.ContainsKey(parserId))
        {
            return OperationResult.Fail("Клиент не авторизован.");
        }
        var temp = TempAuthData.GetOrAdd(parserId, _ => new());
        temp.TwoFactorPassword = twofactorpassword;

        var loginResult = await client.Login(All_ClientsData[parserId].Phone);

        switch (loginResult)
        {
            case "password":
                All_ClientsData[parserId].AuthState = TelegramAuthState.NeedPassword;
                return OperationResult.Fail("Нужен двухфакторный пароль. Отправьте его через /Auth/SubmitTwoFactorPassword");

            case "verification_code":
                All_ClientsData[parserId].AuthState = TelegramAuthState.NeedVerificationCode;
                return OperationResult.Fail("Необходим код верификации, который вам прислал Telegram. Отправьте его через /Auth/SendVerificationCodeFromTelegram");

            case null:
                All_ClientsData[parserId].AuthState = TelegramAuthState.Authorized;
                client.OnUpdates += async update => await HandleUpdate(parserId, update);
                TempAuthData.TryRemove(parserId, out _);
                return OperationResult.Ok("Успешно вошли в Telegram.");
        }

        return OperationResult.Fail("Неизвестный результат авторизации.");
    }

    public async Task<OperationResult> SetKeywordsFromText(Guid parserId, string text)
    {
        if (!All_ClientsData.ContainsKey(parserId))
            return OperationResult.Fail($"Парсер с Id {parserId} не найден.");

        var keywords = text.Split(" ", StringSplitOptions.RemoveEmptyEntries)
                           .Select(k => k.Trim().ToLower())
                           .Where(k => !string.IsNullOrWhiteSpace(k))
                           .ToArray();

        if (keywords.Length == 0)
            return OperationResult.Fail("Не удалось извлечь ключевые слова из текста.");

        All_ClientsData[parserId].Keywords = keywords;
        return OperationResult.Ok("Ключевые слова успешно установлены.");
    }
    
    public async Task<OperationResult> EnterToSessionByKeyAndPassword(LoginToSessionDto logindata)
    {
        if (All_ClientsData.TryGetValue(logindata.SessionKey, out var parser))
        {
            var existParserLogs = await _parserDatabase.ParserLogsTable.Where(x => x.ParserId == logindata.SessionKey).ToListAsync();
        }
        return OperationResult.Ok("fsdf");
    }
}
