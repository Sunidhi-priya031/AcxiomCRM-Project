using AcxiomCRM.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers;

/// <summary>Error and status-code pages. API paths receive problem-details JSON instead of HTML.</summary>
[AllowAnonymous]
[IgnoreAntiforgeryToken]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    [Route("/Home/Error")]
    public IActionResult Error()
    {
        var feature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        if (feature != null)
        {
            _logger.LogError(feature.Error, "Unhandled exception for {Path}", feature.Path);
        }

        if (feature?.Path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) == true)
        {
            return new ObjectResult(new ProblemDetails { Status = 500, Title = "An unexpected error occurred. Please try again later." })
            { StatusCode = 500 };
        }

        Response.StatusCode = 500;
        return View("Error");
    }

    [Route("/Home/StatusCode/{code:int}")]
    public IActionResult StatusCodePage(int code)
    {
        var feature = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();
        if (feature?.OriginalPath.StartsWith("/api", StringComparison.OrdinalIgnoreCase) == true)
        {
            var title = code switch
            {
                401 => "Authentication is required.",
                403 => "You are not authorised to access this resource.",
                404 => "The requested resource was not found.",
                429 => "Too many requests. Please wait and try again.",
                _ => "The request could not be completed."
            };
            return new ObjectResult(new ProblemDetails { Status = code, Title = title }) { StatusCode = code };
        }

        Response.StatusCode = code;
        return View("StatusCode", code);
    }
}
