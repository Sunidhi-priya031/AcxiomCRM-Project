using AcxiomCRM.Dtos;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers.Api;

[Route("api/leads")]
public class LeadsApiController : ApiControllerBase
{
    private readonly LeadService _leads;

    public LeadsApiController(LeadService leads)
    {
        _leads = leads;
    }

    /// <summary>Lists/searches leads in the caller's scope. Query: q, status, source, priority, assignedTo, sort, page, pageSize.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<LeadDto>), 200)]
    public async Task<IActionResult> Get([FromQuery] LeadSearch search)
    {
        return Ok(ToResponse(await _leads.SearchAsync(search), LeadDto.From));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(LeadDto), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _leads.GetAsync(id);
        return result.Succeeded ? Ok(LeadDto.From(result.Value!)) : result.ToApiError(this);
    }

    [HttpPost]
    [ProducesResponseType(typeof(LeadDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create(LeadRequest request)
    {
        var result = await _leads.CreateAsync(request);
        if (!result.Succeeded)
        {
            return result.ToApiError(this);
        }

        var created = await _leads.GetAsync(result.Value!.LeadId);
        return CreatedAtAction(nameof(GetById), new { id = result.Value.LeadId }, LeadDto.From(created.Value!));
    }
}
