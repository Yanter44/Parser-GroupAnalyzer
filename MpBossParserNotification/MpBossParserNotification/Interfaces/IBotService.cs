using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MpBossParserNotification.Interfaces
{
    public interface IBotService
    {
        Task StartAsync(CancellationToken cancellationToken);
        Task NotifyAsync(string parserId, string message, string messageLink);
    }
}
