using Microsoft.AspNetCore.Mvc;
using RiftboundCalendar.Api.Dtos;
using RiftboundCalendar.Core.Interfaces;
using RiftboundCalendar.Infrastructure.Caching;

namespace RiftboundCalendar.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class EventsController : ControllerBase
{
    private static readonly TimeSpan StartupWaitTimeout = TimeSpan.FromSeconds(30);

    private readonly IEventRepository _repository;
    private readonly StartupReadiness _readiness;

    public EventsController(IEventRepository repository, StartupReadiness readiness)
    {
        _repository = repository;
        _readiness = readiness;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RiftboundEventDto>>> GetEvents(
        CancellationToken cancellationToken)
    {
        await WaitForStartupAsync(cancellationToken);
        var events = await _repository.GetEventsAsync(cancellationToken);
        return Ok(events.Select(RiftboundEventDto.FromDomain).ToList());
    }

    [HttpGet("{id}/history")]
    public async Task<ActionResult<IReadOnlyList<StatusHistoryEntryDto>>> GetEventHistory(
        string id,
        [FromServices] IStatusHistoryRepository historyRepository,
        CancellationToken cancellationToken)
    {
        var entries = await historyRepository.GetByEventIdAsync(id, cancellationToken);
        return Ok(entries.Select(StatusHistoryEntryDto.FromDomain).ToList());
    }

    private async Task WaitForStartupAsync(CancellationToken requestCancelled)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(requestCancelled);
        cts.CancelAfter(StartupWaitTimeout);
        try
        {
            await _readiness.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException) when (!requestCancelled.IsCancellationRequested)
        {
            // startup timeout expired — proceed with whatever is in cache
        }
    }
}
