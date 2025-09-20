using System.Collections.Concurrent;
using MpParserAPI.Interfaces;
using MpParserAPI.Models;
using TL;

public class MessageQueueService : IMessageQueueService, IDisposable
{
    private readonly ConcurrentDictionary<Guid, Queue<UpdateData>> _parserQueues = new();
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _queueLocks = new();
    private readonly ILogger<MessageQueueService> _logger;

    public MessageQueueService(ILogger<MessageQueueService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> EnqueueMessageAsync(Guid parserId, UpdatesBase update, ParserData parserData)
    {
        try
        {
            var queue = _parserQueues.GetOrAdd(parserId, _ => new Queue<UpdateData>());
            var lockObj = _queueLocks.GetOrAdd(parserId, _ => new SemaphoreSlim(1, 1));

            await lockObj.WaitAsync();

            try
            {

                var updateData = new UpdateData(update, parserData, DateTime.UtcNow);
                queue.Enqueue(updateData);

                _logger.LogInformation("Сообщение добавлено в очередь парсера {ParserId}. Размер: {Count}",
                    parserId, queue.Count);

                return true;
            }
            finally
            {
                lockObj.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка добавления сообщения в очередь парсера {ParserId}", parserId);
            return false;
        }
    }

    public async Task<UpdateData?> DequeueMessageAsync(Guid parserId)
    {
        if (!_parserQueues.TryGetValue(parserId, out var queue))
            return null;

        var lockObj = _queueLocks.GetOrAdd(parserId, _ => new SemaphoreSlim(1, 1));
        await lockObj.WaitAsync();

        try
        {
            if (queue.Count > 0)
            {
                return queue.Dequeue();
            }
            return null;
        }
        finally
        {
            lockObj.Release();
        }
    }

    public int GetQueueSize(Guid parserId)
    {
        return _parserQueues.TryGetValue(parserId, out var queue) ? queue.Count : 0;
    }

    public bool HasMessages(Guid parserId)
    {
        return GetQueueSize(parserId) > 0;
    }

    public void ClearQueue(Guid parserId)
    {
        if (_parserQueues.TryRemove(parserId, out var queue))
        {
            queue.Clear();
            _logger.LogInformation("Очередь парсера {ParserId} очищена", parserId);
        }
    }

    public void Dispose()
    {
        foreach (var lockObj in _queueLocks.Values)
        {
            lockObj.Dispose();
        }
    }
}

public record UpdateData(UpdatesBase Update, ParserData ParserData, DateTime ReceivedTime);