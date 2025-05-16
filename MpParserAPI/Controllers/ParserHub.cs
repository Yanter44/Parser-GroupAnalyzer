using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace MpParserAPI.Controllers
{
    public class ParserHub : Hub
    {
        private readonly ILogger<ParserHub> _logger;

        public ParserHub(ILogger<ParserHub> logger)
        {
            _logger = logger;
        }
        public override async Task OnConnectedAsync()
        {
            var parserId = Context.GetHttpContext().Request.Query["parserId"];
            if (!string.IsNullOrEmpty(parserId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, parserId);
                _logger.LogInformation($"Клиент {Context.ConnectionId} добавлен в группу {parserId}");
            }       
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var parserId = Context.GetHttpContext().Request.Query["parserId"];
            if (!string.IsNullOrEmpty(parserId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, parserId);
                _logger.LogInformation($"Клиент {Context.ConnectionId} удалён из группы {parserId}");
            }
            await base.OnDisconnectedAsync(exception);
        }
    }
}
