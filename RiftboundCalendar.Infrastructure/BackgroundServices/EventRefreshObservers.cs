using RiftboundCalendar.Infrastructure.Caching;
using RiftboundCalendar.Infrastructure.Notifications;

namespace RiftboundCalendar.Infrastructure.BackgroundServices;

public sealed record EventRefreshObservers(StartupReadiness Readiness, DiscordNotifier Notifier);
