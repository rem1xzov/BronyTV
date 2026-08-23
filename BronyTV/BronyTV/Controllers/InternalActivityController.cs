using BronyTV.Contract;
using BronyTV.DbContext;
using BronyTV.DbContext.Entity;
using BronyTV.Infrastructure;
using BronyTV.Service;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
    private readonly DbBronyTV _context;
    private readonly IAdminAccessService _adminAccessService;
    private readonly string _internalKey;

    public InternalActivityController(
        IUserActivityService userActivityService,
        DbBronyTV context,
        IAdminAccessService adminAccessService,
        IConfiguration configuration)
    {
        _userActivityService = userActivityService;
        _context = context;
        _adminAccessService = adminAccessService;
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

    /// <summary>
    /// Возвращает userId владельца сайта (детерминированный источник для одноразовой
    /// миграции премиум-подписок из AI-сервиса). Если владелец не найден — 404.
    /// </summary>
    [HttpGet("owner-user")]
    public async Task<IActionResult> GetOwnerUser(CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue("X-Internal-Key", out var supplied)
            || string.IsNullOrWhiteSpace(_internalKey)
            || !string.Equals(supplied, _internalKey, StringComparison.Ordinal))
        {
            return Unauthorized(new { message = "Недопустимый внутренний ключ." });
        }

        var owners = await _context.Users
            .AsNoTracking()
            .Where(user => user.IsEmailConfirmed)
            .ToListAsync(cancellationToken);

        var owner = owners.FirstOrDefault(user =>
            _adminAccessService.IsOwnerUser(user));
        if (owner == null)
        {
            return NotFound(new { message = "Владелец не найден." });
        }

        return Ok(new { userId = owner.Id.ToString(), email = owner.Email });
    }

    /// <summary>
    /// Возвращает список ВСЕХ аккаунтов системы (userId + email) для подтверждённых
    /// пользователей. Используется одноразовой миграцией премиум-подписок из AI-сервиса,
    /// которая должна охватывать НЕ только Owner/Admin, но и всех обычных пользователей,
    /// у которых осталась "осиротевшая" legacy-запись (переход sessionId → userId затронул
    /// всех). Соединять потенциально тысячную выборку здесь безопасно: отдаются только
    /// userId + email.
    /// </summary>
    [HttpGet("all-users")]
    public async Task<IActionResult> GetAllUsers(CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue("X-Internal-Key", out var supplied)
            || string.IsNullOrWhiteSpace(_internalKey)
            || !string.Equals(supplied, _internalKey, StringComparison.Ordinal))
        {
            return Unauthorized(new { message = "Недопустимый внутренний ключ." });
        }

        var users = await _context.Users
            .AsNoTracking()
            .Where(user => user.IsEmailConfirmed)
            .OrderBy(user => user.CreatedAtUtc)
            .Select(user => new { userId = user.Id.ToString(), email = user.Email })
            .ToListAsync(cancellationToken);

        return Ok(new { users, total = users.Count });
    }

    /// <summary>
    /// Возвращает список всех аккаунтов с ролью Owner или Admin (userId + email).
    /// Держим для обратной совместимости; современная миграция legacy-премиума использует
    /// /api/internal/all-users (см. выше).
    /// </summary>
    [HttpGet("admin-users")]
    public async Task<IActionResult> GetAdminUsers(CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue("X-Internal-Key", out var supplied)
            || string.IsNullOrWhiteSpace(_internalKey)
            || !string.Equals(supplied, _internalKey, StringComparison.Ordinal))
        {
            return Unauthorized(new { message = "Недопустимый внутренний ключ." });
        }

        var candidates = await _context.Users
            .AsNoTracking()
            .Where(user => user.IsEmailConfirmed)
            .ToListAsync(cancellationToken);

        var admins = candidates
            .Where(user => _adminAccessService.IsAdminOrOwner(user))
            .Select(user => new { userId = user.Id.ToString(), email = user.Email })
            .ToList();

        return Ok(new { users = admins, total = admins.Count });
    }

    /// <summary>
    /// Возвращает userId подтверждённого пользователя по email. Используется точечной
    /// миграцией legacy-премиума (/api/admin/migrate-legacy-premium/assign) для резолва
    /// email -> userId. Если пользователя нет или email не подтверждён — 404.
    /// </summary>
    [HttpGet("user-by-email")]
    public async Task<IActionResult> GetUserByEmail(
        [FromQuery] string? email,
        CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue("X-Internal-Key", out var supplied)
            || string.IsNullOrWhiteSpace(_internalKey)
            || !string.Equals(supplied, _internalKey, StringComparison.Ordinal))
        {
            return Unauthorized(new { message = "Недопустимый внутренний ключ." });
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { message = "Email обязателен." });
        }

        var normalized = email.Trim().ToLowerInvariant();
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == normalized && u.IsEmailConfirmed, cancellationToken);

        if (user == null)
        {
            return NotFound(new { message = "Пользователь не найден или email не подтверждён." });
        }

        return Ok(new { userId = user.Id.ToString(), email = user.Email });
    }
}

public class RecordBotChatRequest
{
    public Guid UserId { get; set; }

    /// <summary>Имя/идентификатор персонажа-бота (characterId). Не текст сообщения.</summary>
    public string? CharacterId { get; set; }
}
