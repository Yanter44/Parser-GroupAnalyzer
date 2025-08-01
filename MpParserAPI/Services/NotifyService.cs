using System.Net.Http;
using MpParserAPI.Interfaces;
using MpParserAPI.Models.Dtos;

namespace MpParserAPI.Services
{
    public class NotifyService : INotify
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<NotifyService> _logger;
        private readonly IConfiguration _configuration;

        public NotifyService(IHttpClientFactory httpClientFactory, ILogger<NotifyService> logger, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task SendNotifyToBotAboutReceivedMessageAsync(Guid parserId, string message, string messageLink)
        {
            var client = _httpClientFactory.CreateClient();

            var notifyRequest = new NotifyRequestDto
            {
                ParserId = parserId.ToString(),
                Message = message,
                MessageLink = messageLink
            };
            var url = _configuration["BotSettings:NotifyUrl"];

            var response = await client.PostAsJsonAsync(url, notifyRequest);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Не удалось отправить уведомление боту: {StatusCode}", response.StatusCode);
            }
        }

        public async Task SendSimpleNotify(Guid parserId, string message)
        {
            var client = _httpClientFactory.CreateClient();

            var notifyRequest = new NotifyRequestDto
            {
                ParserId = parserId.ToString(),
                Message = message
            };
            var url = _configuration["BotSettings:NotifyUrl"];

            var response = await client.PostAsJsonAsync(url, notifyRequest);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Не удалось отправить уведомление боту: {StatusCode}", response.StatusCode);
            }
        }
    }
}

