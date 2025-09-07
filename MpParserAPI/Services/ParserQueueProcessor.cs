
using MpParserAPI.Interfaces;
using System.Collections.Concurrent;

namespace MpParserAPI.Services
{
    public class ParserQueueProcessor : BackgroundService
    {
        private readonly IMessageQueueService _messageQueueService;
        private readonly IParserDataStorage _parserStorage;
        private readonly ILogger<ParserQueueProcessor> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly ConcurrentDictionary<Guid, Task> _workers = new();

        public ParserQueueProcessor(
            IMessageQueueService messageQueueService,
            IParserDataStorage parserStorage,
            ILogger<ParserQueueProcessor> logger,
            IServiceProvider serviceProvider)
        {
            _messageQueueService = messageQueueService;
            _parserStorage = parserStorage;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Запуск воркеров для всех парсеров");

            foreach (var parserId in _parserStorage.GetAllParserIds())
            {
                var task = Task.Run(() => WorkerLoop(parserId, stoppingToken), stoppingToken);
                _workers[parserId] = task;
            }

            return Task.WhenAll(_workers.Values);
        }

        private async Task WorkerLoop(Guid parserId, CancellationToken stoppingToken)
        {
            _logger.LogInformation("Воркер для парсера {ParserId} запущен", parserId);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var updateData = await _messageQueueService.DequeueMessageAsync(parserId);

                    if (updateData == null)
                    {
                        await Task.Delay(100, stoppingToken); 
                        continue;
                    }

                    using var scope = _serviceProvider.CreateScope();
                    var handler = scope.ServiceProvider.GetRequiredService<IUpdateHandler>();

                    await handler.HandleAsync(parserId, updateData);

                    await Task.Delay(200, stoppingToken);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка в воркере {ParserId}", parserId);

                }
            }

            _logger.LogInformation("Воркер для парсера {ParserId} остановлен", parserId);
        }
    }


}

