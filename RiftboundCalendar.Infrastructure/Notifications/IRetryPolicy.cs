namespace RiftboundCalendar.Infrastructure.Notifications;

public interface IRetryPolicy
{
    Task<HttpResponseMessage> ExecuteAsync(
        Func<CancellationToken, Task<HttpResponseMessage>> operation,
        CancellationToken ct);
}
