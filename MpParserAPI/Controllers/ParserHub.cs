using Microsoft.AspNetCore.SignalR;

namespace MpParserAPI.Controllers
{
    public class ParserHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            var parserId = Context.GetHttpContext().Request.Query["parserId"];
            if (!string.IsNullOrEmpty(parserId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, parserId);
                Console.WriteLine($"Клиент {Context.ConnectionId} добавлен в группу {parserId}");
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var parserId = Context.GetHttpContext().Request.Query["parserId"];
            if (!string.IsNullOrEmpty(parserId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, parserId);
                Console.WriteLine($"Клиент {Context.ConnectionId} удалён из группы {parserId}");
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}
