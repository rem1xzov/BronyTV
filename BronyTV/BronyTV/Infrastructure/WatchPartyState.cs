using System;
using BronyTV.Contract;

namespace BronyTV.Infrastructure;

/// <summary>
/// Singleton-состояние текущего эфира WatchParty. Живёт в памяти (не в БД) — эфемерное
/// состояние, актуальное только пока идёт трансляция. Потокобезопасность через lock.
/// Формула позиции: <c>position = now - StartedAtUtc</c>; пауза/перемотка пересчитывают
/// <c>StartedAtUtc</c>/<c>PausedAtSeconds</c> так, чтобы формула оставалась верной.
/// </summary>
public sealed class WatchPartyState
{
    private readonly object _lock = new();

    private Guid? _videoId;
    private string? _videoUrl;
    private string? _videoTitle;
    private DateTimeOffset? _startedAtUtc;
    private bool _isPaused;
    private double _pausedAtSeconds;
    private Guid? _announcementId;
    private int _viewerCount;

    public WatchPartySyncState Snapshot()
    {
        lock (_lock)
        {
            var now = DateTimeOffset.UtcNow;
            double position;
            if (!_startedAtUtc.HasValue)
            {
                position = 0;
            }
            else if (_isPaused)
            {
                position = _pausedAtSeconds;
            }
            else
            {
                position = Math.Max(0, (now - _startedAtUtc.Value).TotalSeconds);
            }

            return new WatchPartySyncState
            {
                IsLive = _startedAtUtc.HasValue,
                VideoId = _videoId,
                VideoUrl = _videoUrl,
                VideoTitle = _videoTitle,
                IsPaused = _isPaused,
                StartedAtUtc = _startedAtUtc,
                PausedAtSeconds = _pausedAtSeconds,
                PositionSeconds = position,
                ServerTimeUtc = now
            };
        }
    }

    public bool IsLive { get { lock (_lock) return _startedAtUtc.HasValue; } }

    public void Start(Guid videoId, string videoUrl, string videoTitle, Guid? announcementId)
    {
        lock (_lock)
        {
            _videoId = videoId;
            _videoUrl = videoUrl;
            _videoTitle = videoTitle;
            _startedAtUtc = DateTimeOffset.UtcNow;
            _isPaused = false;
            _pausedAtSeconds = 0;
            _announcementId = announcementId;
        }
    }

    public void Pause()
    {
        lock (_lock)
        {
            if (!_startedAtUtc.HasValue || _isPaused)
            {
                return;
            }

            _pausedAtSeconds = Math.Max(0, (DateTimeOffset.UtcNow - _startedAtUtc.Value).TotalSeconds);
            _isPaused = true;
        }
    }

    public void Resume()
    {
        lock (_lock)
        {
            if (!_startedAtUtc.HasValue || !_isPaused)
            {
                return;
            }

            _startedAtUtc = DateTimeOffset.UtcNow - TimeSpan.FromSeconds(_pausedAtSeconds);
            _isPaused = false;
        }
    }

    public void Seek(double seconds)
    {
        lock (_lock)
        {
            if (!_startedAtUtc.HasValue)
            {
                return;
            }

            var clamped = Math.Max(0, seconds);
            if (_isPaused)
            {
                _pausedAtSeconds = clamped;
            }
            else
            {
                _startedAtUtc = DateTimeOffset.UtcNow - TimeSpan.FromSeconds(clamped);
            }
        }
    }

    /// <summary>Завершает эфир и возвращает id анонса (если стрим был привязан к анонсу).</summary>
    public Guid? End()
    {
        lock (_lock)
        {
            var announcementId = _announcementId;
            _videoId = null;
            _videoUrl = null;
            _videoTitle = null;
            _startedAtUtc = null;
            _isPaused = false;
            _pausedAtSeconds = 0;
            _announcementId = null;
            return announcementId;
        }
    }

    public void IncrementViewers() { lock (_lock) { _viewerCount++; } }

    public void DecrementViewers() { lock (_lock) { if (_viewerCount > 0) _viewerCount--; } }

    public int ViewerCount { get { lock (_lock) return _viewerCount; } }
}
