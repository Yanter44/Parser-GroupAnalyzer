using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MpBossParserNotification.Interfaces;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot;
using System.Net.Http;
using MpBossParserNotification.Models;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.Enums;

namespace MpBossParserNotification.Services
{
    public class BotService : IBotService
    {
        private readonly TelegramBotClient _botClient;
        private readonly ParserSubscriptionStorage _storage;
        private readonly HttpClient _httpClient;
        public BotService(TelegramBotClient botClient, ParserSubscriptionStorage storage, HttpClient httpClient)
        {
            _botClient = botClient;
            _storage = storage;
            _httpClient = httpClient;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var receiverOptions = new ReceiverOptions { AllowedUpdates = { } };
            _botClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions, cancellationToken);

            Console.WriteLine("Telegram bot started...");
            return Task.CompletedTask;
        }

        private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken token)
        {
            if (update.Message is not { } message || message.Text is null) return;

            var chatId = message.Chat.Id;
            var text = message.Text.Trim();

            if (text.StartsWith("/start"))
            {
                var replyKeyboard = new ReplyKeyboardMarkup(new[]
                {
                    new KeyboardButton[] { "Подписаться на парсер✍️" },
                    new KeyboardButton[] { "Отменить текущую подписку🛑" }
                })
                {
                    ResizeKeyboard = true,
                    OneTimeKeyboard = false
                };
                await bot.SendMessage(chatId, "Выберите действие:", replyMarkup: replyKeyboard);
            }
            else if (text == "Подписаться на парсер✍️")
            {
                await bot.SendMessage(chatId, "Введите parserId и пароль через пробел");
            }
            else if (text == "Отменить текущую подписку🛑")
            {
                var success = _storage.RemoveSubscriptionByChatId(chatId);
                if (success)
                    await bot.SendMessage(chatId, "✅ Ваша подписка отменена");
                else
                    await bot.SendMessage(chatId, "⚠️ У вас нет активной подписки");
            }
            else if (text.Contains(' '))
            {
                var parts = text.Split(' ', 2);
                var parserId = parts[0];
                var password = parts[1];

                var valid = await ValidateParserIdAsync(parserId, password);
                if (valid)
                {
                    _storage.SaveSubscription(parserId, chatId);
                    await bot.SendMessage(chatId, $"✅ Вы подписаны как {parserId}");
                }
                else
                {
                    await bot.SendMessage(chatId, $"❌ Неверный parserId или пароль");
                }
            }
            else
            {
                await bot.SendMessage(chatId, "Я не понял вашу команду. Используйте /start для начала.");
            }
        }


        private Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken token)
        {
            Console.WriteLine($"[ERROR] {exception.Message}");
            return Task.CompletedTask;
        }

        private async Task<bool> ValidateParserIdAsync(string parserId, string password)
        {
            var requestData = new
            {
                ParserId = parserId,
                Password = password
            };

            var response = await _httpClient.PostAsJsonAsync("http://backend:9090/Notification/ValidateParserIdAndPassword", requestData);
       
            if (!response.IsSuccessStatusCode)
                return false;

            var result = await response.Content.ReadFromJsonAsync<ValidateResponse>();

            return result?.IsValid ?? false;
        }


        public async Task NotifyAsync(string parserId, string message, string messagelink)
        {
            if (_storage.TryGetChatId(parserId, out var chatId))
            {
                var inlineKeyboard = new InlineKeyboardMarkup(new[]
                {
                   InlineKeyboardButton.WithUrl("Перейти к сообщению", messagelink)
                });
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: message,
                    replyMarkup: inlineKeyboard,
                    parseMode: ParseMode.None
                );
            }
        }
    }
}
