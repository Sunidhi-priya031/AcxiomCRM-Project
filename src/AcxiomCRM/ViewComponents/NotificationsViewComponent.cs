using AcxiomCRM.Services;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.ViewComponents;

/// <summary>Header notifications: the signed-in user's overdue and due-today follow-ups.</summary>
public class NotificationsViewComponent : ViewComponent
{
    private readonly FollowUpService _followUps;
    private readonly IUserContext _user;

    public NotificationsViewComponent(FollowUpService followUps, IUserContext user)
    {
        _followUps = followUps;
        _user = user;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var (overdue, today, items) = await _followUps.GetNotificationsAsync(_user.UserId);
        return View(new NotificationsModel(overdue, today, items));
    }
}

public record NotificationsModel(int Overdue, int Today, List<Models.FollowUp> Items)
{
    public int Total => Overdue + Today;
}
