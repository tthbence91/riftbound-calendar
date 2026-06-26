using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RiftboundCalendar.Core.Entities;
using RiftboundCalendar.Infrastructure.Notifications;

namespace RiftboundCalendar.Infrastructure.Tests.Notifications;

public class DiscordNotifierStatusTests
{
    [Fact]
    public async Task NotifyStatusChangedAsync_WithNoWebhookUrl_DoesNotSendRequest()
    {
        var handler = new CapturingHttpMessageHandler();
        var factory = CreateFactory(handler);
        var sut = new DiscordNotifier(factory, new NoRetryPolicy(), Options.Create(new DiscordOptions()), NullLogger<DiscordNotifier>.Instance);

        await sut.NotifyStatusChangedAsync([CreateChange(RegistrationStatus.Closed, RegistrationStatus.Open)], CancellationToken.None);

        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task NotifyStatusChangedAsync_WithEmptyList_DoesNotSendRequest()
    {
        var handler = new CapturingHttpMessageHandler();
        var factory = CreateFactory(handler);
        var sut = new DiscordNotifier(
            factory,
            new NoRetryPolicy(),
            Options.Create(new DiscordOptions { WebhookUrl = "https://discord.com/api/webhooks/test" }),
            NullLogger<DiscordNotifier>.Instance);

        await sut.NotifyStatusChangedAsync([], CancellationToken.None);

        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task NotifyStatusChangedAsync_WithChanges_SendsOneRequestPerBatch()
    {
        var handler = new CapturingHttpMessageHandler();
        var factory = CreateFactory(handler);
        var sut = new DiscordNotifier(
            factory,
            new NoRetryPolicy(),
            Options.Create(new DiscordOptions { WebhookUrl = "https://discord.com/api/webhooks/test" }),
            NullLogger<DiscordNotifier>.Instance);

        var changes = Enumerable.Range(0, 3)
            .Select(_ => CreateChange(RegistrationStatus.Closed, RegistrationStatus.Open))
            .ToList();

        await sut.NotifyStatusChangedAsync(changes, CancellationToken.None);

        handler.Requests.Should().HaveCount(1);
    }

    [Fact]
    public async Task NotifyStatusChangedAsync_MoreThan10Changes_SendsMultipleBatches()
    {
        var handler = new CapturingHttpMessageHandler();
        var factory = CreateFactory(handler);
        var sut = new DiscordNotifier(
            factory,
            new NoRetryPolicy(),
            Options.Create(new DiscordOptions { WebhookUrl = "https://discord.com/api/webhooks/test" }),
            NullLogger<DiscordNotifier>.Instance);

        var changes = Enumerable.Range(0, 12)
            .Select(_ => CreateChange(RegistrationStatus.Closed, RegistrationStatus.Open))
            .ToList();

        await sut.NotifyStatusChangedAsync(changes, CancellationToken.None);

        handler.Requests.Should().HaveCount(2);
    }

    private static IHttpClientFactory CreateFactory(HttpMessageHandler handler)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler));
        return factory.Object;
    }

    private static StatusChange CreateChange(RegistrationStatus from, RegistrationStatus to)
    {
        var evt = new RiftboundEvent(
            Guid.NewGuid().ToString(),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddHours(4),
            new EventLocation("Test Store", 47.4979, 19.0402),
            new EventInfo("Test Event", "Constructed", new Uri("https://example.com")));
        return new StatusChange(evt, from, to);
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }
    }

    private sealed class NoRetryPolicy : IRetryPolicy
    {
        public Task<HttpResponseMessage> ExecuteAsync(
            Func<CancellationToken, Task<HttpResponseMessage>> operation,
            CancellationToken ct) => operation(ct);
    }
}
