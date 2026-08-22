namespace UserManagementApp.Infrastructure.Services;

public class QueuedBackgroundService : BackgroundService
{
    private readonly IBackgroundTaskQueue _queue;
    private readonly ILogger<QueuedBackgroundService> _logger;

    public QueuedBackgroundService(
        IBackgroundTaskQueue queue,
        ILogger<QueuedBackgroundService> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var workItem = await _queue.DequeueAsync(stoppingToken);

            try
            {
                await workItem(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "An error occurred while processing a background work item.");
            }
        }
    }
}
