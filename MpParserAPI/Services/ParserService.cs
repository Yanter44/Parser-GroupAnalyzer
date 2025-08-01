using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MpParserAPI.Common;
using MpParserAPI.Controllers;
using MpParserAPI.DbContext;
using MpParserAPI.Interfaces;
using MpParserAPI.Models;
using MpParserAPI.Models.Dtos;
using MpParserAPI.Utils;
using TL;

public class ParserService : IParser
{
    private readonly ICloudinaryService _cloudinaryService;
    private readonly IGenerator _generatorService;
    private readonly IDbContextFactory<ParserDbContext> _dbContextFactory;
    private readonly IParserDataStorage _parserStorage;
    private readonly IHubContext<ParserHub> _parserHubContext;
    private readonly ILogger<ParserService> _logger;
    private readonly INotify _notificationService;
    private readonly MpParserAPI.Interfaces.IRedis _redisService;

    private static readonly TimeZoneInfo MoscowTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time");
    public ParserService(ICloudinaryService cloudinaryService,
                         IGenerator generator,
                         IDbContextFactory<ParserDbContext> dbContextFactory,
                         IParserDataStorage parserStorage,
                         IHubContext<ParserHub> parserhubContext,
                         ILogger<ParserService> logger,
                         INotify notificationService,
                         MpParserAPI.Interfaces.IRedis redisService)
    {
        _cloudinaryService = cloudinaryService;
        _generatorService = generator;
        _dbContextFactory = dbContextFactory;
        _parserStorage = parserStorage;
        _parserHubContext = parserhubContext;
        _logger = logger;
        _notificationService = notificationService;
        _redisService = redisService;
    }


    private static string FormatToMoscowTime(DateTime utcDateTime)
    {
        var moscowTime = TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, MoscowTimeZone);
        return moscowTime.ToString("HH:mm");
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
                                    if (!string.IsNullOrEmpty(user.username) && user.username.EndsWith("bot", StringComparison.OrdinalIgnoreCase))                                    
                                        continue;

                                    string groupTitle = "Неизвестная группа";
                                    string groupUsername = "";

                                    if (dialogs.chats.TryGetValue(msg.peer_id.ID, out var chatBase))
                                    {
                                        switch (chatBase)
                                        {
                                            case Chat chat:
                                                groupUsername = chat.MainUsername;
                                                groupTitle = chat.title;
                                                break;
                                            case Channel channel:
                                                groupUsername = channel.username;
                                                groupTitle = channel.title;
                                                break;
                                        }


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
                                        await database.SaveChangesAsync();
                                        var msgConvertedToHash = HashHelper.ComputeSha256Hash(msg.message);
                                        var isexistSpamMessageInRedis = await _redisService.SetContainsAsync(parserId.ToString(), msgConvertedToHash);
                                        if (isexistSpamMessageInRedis) { return; }
                                        
                                        var parserlog = new ParserLogs
                                        {
                                            ParserId = parserId,
                                            TelegramUserId = existingTelegramUser.TelegramUserId,
                                            MessageText = msg.message,
                                        };

                                        database.ParserLogsTable.Add(parserlog);
                                        await database.SaveChangesAsync();

                                        string messageLink = null;
                                        if (!string.IsNullOrEmpty(groupUsername))
                                        {
                                            messageLink = $"https://t.me/{groupUsername}/{msg.id}";
                                        }

                                      //  await _notificationService.SendNotifyToBotAboutReceivedMessageAsync(parserId, $"Пользователь: {existingTelegramUser.FirstName}\n\nСообщение: {msg.message}\n\nГруппа: {groupTitle}\nUsername: @{user.username}", messageLink);

                                        var formattedTime = FormatToMoscowTime(parserlog.CreatedAt);

                                        await _parserHubContext.Clients.Group(parserId.ToString()).SendAsync("ReceiveMessage", new
                                        {
                                            ProfileImageUrl = imageUrl,
                                            Name = user.first_name,
                                            Username = user.username,
                                            MessageText = msg.message,
                                            MessageTime = formattedTime

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
    }
  
    public async Task<OperationResult<object>> SetGroupsNames(Guid parserId, IEnumerable<string> groupNames)
    {
        if (!_parserStorage.ContainsParser(parserId))
        {
            return OperationResult<object>.Fail($"Парсер с id {parserId} не найден.");
        }

        _parserStorage.TryGetParser(parserId, out var parser);

        if (groupNames == null || !groupNames.Any())
        {
            parser.TargetGroups.Clear();
            parser.TargetGroupTitles.Clear();

            return OperationResult<object>.Ok("Группы очищены.");
        }

        var client = parser.Client;
        var dialogs = await client.Messages_GetAllDialogs();
        var newGroups = new List<InputPeer>();
        var groupReferences = new List<GroupReference>();
        var groupTitles = new List<string>();

        foreach (var groupName in groupNames)
        {
            var group = dialogs.chats.Values.OfType<ChatBase>().FirstOrDefault(c => c.Title == groupName);
            if (group != null)
            {
                var inputPeer = group.ToInputPeer();
                if (inputPeer is InputPeerChannel inputPeerChannel)
                {
                    newGroups.Add(inputPeer);
                    groupTitles.Add(group.Title);

                    groupReferences.Add(new GroupReference
                    {
                        ChatId = inputPeerChannel.channel_id,
                        AccessHash = inputPeerChannel.access_hash,
                        Title = group.Title
                    });
                }
            }
        }

        parser.TargetGroups.Clear();
        parser.TargetGroups.AddRange(newGroups);

        parser.TargetGroupTitles.Clear();
        parser.TargetGroupTitles.AddRange(groupTitles);
        using var database = await _dbContextFactory.CreateDbContextAsync();

        var existParser = await database.ParsersStates
            .AsTracking()
            .FirstOrDefaultAsync(x => x.ParserId == parserId);

        if (existParser == null)
        {
            existParser = new ParserStateTable
            {
                ParserId = parserId,
                TargetGroups = new List<GroupReference>()
            };
            database.ParsersStates.Add(existParser);
        }

        existParser.TargetGroups = groupReferences;
        await database.SaveChangesAsync();
        return OperationResult<object>.Ok("Группы успешно установлены.");
    }



    public async Task<OperationResult<object>> SetKeywords(Guid parserId, List<string> keywordss)
    {
        if (!_parserStorage.ContainsParser(parserId))
            return OperationResult<object>.Fail($"Парсер с Id {parserId} не найден.");

        _parserStorage.TryGetParser(parserId, out var parser);

        if (keywordss == null || !keywordss.Any())
        {
            parser.Keywords = Array.Empty<string>();
            return OperationResult<object>.Ok("Ключевые слова очищены.");
        }

        var keywords = keywordss
                        .Select(k => k.Trim().ToLower())
                        .Where(k => !string.IsNullOrWhiteSpace(k))
                        .ToArray();

        parser.Keywords = keywords;
        using var database = await _dbContextFactory.CreateDbContextAsync();
        var existparser = await database.ParsersStates.FirstOrDefaultAsync(x => x.ParserId == parserId);
        if(existparser != null)
        {
            existparser.Keywords = keywords;
        }
        await database.SaveChangesAsync();
        return OperationResult<object>.Ok("Ключевые слова успешно установлены.");
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
        var usergroupsList = new List<string>();

        var gettedDataAboutUserGroups = await parser.Client.Messages_GetAllDialogs();

        foreach (var chatEntry in gettedDataAboutUserGroups.chats)
        {
            var chat = chatEntry.Value;
            string title = chat switch
            {
                Chat group => group.Title,
                Channel channel when !channel.flags.HasFlag(Channel.Flags.broadcast) => channel.Title,
                _ => null
            };

            if (title != null && !usergroupsList.Contains(title))
                usergroupsList.Add(title);
        }

        var spamWords = await db.ParsersStates
            .Where(x => x.ParserId == parserId)
            .Select(x => x.SpamWords)
            .FirstOrDefaultAsync();

        var parserLogsRaw = await db.ParserLogsTable
            .Where(x => x.ParserId == parserId)
            .Where(x => spamWords == null || !spamWords.Any(sw => x.MessageText == sw))
            .Include(x => x.TelegramUser)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new ParserLogsResponceDto
            {
                MessageText = x.MessageText,
                FirstName = x.TelegramUser.FirstName,
                Username = x.TelegramUser.Username,
                ProfileImageUrl = x.TelegramUser.ProfileImageUrl,
                MessageTime = x.CreatedAt.ToLocalTime().ToString("HH:mm"),


            })
            .ToListAsync();

        var remainingTime = parser.GetRemainingParsingTime() ?? TimeSpan.Zero;

        var response = new GetParserStateResponceDto
        {
            parserDataResponceDto = new ParserDataResponceDto
            {
                IsParsingStarted = parser.IsParsingStarted,
                Parserkeywords = parser.Keywords,
                ProfileImageUrl = profileImageUrl,
                ProfileNickName = profileNickName,
                TargetGroups = parser.TargetGroupTitles.ToArray() ?? Array.Empty<string>(),
                ParserId = parser.Id,
                ParserPassword = parser.Password,
                UserGroupsList = usergroupsList,
                RemainingParsingTimeHoursMinutes = remainingTime.ToString(@"hh\:mm\:ss") ?? "00:00:00",
                TotalParsingMinutes = parser.TotalParsingMinutes?.ToString(@"hh\:mm\:ss") ?? "00:00:00"
            },
            parserLogs = parserLogsRaw
        };

        return response;
    }
    public async Task StartParsing(Guid parserId)
    {
        if (_parserStorage.TryGetParser(parserId, out var parser) && parser.Client != null)
        {
            Func<IObject, Task> handler = async update => await HandleUpdate(parserId, update);
            parser.Client.OnUpdates += handler;
            _parserStorage.AddHandler(parserId, handler);
            parser.IsParsingStarted = true;
            parser.ParsingStartedAt = DateTime.UtcNow;
         
            if (parser.ParsingDelay.HasValue)
            {
                parser.ParsingTimer = new Timer(async _ =>
                {
                    await StopParsing(parserId);
              //      await _notificationService.SendSimpleNotify(parserId, "Парсинг автоматически остановлен по таймеру");
                    _logger.LogInformation($"Парсинг автоматически остановлен по таймеру для {parserId}");
                },
                null,
                parser.ParsingDelay.Value,
                Timeout.InfiniteTimeSpan);
            }
            using var databse = await _dbContextFactory.CreateDbContextAsync();
            var existparserProfile = databse.ParsersStates.FirstOrDefault(x => x.ParserId == parserId);
            if(existparserProfile != null)
            {
               var spammessages =  existparserProfile.SpamWords.ToList();
                foreach (var message in spammessages)
                {
                    await _redisService.SetAddAsync(parserId.ToString(), message);
                }
           
            }
        }
    }

    public async Task StopParsing(Guid parserId)
    {
        if (_parserStorage.TryGetParser(parserId, out var parser) &&
            parser.Client != null &&
            _parserStorage.TryGetHandler(parserId, out var handler))
        {
            parser.Client.OnUpdates -= handler;
            _parserStorage.RemoveHandler(parserId);
            parser.IsParsingStarted = false;

            if (parser.ParsingStartedAt.HasValue)
            {
                var elapsed = DateTime.UtcNow - parser.ParsingStartedAt.Value;
                if (elapsed > TimeSpan.Zero)
                {
                    parser.TotalParsingMinutes ??= TimeSpan.Zero;
                    parser.TotalParsingMinutes -= elapsed;
                }
            }

            parser.ParsingStartedAt = null;
            parser.ParsingTimer?.Dispose();
            parser.ParsingTimer = null;
            parser.ParsingDelay = null;

            using var database = await _dbContextFactory.CreateDbContextAsync();
            var parserState = await database.ParsersStates.FirstOrDefaultAsync(x => x.ParserId == parserId);
            if (parserState != null)
            {
                parserState.TotalParsingMinutes = parser.TotalParsingMinutes;
                await database.SaveChangesAsync();
            }

            await _redisService.DeleteKeyAsync(parserId.ToString());
            await _parserHubContext.Clients.Group(parserId.ToString()).SendAsync("ParsingIsStoped");
        }
    }


    public async Task DisposeParser(Guid parserId)
    {
        if (_parserStorage.TryGetParser(parserId, out var parser))
        {
            parser.DisposeData();
            _parserStorage.TryRemoveParser(parserId);
            await Task.CompletedTask;
        }
    }

    public Task<OperationResult<object>> AddTimeParsing(Guid parserId, TimeParsingDto dto)
    {
        if (!_parserStorage.TryGetParser(parserId, out var parser))
            return Task.FromResult(OperationResult<object>.Fail($"Парсер с id {parserId} не найден."));

        var newDelay = TimeSpan.FromHours(dto.Hours) + TimeSpan.FromMinutes(dto.Minutes);

        if (parser.TotalParsingMinutes.HasValue && newDelay > parser.TotalParsingMinutes.Value)
        {
            return Task.FromResult(OperationResult<object>.Fail(
                $"Нельзя установить время больше чем общее время парсинга. Допустимо максимум: {parser.TotalParsingMinutes.Value.TotalMinutes} минут."));
        }
        parser.ParsingTimer?.Dispose();
        parser.ParsingTimer = null;

        if (newDelay > TimeSpan.Zero)
        {
            parser.ParsingDelay = newDelay;
            parser.ParsingStartedAt = DateTime.UtcNow;
        }
        else
        {
            parser.ParsingDelay = null;
            parser.ParsingStartedAt = null;
        }

        return Task.FromResult(OperationResult<object>.Ok(
            parser.ParsingDelay.HasValue
                ? $"Время парсинга для парсера {parserId} настроено на {parser.ParsingDelay.Value.TotalMinutes} минут."
                : "Таймер парсинга сброшен."));
    }


    public GetParsingRemainTimeResponceDto GetParserRemainTime(Guid parserId)
    {
        if (_parserStorage.TryGetParser(parserId, out var parser))
        {
            var remainingTime = parser.GetRemainingParsingTime() ?? TimeSpan.Zero;
            return new GetParsingRemainTimeResponceDto()
            {
                RemainingParsingTimeHoursMinutes = remainingTime.ToString(@"hh\:mm\:ss") ?? "00:00:00"
            };
        }
        return null;
    }

    public async Task<OperationResult<object>> AddNewSpamMessage(Guid parserId, AddNewSpamMessageDto modelDto)
    {
        if (!_parserStorage.TryGetParser(parserId, out var parser))
            return OperationResult<object>.Fail("Не удалось найти parser");

        await using var database = _dbContextFactory.CreateDbContext();
        var existParser = await database.ParsersStates.FirstOrDefaultAsync(x => x.ParserId == parserId);
        if (existParser == null)
            return OperationResult<object>.Fail("Не удалось найти существующий parser");

        if (existParser.SpamWords?.Contains(modelDto.Message) == true)
            return OperationResult<object>.Fail("Сообщение уже есть в черном списке");

        existParser.SpamWords ??= new List<string>();

        existParser.SpamWords.Add(modelDto.Message);

        database.Entry(existParser).Property(x => x.SpamWords).IsModified = true;

        string hash = HashHelper.ComputeSha256Hash(modelDto.Message);
        string redisKey = parserId.ToString();
        await _redisService.SetAddAsync(redisKey, hash);

        await database.SaveChangesAsync();

        return OperationResult<object>.Ok("Сообщение успешно добавлено в черный список");
    }


}
