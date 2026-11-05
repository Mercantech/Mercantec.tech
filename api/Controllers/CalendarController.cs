using System.Security.Claims;
using Mercantec.Tech.Api.Auth;
using Mercantec.Tech.Api.Integrations.Azure;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mercantec.Tech.Api.Controllers;

[ApiController, Authorize, Route("api/calendar")]
public sealed class CalendarController(AzureGraphCalendarClient graph) : ControllerBase
{
    /// <summary>Underviserens egen Outlook-kalender via Azure Graph (app-credentials).</summary>
    [HttpGet("mine")]
    public async Task<IActionResult> Mine(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        if (!UserClaims.IsTeacher(User))
            return Forbid();

        var email = User.FindFirstValue("email");
        var upn = graph.ResolveTeacherUpn(email);
        if (string.IsNullOrWhiteSpace(upn))
            return BadRequest(new { error = "missing_email", message = "Din konto mangler e-mail til kalenderkobling." });

        var start = from ?? StartOfWeek(DateTimeOffset.UtcNow);
        var end = to ?? start.AddDays(7);
        if (end <= start)
            return BadRequest(new { error = "invalid_range" });
        if (end - start > TimeSpan.FromDays(45))
            return BadRequest(new { error = "range_too_large", message = "Højst 45 dage ad gangen." });

        if (!graph.IsConfigured)
        {
            return Ok(new TeacherCalendarView(upn, start, end, Configured: false, Events: []));
        }

        try
        {
            var events = await graph.GetCalendarViewAsync(upn, start, end, ct);
            var visible = events.Where(e => !e.IsCancelled).ToList();
            return Ok(new TeacherCalendarView(upn, start, end, Configured: true, Events: visible));
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return NotFound(new { error = "mailbox_not_found", message = $"Ingen postkasse for {upn}." });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = "graph_unavailable",
                message = "Azure-kalenderen kunne ikke hentes lige nu.",
                detail = ex.Message
            });
        }
    }

    private static DateTimeOffset StartOfWeek(DateTimeOffset utc)
    {
        var localDate = DateOnly.FromDateTime(utc.UtcDateTime);
        var monday = localDate.AddDays(-((int)localDate.DayOfWeek + 6) % 7);
        return new DateTimeOffset(monday.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
    }
}
