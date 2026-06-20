using System.Security.Claims;
using BronyTV.Contract;
using BronyTV.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BronyTV.Controllers;

[ApiController]
[Route("api/support")]
public class SupportController : ControllerBase
{
    private readonly ISupportService _supportService;

    public SupportController(ISupportService supportService)
    {
        _supportService = supportService;
    }

    [Authorize(Roles = "User")]
    [HttpPost("tickets")]
    public async Task<IActionResult> CreateTicket(
        [FromBody] CreateSupportTicketRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var title = request.Title ?? request.Subject ?? string.Empty;
        var content = request.Content ?? request.Description ?? string.Empty;

        var (response, error, statusCode) = await _supportService.CreateTicketAsync(
            userId,
            title,
            content,
            cancellationToken);

        if (response == null)
        {
            return StatusCode(statusCode, new { message = error });
        }

        return Ok(response);
    }

    [Authorize(Roles = "User")]
    [HttpGet("tickets/me")]
    public async Task<IActionResult> GetMyTickets(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var tickets = await _supportService.GetMyTicketsAsync(userId, cancellationToken);
        return Ok(tickets);
    }

    [Authorize(Roles = "User")]
    [HttpGet("tickets/{ticketId:guid}/messages")]
    public async Task<IActionResult> GetTicketMessages(Guid ticketId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var (messages, error, statusCode) = await _supportService.GetTicketMessagesAsync(
            ticketId,
            userId,
            User.IsInRole("Admin"),
            cancellationToken);

        if (error != null)
        {
            return StatusCode(statusCode, new { message = error });
        }

        return Ok(messages);
    }

    [Authorize(Roles = "User")]
    [HttpPost("tickets/{ticketId:guid}/messages")]
    public async Task<IActionResult> AddMessage(
        Guid ticketId,
        [FromBody] CreateSupportMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var content = request.Content ?? request.Text ?? string.Empty;
        var (response, error, statusCode) = await _supportService.AddMessageAsync(
            ticketId,
            userId,
            User.IsInRole("Admin"),
            content,
            cancellationToken);

        if (response == null)
        {
            return StatusCode(statusCode, new { message = error });
        }

        return Ok(response);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("tickets/all")]
    public async Task<IActionResult> GetAllTickets(
        [FromQuery] string? query,
        CancellationToken cancellationToken)
    {
        var tickets = await _supportService.GetAllTicketsAsync(query, cancellationToken);
        return Ok(tickets);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("tickets")]
    public Task<IActionResult> GetAllTicketsAlias(
        [FromQuery] string? query,
        CancellationToken cancellationToken) =>
        GetAllTickets(query, cancellationToken);

    [Authorize(Roles = "Admin")]
    [HttpPut("tickets/{ticketId:guid}/close")]
    public async Task<IActionResult> CloseTicket(Guid ticketId, CancellationToken cancellationToken)
    {
        var (success, error, statusCode) = await _supportService.CloseTicketAsync(ticketId, cancellationToken);
        if (!success)
        {
            return StatusCode(statusCode, new { message = error });
        }

        return Ok(new { message = "Обращение закрыто." });
    }

    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out userId);
    }
}
