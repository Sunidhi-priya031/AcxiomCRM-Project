using AcxiomCRM.Dtos;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers.Api;

[Route("api/opportunities")]
public class OpportunitiesApiController : ApiControllerBase
{
    private readonly OpportunityService _opportunities;

    public OpportunitiesApiController(OpportunityService opportunities)
    {
        _opportunities = opportunities;
    }

    /// <summary>Lists/searches opportunities in the caller's scope. Query: q, stage, status, customerId, assignedTo, sort, page, pageSize.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<OpportunityDto>), 200)]
    public async Task<IActionResult> Get([FromQuery] OpportunitySearch search)
    {
        return Ok(ToResponse(await _opportunities.SearchAsync(search), OpportunityDto.From));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(OpportunityDto), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _opportunities.GetAsync(id);
        return result.Succeeded ? Ok(OpportunityDto.From(result.Value!)) : result.ToApiError(this);
    }

    /// <summary>Creates an opportunity. Amount must be &gt; 0, probability 0-100, expected close date today or later.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(OpportunityDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create(OpportunityRequest request)
    {
        var result = await _opportunities.CreateAsync(request);
        if (!result.Succeeded)
        {
            return result.ToApiError(this);
        }

        var created = await _opportunities.GetAsync(result.Value!.OpportunityId);
        return CreatedAtAction(nameof(GetById), new { id = result.Value.OpportunityId }, OpportunityDto.From(created.Value!));
    }
}
