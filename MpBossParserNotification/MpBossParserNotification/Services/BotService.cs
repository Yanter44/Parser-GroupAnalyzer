using MpBossParserNotification.Interfaces;
using MpBossParserNotification.Models;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace MpBossParserNotification.Services
{
    public class BotService : IBotService
    {
        private readonly TelegramBotClient _botClient;
        private readonly ParserSubscriptionStorage _storage;
        private readonly HttpClient _httpClient;

        public BotService(
            TelegramBotClient botClient,
            ParserSubscriptionStorage storage,
            HttpClient httpClient)
        {
            _botClient = botClient;
            _storage = storage;
            _httpClient = httpClient;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = { }
            };

            _botClient.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                receiverOptions,
                cancellationToken
            );

            Console.WriteLine("Telegram bot started...");
            return Task.CompletedTask;
        }

        private async Task HandleUpdateAsync(
            ITelegramBotClient bot,
            Update update,
            CancellationToken token)
        {
            if (update.Message is not { } message || message.Text is null)
                return;

            var chatId = message.Chat.Id;
            var text = message.Text.Trim();

            try
            {
                await HandleMessageAsync(bot, chatId, text, token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Ошибка обработки сообщения: {ex.Message}");
                await bot.SendMessage(chatId, "⚠️ Произошла ошибка при обработке команды");
            }
        }

        private async Task HandleMessageAsync(
            ITelegramBotClient bot,
            long chatId,
            string text,
            CancellationToken token)
        {
            switch (text)
            {
                case string s when s.StartsWith("/start"):
                    await HandleStartCommandAsync(bot, chatId);
                    break;

                case "Подписаться на парсер✍️":
                    await HandleSubscribeCommandAsync(bot, chatId);
                    break;

                case "Отменить текущую подписку🛑":
                    await HandleUnsubscribeCommandAsync(bot, chatId);
                    break;

                case string s when s.Contains(' '):
                    await HandleCredentialsInputAsync(bot, chatId, text);
                    break;

                default:
                    await HandleUnknownCommandAsync(bot, chatId);
                    break;
            }
        }

        private async Task HandleStartCommandAsync(ITelegramBotClient bot, long chatId)
        {
            var isSubscribed = _storage.IsUserSubscribedToParser(chatId);

            if (isSubscribed)
            {
                await ShowSubscribedMenu(bot, chatId, isWelcome: true);
            }
            else
            {
                await ShowUnsubscribedMenu(bot, chatId, isWelcome: true);
            }
        }

        private async Task HandleSubscribeCommandAsync(ITelegramBotClient bot, long chatId)
        {
            await bot.SendMessage(
                chatId,
                "Введите parserId и пароль через пробел\n\nПример: `abc123-def456 password123`",
                parseMode: ParseMode.Markdown
            );
        }

        private async Task HandleUnsubscribeCommandAsync(ITelegramBotClient bot, long chatId)
        {
            var success = _storage.RemoveSubscriptionByChatId(chatId);

            if (success)
            {
                await bot.SendMessage(chatId, "✅ Ваша подписка отменена");
                await ShowUnsubscribedMenu(bot, chatId, isWelcome: false);
            }
            else
            {
                await bot.SendMessage(chatId, "⚠️ У вас нет активной подписки");
            }
        }

        private async Task HandleCredentialsInputAsync(
            ITelegramBotClient bot,
            long chatId,
            string text)
        {
            var parts = text.Split(' ', 2);
            if (parts.Length < 2)
            {
                await bot.SendMessage(
                    chatId,
                    "❌ Неверный формат. Введите parserId и пароль через пробел\n\nПример: `abc123-def456 password123`",
                    parseMode: ParseMode.Markdown
                );
                return;
            }

            var parserId = parts[0];
            var password = parts[1];

            var isValid = await ValidateParserCredentialsAsync(parserId, password);

            if (isValid)
            {
                _storage.SaveSubscription(parserId, chatId);
                await bot.SendMessage(chatId, $"✅ Вы успешно подписаны как *{parserId}*",
                    parseMode: ParseMode.Markdown);
                await ShowSubscribedMenu(bot, chatId, isWelcome: false);
            }
            else
            {
                await bot.SendMessage(chatId, "❌ Неверный parserId или пароль");
            }
        }

        private async Task ShowUnsubscribedMenu(ITelegramBotClient bot, long chatId, bool isWelcome)
        {
            var replyKeyboard = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "Подписаться на парсер✍️" }
            })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = false
            };

            var message = isWelcome
                ? "👋 Привет! Я бот для уведомлений от MP Parser\n\nВыберите действие:"
                : "📋 Главное меню. Вы можете подписаться на уведомления:";

            await bot.SendMessage(
                chatId,
                message,
                replyMarkup: replyKeyboard
            );
        }

        private async Task ShowSubscribedMenu(ITelegramBotClient bot, long chatId, bool isWelcome)
        {
            var replyKeyboard = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "Отменить текущую подписку🛑" }
            })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = false
            };

            var message = isWelcome
                ? "👋 Привет! Вы уже подписаны на уведомления от MP Parser\n\nВы можете отписаться в любой момент:"
                : "✅ Вы подписаны на уведомления. Вы можете отписаться в любой момент:";

            await bot.SendMessage(
                chatId,
                message,
                replyMarkup: replyKeyboard
            );
        }

        private async Task HandleUnknownCommandAsync(ITelegramBotClient bot, long chatId)
        {
            var isSubscribed = _storage.IsUserSubscribedToParser(chatId);

            if (isSubscribed)
            {
                await ShowSubscribedMenu(bot, chatId, isWelcome: false);
            }
            else
            {
                await ShowUnsubscribedMenu(bot, chatId, isWelcome: false);
            }
        }

        private Task HandleErrorAsync(
            ITelegramBotClient bot,
            Exception exception,
            CancellationToken token)
        {
            Console.WriteLine($"[ERROR] {exception.Message}");
            return Task.CompletedTask;
        }

        private async Task<bool> ValidateParserCredentialsAsync(string parserId, string password)
        {
            try
            {
                var requestData = new
                {
                    ParserId = parserId,
                    Password = password
                };

                var response = await _httpClient.PostAsJsonAsync(
                    "http://backend:9090/Notification/ValidateParserIdAndPassword",
                    requestData
                );

                if (!response.IsSuccessStatusCode)
                    return false;

                var result = await response.Content.ReadFromJsonAsync<ValidateResponse>();
                return result?.IsValid ?? false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Ошибка валидации: {ex.Message}");
                return false;
            }
        }

        public async Task NotifyAsync(string parserId, string message, string messageLink)
        {
            if (!_storage.TryGetChatId(parserId, out var chatId))
                return;

            try
            {
                var inlineKeyboard = new InlineKeyboardMarkup(new[]
                {
                    InlineKeyboardButton.WithUrl("📩 Перейти к сообщению", messageLink)
                });

                await _botClient.SendMessage(
                    chatId: chatId,
                    text: $"🔔 *Новое уведомление:*\n\n{message}",
                    replyMarkup: inlineKeyboard,
                    parseMode: ParseMode.Markdown
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Ошибка отправки уведомления: {ex.Message}");
            }
        }
    }
}