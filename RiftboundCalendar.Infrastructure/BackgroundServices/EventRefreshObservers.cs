using RiftboundCalendar.Core.Interfaces;
using RiftboundCalendar.Infrastructure.Caching;

namespace RiftboundCalendar.Infrastructure.BackgroundServices;

public sealed record EventRefreshObservers(
    StartupReadiness Readiness,
    IEventNotifier Notifier,
    INotificationStateRepository StateRepository);
