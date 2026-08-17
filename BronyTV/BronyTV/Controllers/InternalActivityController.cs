using BronyTV.Contract;
using BronyTV.Service;
using Microsoft.AspNetCore.Mvc;

namespace BronyTV.Controllers;

/// <summary>
/// Внутренние server-to-server эндпоинты. Доступ защищён секретным заголовком
/// (переменная окружения BRONYTV_INTERNAL_KEY, общая для BronyTV и AiBronyTV).
/// Не предназначены для прямых запросов с фронтенда.
/// </summary>
[ApiController]
[Route("api/internal")]
public class InternalActivityController : ControllerBase
{
    private readonly IUserActivityService _userActivityService;
    private readonly string _internalKey;

    public InternalActivityController(
        IUserActivityService userActivityService,
        IConfiguration configuration)
    {
        _userActivityService = userActivityService;
        _internalKey = configuration["InternalApiKey"]
            ?? Environment.GetEnvironmentVariable("BRONYTV_INTERNAL_KEY")
            ?? string.Empty;
    }

    /// <summary>
    /// Логирует факт общения пользователя с ботом. Принимает ТОЛЬКО UserId и имя бота —
    /// текст сообщения никуда не передаётся и не сохраняется.
    /// </summary>
    [HttpPost("activity/bot-chat")]
    public async Task<IActionResult> RecordBotChat(
        [FromBody] RecordBotChatRequest request,
        CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue("X-Internal-Key", out var supplied)
            || string.IsNullOrWhiteSpace(_internalKey)
            || !string.Equals(supplied, _internalKey, StringComparison.Ordinal))
        {
            return Unauthorized(new { message = "Недопустимый внутренний ключ." });
        }

        if (request == null || request.UserId == Guid.Empty)
        {
            return BadRequest(new { message = "UserId обязателен." });
        }

        // Details = имя персонажа-бота, НИКОГДА не текст сообщения.
        await _userActivityService.RecordAsync(
            request.UserId,
            "bot_chat",
            string.IsNullOrWhiteSpace(request.CharacterId) ? null : request.CharacterId,
            cancellationToken);

        return Ok();
    }
}

public class RecordBotChatRequest
{
    public Guid UserId { get; set; }

    /// <summary>Имя/идентификатор персонажа-бота (characterId). Не текст сообщения.</summary>
    public string? CharacterId { get; set; }
}
