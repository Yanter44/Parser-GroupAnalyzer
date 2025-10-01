using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MpParserAPI.Controllers;
using MpParserAPI.DbContext;
using MpParserAPI.Interfaces;
using MpParserAPI.Models;
using MpParserAPI.Utils;
using TL;

namespace MpParserAPI.Services
{
    public class UpdateHandler : IUpdateHandler
    {
        private readonly ILogger<UpdateHandler> _logger;
        private readonly IParserDataStorage _parserStorage;
        private readonly IDbContextFactory<ParserDbContext> _dbContextFactory;
        private readonly ICloudinaryService _cloudinaryService;
        private readonly MpParserAPI.Interfaces.IRedis _redisService;
        private readonly INotify _notificationService;
        private readonly IHubContext<ParserHub> _parserHubContext;
        public UpdateHandler(ILogger<UpdateHandler> logger,
            IParserDataStorage parserStorage,
            IDbContextFactory<ParserDbContext> dbContextFactory,
            ICloudinaryService cloudinaryService,
            IRedis redisService,
            INotify notificationService,
            IHubContext<ParserHub> parserHubContext)
        {
            _logger = logger;
            _parserStorage = parserStorage;
            _dbContextFactory = dbContextFactory;
            _cloudinaryService = cloudinaryService;
            _redisService = redisService;
            _notificationService = notificationService;
            _parserHubContext = parserHubContext;
        }
        public async Task HandleAsync(Guid parserId, UpdateData update)
        {
            if (!_parserStorage.ContainsParser(parserId))
                return;

            _parserStorage.TryGetParser(parserId, out var parser);
            _logger.LogInformation("Начали обработку сообщения {ParserId}", parserId);
            var parserData = parser;

            if (update.Update is UpdatesBase updates)
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
                                var messageText = msg.message ?? string.Empty;
                                var normalizedMessage = TextNormalizer.NormalizeText(messageText);

                                var keywords = parser.Keywords
                                    .Select(TextNormalizer.NormalizeText)
                                    .ToList();

                                bool isMatch = CheckForKeywordsOrPhrases(normalizedMessage, keywords);

                                if (isMatch)
                                {
                                    var userId = peerUser.user_id;
                                    var dialogs = await parserData.Client.Messages_GetAllDialogs();

                                    if (dialogs.users.TryGetValue(userId, out var user))
                                    {
                                        if (!string.IsNullOrEmpty(user.username) &&
                                            user.username.EndsWith("bot", StringComparison.OrdinalIgnoreCase))
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
                                                    await parserData.Client.DownloadProfilePhotoAsync(
                                                        user, userProfileImageBytes, true, true);
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
                                            else
                                            {
                                                imageUrl = existingTelegramUser.ProfileImageUrl;
                                            }

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

                                            var msgConvertedToHash = HashHelper.ComputeSha256Hash(normalizedMessage);
                                            var isexistSpamMessageInRedis =
                                                await _redisService.SetContainsAsync(parserId.ToString(), msgConvertedToHash);
                                            if (isexistSpamMessageInRedis) { return; }

                                            var parserlog = new ParserLogs
                                            {
                                                ParserId = parserId,
                                                TelegramUserId = existingTelegramUser.TelegramUserId,
                                                MessageText = normalizedMessage,
                                                CreatedAt = DateTime.UtcNow
                                            };

                                            database.ParserLogsTable.Add(parserlog);
                                            await database.SaveChangesAsync();

                                            string messageLink = null;
                                            if (!string.IsNullOrEmpty(groupUsername))
                                            {
                                                messageLink = $"https://t.me/{groupUsername}/{msg.id}";
                                            }

                                            _logger.LogInformation("Подходим к завершению обработки сообщения {ParserId}", parserId);
                                            await _notificationService.SendNotifyToBotAboutReceivedMessageAsync(parserId, $"🙍‍Пользователь: {existingTelegramUser.FirstName}\n\n💬Сообщение: {msg.message}\n\n👩‍👩‍👧‍👦Группа: {groupTitle}\n🔖Никнейм: @{user.username}", messageLink);
                                            _logger.LogInformation("отправили нотифай в бот {ParserId}", parserId);
                                            await _parserHubContext.Clients.Group(parserId.ToString()).SendAsync("ReceiveMessage", new
                                            {
                                                ProfileImageUrl = imageUrl,
                                                Name = user.first_name,
                                                Username = user.username,
                                                MessageText = messageText,
                                                MessageTime = parserlog.CreatedAt.ToString("o"),
                                                MessageLink = messageLink
                                            });

                                            _logger.LogInformation("""
                                                                   Для парсера {ParserId} пришло сообщение:
                                                                   Пользователь: {UserId} ({FirstName} {LastName})
                                                                   Никнейм: @{Username}
                                                                   Телефон: {Phone}
                                                                   Группа: {GroupTitle}
                                                                   Сообщение: {Message}
                                                                   """, parserId, user.id, user.first_name, user.last_name,
                                                                   user.username, user.phone, groupTitle, messageText);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private bool CheckForKeywordsOrPhrases(string messageText, List<string> keywords)
        {
            if (string.IsNullOrEmpty(messageText) || keywords == null || keywords.Count == 0)
                return false;

            var wordsInMessage = messageText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var keyword in keywords)
            {
                if (string.IsNullOrWhiteSpace(keyword))
                    continue;

                if (keyword.Contains(' '))
                {
                    if (messageText.Contains(keyword))
                        return true;
                }
                else
                {
                    if (wordsInMessage.Contains(keyword))
                        return true;
                }
            }

            return false;
        }

    }
}
