using System;
using System.Security.Claims;
using System.Threading.Tasks;
using BronyTV.Contract;
using BronyTV.Infrastructure;
using BronyTV.Repository;
using BronyTV.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BronyTV.Hubs;

/// <summary>
/// SignalR-хаб совместного просмотра (WatchParty). Синхронизирует позицию VOD-плеера
/// и реалтайм-чат. Управление трансляцией — только Admin, чат — любой авторизованный.
/// </summary>
[Authorize]
public class StreamHub : Hub
{
    private const string WatchPartyGroup = "watchparty";

    private readonly WatchPartyState _state;
    private readonly IVideoService _videoService;
    private readonly IUserRepository _userRepository;
    private readonly IStreamAnnouncementRepository _announcementRepository;

    public StreamHub(
        WatchPartyState state,
        IVideoService videoService,
        IUserRepository userRepository,
        IStreamAnnouncementRepository announcementRepository)
    {
        _state = state;
        _videoService = videoService;
        _userRepository = userRepository;
        _announcementRepository = announcementRepository;
    }

    public async Task JoinWatchParty()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, WatchPartyGroup);

        // Late joiner сразу получает текущее состояние (видео + позиция + серверное время).
        await Clients.Caller.SendAsync("ReceiveSyncState", _state.Snapshot());

        var username = await ResolveUsernameAsync();
        if (!string.IsNullOrWhiteSpace(username))
        {
            await Clients.OthersInGroup(WatchPartyGroup)
                .SendAsync("ReceiveSystemMessage", $"К нам присоединился @{username}");
        }

        _state.IncrementViewers();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _state.DecrementViewers();
        await base.OnDisconnectedAsync(exception);
    }

    [Authorize(Roles = "Admin")]
    public async Task StartStream(Guid videoId, Guid? announcementId = null)
    {
        var video = await _videoService.GetVideoByIdAsync(videoId);
        if (video == null)
        {
            throw new HubException("Видео не найдено.");
        }

        _state.Start(video.Id, video.FilePath, video.Title, announcementId);
        await Clients.Group(WatchPartyGroup).SendAsync("ReceiveSyncState", _state.Snapshot());
    }

    [Authorize(Roles = "Admin")]
    public async Task Pause()
    {
        _state.Pause();
        await BroadcastSyncAsync();
    }

    [Authorize(Roles = "Admin")]
    public async Task Resume()
    {
        _state.Resume();
        await BroadcastSyncAsync();
    }

    [Authorize(Roles = "Admin")]
    public async Task Seek(double seconds)
    {
        _state.Seek(seconds);
        await BroadcastSyncAsync();
    }

    [Authorize(Roles = "Admin")]
    public async Task EndStream()
    {
        var announcementId = _state.End();
        await Clients.Group(WatchPartyGroup).SendAsync("StreamEnded");

        if (announcementId.HasValue)
        {
            await _announcementRepository.MarkCompletedAsync(announcementId.Value);
        }
    }

    public async Task SendChatMessage(string text)
    {
        var clean = text?.Trim();
        if (string.IsNullOrEmpty(clean) || clean.Length > 500)
        {
            return;
        }

        var username = await ResolveUsernameAsync();
        var message = new WatchPartyChatMessage
        {
            Username = string.IsNullOrWhiteSpace(username) ? "аноним" : username,
            Text = clean,
            SentAtUtc = DateTimeOffset.UtcNow,
            IsSystem = false
        };

        await Clients.Group(WatchPartyGroup).SendAsync("ReceiveChatMessage", message);
    }

    private Task BroadcastSyncAsync()
        => Clients.Group(WatchPartyGroup).SendAsync("ReceiveSyncState", _state.Snapshot());

    private async Task<string?> ResolveUsernameAsync()
    {
        var userIdValue = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return null;
        }

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(user.Username) ? user.Email : user.Username;
    }
}
