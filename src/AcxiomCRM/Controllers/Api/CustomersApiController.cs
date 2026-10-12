using AcxiomCRM.Dtos;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers.Api;

[Route("api/customers")]
public class CustomersApiController : ApiControllerBase
{
    private readonly CustomerService _customers;

    public CustomersApiController(CustomerService customers)
    {
        _customers = customers;
    }

    /// <summary>Lists/searches customers in the caller's scope. Query: q, status, assignedTo, sort, page, pageSize.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<CustomerDto>), 200)]
    public async Task<IActionResult> Get([FromQuery] CustomerSearch search)
    {
        var page = await _customers.SearchAsync(search);
        return Ok(ToResponse(page, CustomerDto.From));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CustomerDto), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _customers.GetAsync(id);
        return result.Succeeded ? Ok(CustomerDto.From(result.Value!)) : result.ToApiError(this);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CustomerDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Create(CustomerRequest request)
    {
        var result = await _customers.CreateAsync(request);
        if (!result.Succeeded)
        {
            return result.ToApiError(this);
        }

        var created = await _customers.GetAsync(result.Value!.CustomerId);
        return CreatedAtAction(nameof(GetById), new { id = result.Value.CustomerId }, CustomerDto.From(created.Value!));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(CustomerDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Update(int id, CustomerRequest request)
    {
        var result = await _customers.UpdateAsync(id, request);
        return result.Succeeded ? Ok(CustomerDto.From(result.Value!)) : result.ToApiError(this);
    }

    /// <summary>Deletes the customer, or deactivates it when related records exist. Admin and Manager only.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _customers.DeleteAsync(id);
        if (!result.Succeeded)
        {
            return result.ToApiError(this);
        }

        return Ok(new
        {
            id,
            result = result.Value ? "Deleted" : "Deactivated",
            message = result.Value ? "Customer deleted." : "Customer has related records and was deactivated (Status = Inactive)."
        });
    }
}
