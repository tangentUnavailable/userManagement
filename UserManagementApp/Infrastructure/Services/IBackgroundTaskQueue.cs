namespace UserManagementApp.Infrastructure.Services;

//     Represents a unit of work that can be processed in the background.
public interface IBackgroundTaskQueue
{
    void QueueBackgroundWorkItem(Func<CancellationToken, Task> workItem);

    Task<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken);
}
