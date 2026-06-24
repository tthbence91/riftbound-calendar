namespace RiftboundCalendar.Infrastructure.Caching;

public sealed class StartupReadiness
{
    private readonly TaskCompletionSource _ready =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Signal() => _ready.TrySetResult();

    public Task WaitAsync(CancellationToken cancellationToken) =>
        _ready.Task.WaitAsync(cancellationToken);
}
