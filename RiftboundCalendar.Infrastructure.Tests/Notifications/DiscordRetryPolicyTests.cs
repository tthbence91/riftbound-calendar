using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RiftboundCalendar.Infrastructure.Notifications;

namespace RiftboundCalendar.Infrastructure.Tests.Notifications;

public class DiscordRetryPolicyTests
{
    [Fact]
    public async Task ExecuteAsync_OnSuccess_CallsOperationOnce()
    {
        var callCount = 0;
        var sut = CreateSut();

        var result = await sut.ExecuteAsync(_ =>
        {
            callCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }, CancellationToken.None);

        callCount.Should().Be(1);
        result.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ExecuteAsync_On5xxThenSuccess_RetriesOnce()
    {
        var callCount = 0;
        var sut = CreateSut();

        var result = await sut.ExecuteAsync(_ =>
        {
            callCount++;
            var code = callCount == 1 ? HttpStatusCode.InternalServerError : HttpStatusCode.NoContent;
            return Task.FromResult(new HttpResponseMessage(code));
        }, CancellationToken.None);

        callCount.Should().Be(2);
        result.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ExecuteAsync_On429ThenSuccess_RetriesOnce()
    {
        var callCount = 0;
        var sut = CreateSut();

        var result = await sut.ExecuteAsync(_ =>
        {
            callCount++;
            if (callCount == 1)
            {
                var rateLimited = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Content = new StringContent("{\"retry_after\": 0.001}")
                };
                return Task.FromResult(rateLimited);
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }, CancellationToken.None);

        callCount.Should().Be(2);
        result.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ExecuteAsync_On4xxNotRateLimit_DoesNotRetry()
    {
        var callCount = 0;
        var sut = CreateSut();

        var result = await sut.ExecuteAsync(_ =>
        {
            callCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
        }, CancellationToken.None);

        callCount.Should().Be(1);
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ExecuteAsync_OnThreeConsecutive5xx_ReturnsLastResponse()
    {
        var sut = CreateSut();

        var result = await sut.ExecuteAsync(
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)),
            CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task ExecuteAsync_OnNetworkExceptionThenSuccess_RetriesOnce()
    {
        var callCount = 0;
        var sut = CreateSut();

        var result = await sut.ExecuteAsync(_ =>
        {
            callCount++;
            if (callCount == 1)
                throw new HttpRequestException("Network error");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }, CancellationToken.None);

        callCount.Should().Be(2);
        result.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ExecuteAsync_OnAllRetriesNetworkException_RethrowsException()
    {
        var sut = CreateSut();

        var act = () => sut.ExecuteAsync(
            _ => throw new HttpRequestException("Network error"),
            CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>().WithMessage("Network error");
    }

    [Fact]
    public async Task ExecuteAsync_OnCancellation_DoesNotRetry()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var sut = CreateSut();

        var act = () => sut.ExecuteAsync(
            ct => Task.FromCanceled<HttpResponseMessage>(ct),
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static DiscordRetryPolicy CreateSut() =>
        new(NullLogger<DiscordRetryPolicy>.Instance,
            backoff: [TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero]);
}
