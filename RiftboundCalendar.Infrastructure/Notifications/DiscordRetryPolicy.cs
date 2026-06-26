using System.Runtime.ExceptionServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace RiftboundCalendar.Infrastructure.Notifications;

public sealed class DiscordRetryPolicy : IRetryPolicy
{
    private const int MaxAttempts = 3;
    private static readonly TimeSpan[] DefaultBackoff =
        [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)];

    private readonly ILogger<DiscordRetryPolicy> _logger;
    private readonly TimeSpan[] _backoff;

    public DiscordRetryPolicy(ILogger<DiscordRetryPolicy> logger)
        : this(logger, DefaultBackoff) { }

    public DiscordRetryPolicy(ILogger<DiscordRetryPolicy> logger, TimeSpan[] backoff)
    {
        _logger = logger;
        _backoff = backoff;
    }

    public async Task<HttpResponseMessage> ExecuteAsync(
        Func<CancellationToken, Task<HttpResponseMessage>> operation,
        CancellationToken ct)
    {
        HttpResponseMessage? lastResponse = null;
        ExceptionDispatchInfo? lastException = null;

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            try
            {
                lastResponse = await operation(ct);

                if (lastResponse.IsSuccessStatusCode)
                    return lastResponse;

                var statusCode = (int)lastResponse.StatusCode;

                if (statusCode == 429 && attempt < MaxAttempts - 1)
                {
                    var delay = await ReadRetryAfterAsync(lastResponse);
                    _logger.LogWarning("Discord rate limited, retrying after {Delay:F1}s (attempt {Attempt}/{Max})",
                        delay.TotalSeconds, attempt + 1, MaxAttempts);
                    await Task.Delay(delay, ct);
                    continue;
                }

                if (statusCode >= 500 && attempt < MaxAttempts - 1)
                {
                    _logger.LogWarning("Discord returned {Status}, retrying (attempt {Attempt}/{Max})",
                        lastResponse.StatusCode, attempt + 1, MaxAttempts);
                    await Task.Delay(_backoff[attempt], ct);
                    continue;
                }

                return lastResponse;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastException = ExceptionDispatchInfo.Capture(ex);
                _logger.LogWarning(ex, "Discord request threw exception (attempt {Attempt}/{Max})",
                    attempt + 1, MaxAttempts);
                if (attempt < MaxAttempts - 1)
                    await Task.Delay(_backoff[attempt], ct);
            }
        }

        if (lastException is not null)
            lastException.Throw();

        return lastResponse!;
    }

    private static async Task<TimeSpan> ReadRetryAfterAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("retry_after", out var prop))
                return TimeSpan.FromSeconds(prop.GetDouble());
        }
        catch
        {
            // fall through to default
        }
        return TimeSpan.FromSeconds(1);
    }
}
