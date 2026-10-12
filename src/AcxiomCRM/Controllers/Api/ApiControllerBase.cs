using AcxiomCRM.Dtos;
using AcxiomCRM.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers.Api;

/// <summary>
/// Base for REST endpoints: authenticated by session cookie or bearer token, JSON only, model validation
/// handled by [ApiController] (400 with validation problem details). Anti-forgery tokens are not used for the
/// API: bearer requests are not cookie-based, and the session cookie is SameSite=Strict.
/// </summary>
[ApiController]
[Authorize(Policy = ApiPolicies.ApiUser)]
[IgnoreAntiforgeryToken]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected static PagedResponse<TDto> ToResponse<TEntity, TDto>(PagedResult<TEntity> page, Func<TEntity, TDto> map) =>
        new(page.Items.Select(map).ToList(), page.Page, page.PageSize, page.TotalCount, page.TotalPages);
}
