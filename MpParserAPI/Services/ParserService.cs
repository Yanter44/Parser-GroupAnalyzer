using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MpParserAPI.Common;
using MpParserAPI.Controllers;
using MpParserAPI.DbContext;
using MpParserAPI.Enums;
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
    private readonly ISubscriptionManager _subscriptionManager;
    private readonly IMessageQueueService _messageQueueService;
    public ParserService(ICloudinaryService cloudinaryService,
                         IGenerator generator,
                         IDbContextFactory<ParserDbContext> dbContextFactory,
                         IParserDataStorage parserStorage,
                         IHubContext<ParserHub> parserhubContext,
                         ILogger<ParserService> logger,
                         INotify notificationService,
                         MpParserAPI.Interfaces.IRedis redisService,
                         ISubscriptionManager subscriptionManager, 
                         IMessageQueueService messageQueueService)
    {
        _cloudinaryService = cloudinaryService;
        _generatorService = generator;
        _dbContextFactory = dbContextFactory;
        _parserStorage = parserStorage;
        _parserHubContext = parserhubContext;
        _logger = logger;
        _notificationService = notificationService;
        _redisService = redisService;
        _subscriptionManager = subscriptionManager;
        _messageQueueService = messageQueueService;
    }

   
    private async Task HandleUpdate(Guid parserId, IObject updateObj)

    {
        if (!_parserStorage.ContainsParser(parserId))
            return;

        if (updateObj is UpdatesBase updates)
        {
            foreach (var upd in updates.UpdateList)
            {
                if (upd is UpdateNewMessage unm && unm.message is Message msg)
                {
                    if (_parserStorage.ContainsParser(parserId))
                    {
                        await _messageQueueService.EnqueueMessageAsync(parserId, updates, _parserStorage.GetParser(parserId));
                        _logger.LogInformation("Добавлено сообщение в очередь парсера {ParserId}", parserId);
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
                else if (inputPeer is InputPeerChat inputPeerChat)
                {
                    newGroups.Add(inputPeer);
                    groupTitles.Add(group.Title);

                    groupReferences.Add(new GroupReference
                    {
                        ChatId = inputPeerChat.chat_id,
                        AccessHash = 0, 
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

        var TotalParsingTime = _subscriptionManager.GetTotalParsingTime(parser);
        var AvailableParsingTime = _subscriptionManager.GetRemainingParsingTime(parserId);

        var formattedTotalParsingTime = TimeFormatterHelper.ToHumanReadableString(TotalParsingTime);
        var formattedAvailableParsingTime = TimeFormatterHelper.ToHumanReadableStringThisSeconds(AvailableParsingTime);
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
                RemainingParsingTimeHoursMinutes = formattedAvailableParsingTime,
                TotalParsingTime = formattedTotalParsingTime
            },
            parserLogs = parserLogsRaw
        };

        return response;
    }
    public async Task<OperationResult<object>> StartParsing(Guid parserId)
    {

        if (!_parserStorage.TryGetParser(parserId, out var parser) || parser.Client == null)
            return OperationResult<object>.Fail("Не удалось найти клиента или парсер");

        if (parser.IsParsingStarted)
        {
            _logger.LogWarning("Парсинг уже запущен для {ParserId}", parserId);
              return OperationResult<object>.Fail($"Парсинг уже запущен для {parserId}");
        }
        if (!_subscriptionManager.CanStartParsing(parser, out var allowedDuration))
        {
            _logger.LogWarning("Недостаточно времени подписки для парсера {ParserId}", parserId);
              return OperationResult<object>.Fail($"Недостаточно времени подписки для парсера {parserId}");
        }

        Func<IObject, Task> handler = async update => await HandleUpdate(parserId, update);
        parser.Client.OnUpdates += handler;
        _parserStorage.AddHandler(parserId, handler);

        parser.IsParsingStarted = true;
        parser.ParsingStartedAt = DateTime.UtcNow;
        parser.ParsingDelay = allowedDuration; 

        parser.ParsingTimer = new Timer(async _ =>
        {
            await StopParsing(parserId);
            _logger.LogInformation("Парсинг автоматически остановлен по таймеру для {ParserId}", parserId);
        },
        null,
        allowedDuration, 
        Timeout.InfiniteTimeSpan);


        using var database = await _dbContextFactory.CreateDbContextAsync();
        var parserProfile = database.ParsersStates.FirstOrDefault(x => x.ParserId == parserId);

        var spamWords = parserProfile?.SpamWords?.ToArray();
        if (spamWords?.Length > 0)
        {
            var spamHashes = spamWords.Select(HashHelper.ComputeSha256Hash).ToArray();
            await _redisService.SetAddRangeAsync(parserId.ToString(), spamHashes);
        }
        return OperationResult<object>.Ok($"Парсинг был успешно запущен для {parserId}");
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
                    parser.TotalParsingTime -= elapsed;
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
                parserState.TotalParsingTime = parser.TotalParsingTime;
                await database.SaveChangesAsync();
            }

            await _redisService.DeleteKeyAsync(parserId.ToString());

            var TotalParsingTime = _subscriptionManager.GetTotalParsingTime(parser);
            var formattedTotalParsingTime = TimeFormatterHelper.ToHumanReadableString(TotalParsingTime);

            await _parserHubContext.Clients.Group(parserId.ToString()).SendAsync("ParsingIsStoped", new
            {
                TotalParsingTime = formattedTotalParsingTime
            });

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

    public async Task<OperationResult<object>> AddNewSpamMessage(Guid parserId, AddNewSpamMessageDto modelDto)
    {
        try
        {
            if (!_parserStorage.TryGetParser(parserId, out var parser))
                return OperationResult<object>.Fail("Не удалось найти parser");

            await using var database = _dbContextFactory.CreateDbContext();
            var existParser = await database.ParsersStates.FirstOrDefaultAsync(x => x.ParserId == parserId);
            if (existParser == null)
                return OperationResult<object>.Fail("Не удалось найти существующий parser");

            var logsToRemove = await database.ParserLogsTable
                .Where(x => x.ParserId == parserId && x.MessageText == modelDto.Message)
                .ToListAsync();

            if (logsToRemove.Any())
            {
                database.ParserLogsTable.RemoveRange(logsToRemove);
                _logger.LogInformation(
                    "Удалены старые записи для парсера {Parser}. Сообщения: {Messages}",
                    parserId,
                    string.Join(", ", logsToRemove.Select(l => l.MessageText))
                );
            }

            if (existParser.SpamWords?.Contains(modelDto.Message) == true)
                return OperationResult<object>.Fail("Сообщение уже есть в черном списке");

            existParser.SpamWords ??= new List<string>();
            existParser.SpamWords.Add(modelDto.Message);
            database.Entry(existParser).Property(x => x.SpamWords).IsModified = true;

            string redisKey = parserId.ToString();
            string hash = HashHelper.ComputeSha256Hash(modelDto.Message);
            await _redisService.SetAddAsync(redisKey, hash);

            _logger.LogInformation("Сообщение: {Message}, Hash: {Hash} добавлены в Redis под ключом {Key}",
                modelDto.Message, hash, redisKey);

            await database.SaveChangesAsync();

            return OperationResult<object>.Ok("Сообщение успешно добавлено в черный список");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при добавлении сообщения в черный список для парсера {ParserId}", parserId);
            return OperationResult<object>.Fail("Произошла ошибка при добавлении сообщения в черный список");
        }
    }


}
