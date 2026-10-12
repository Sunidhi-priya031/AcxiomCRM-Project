using AcxiomCRM.Dtos;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers.Api;

[Route("api/followups")]
public class FollowUpsApiController : ApiControllerBase
{
    private readonly FollowUpService _followUps;

    public FollowUpsApiController(FollowUpService followUps)
    {
        _followUps = followUps;
    }

    /// <summary>Lists follow-ups in the caller's scope. Query: view (pending|today|upcoming|overdue), status, type, assignedTo, from, to, related, page, pageSize.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<FollowUpDto>), 200)]
    public async Task<IActionResult> Get([FromQuery] FollowUpSearch search)
    {
        return Ok(ToResponse(await _followUps.SearchAsync(search), FollowUpDto.From));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(FollowUpDto), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _followUps.GetAsync(id);
        return result.Succeeded ? Ok(FollowUpDto.From(result.Value!)) : result.ToApiError(this);
    }

    /// <summary>Schedules a follow-up linked to exactly one customer, lead or opportunity. The date cannot be earlier than today.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(FollowUpDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create(FollowUpRequest request)
    {
        var result = await _followUps.CreateAsync(request);
        if (!result.Succeeded)
        {
            return result.ToApiError(this);
        }

        var created = await _followUps.GetAsync(result.Value!.FollowUpId);
        return CreatedAtAction(nameof(GetById), new { id = result.Value.FollowUpId }, FollowUpDto.From(created.Value!));
    }
}
