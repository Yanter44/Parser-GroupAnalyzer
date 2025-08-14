using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using MpBossParserNotification.Interfaces;

namespace MpBossParserNotification.HostedServices
{
    public class BotHostedService : IHostedService
    {
        private readonly IBotService _bot;

        public BotHostedService(IBotService bot)
        {
            _bot = bot;
        }

        public Task StartAsync(CancellationToken cancellationToken) => _bot.StartAsync(cancellationToken);

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
