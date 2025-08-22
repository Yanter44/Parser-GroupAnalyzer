using Microsoft.AspNetCore.SignalR;

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
            var httpContext = Context.GetHttpContext();
            var parserId = httpContext?.Request.Cookies["ParserId"];

            if (!string.IsNullOrEmpty(parserId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, parserId);
                _logger.LogInformation(
                "Клиент {ConnectionId} добавлен в группу {ParserId}",
                Context.ConnectionId, parserId);
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var httpContext = Context.GetHttpContext();
            var parserId = httpContext?.Request.Cookies["ParserId"];
            if (!string.IsNullOrEmpty(parserId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, parserId);
                _logger.LogInformation(
                "Клиент {ConnectionId} удалён из группы {ParserId}",
                Context.ConnectionId, parserId);
            }
            await base.OnDisconnectedAsync(exception);
        }
    }
}
