using Microsoft.AspNetCore.Mvc;
using RiftboundCalendar.Api.Dtos;
using RiftboundCalendar.Core.Interfaces;

namespace RiftboundCalendar.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class EventsController : ControllerBase
{
    private readonly IEventRepository _repository;

    public EventsController(IEventRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RiftboundEventDto>>> GetEvents(
        CancellationToken cancellationToken)
    {
        var events = await _repository.GetEventsAsync(cancellationToken);
        return Ok(events.Select(RiftboundEventDto.FromDomain).ToList());
    }
}
