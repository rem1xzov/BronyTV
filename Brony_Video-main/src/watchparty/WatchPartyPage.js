import React, { useCallback, useEffect, useRef, useState } from "react";
import * as signalR from "@microsoft/signalr";
import {
  CalendarClock,
  Pause,
  Play,
  Radio,
  Send,
  SkipForward,
  Square
} from "lucide-react";
import { apiFetch } from "../auth/api";
import { useAuth } from "../auth/AuthContext";

const HUB_URL = "/hubs/watchparty";

const formatDateTime = (value) => {
  if (!value) return "";
  const d = new Date(value);
  return d.toLocaleString("ru-RU", {
    day: "2-digit",
    month: "long",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit"
  });
};

function WatchPartyStyles() {
  return (
    <style>{`
      .wp-page { max-width: 1080px; margin: 0 auto; padding: 24px; display: flex; flex-direction: column; gap: 20px; }
      .wp-head { display: flex; align-items: center; gap: 12px; }
      .wp-head h1 { margin: 0; }
      .wp-announcement {
        display: flex; flex-direction: column; align-items: center; gap: 10px;
        padding: 28px; border-radius: 20px; text-align: center;
        background: var(--bg-soft, #faecff);
        border: 1px solid var(--border-soft, rgba(168,85,247,.16));
      }
      .wp-announcement .wp-date { font-size: 1.15rem; font-weight: 700; color: var(--accent-strong, #db2777); }
      .wp-live { display: grid; grid-template-columns: 1fr 360px; gap: 16px; align-items: start; }
      @media (max-width: 820px) { .wp-live { grid-template-columns: 1fr; } }
      .wp-player video { width: 100%; border-radius: 16px; background: #000; }
      .wp-chat {
        display: flex; flex-direction: column; height: 480px;
        border-radius: 16px; border: 1px solid var(--border-soft, rgba(168,85,247,.16));
        background: var(--bg-card, #fff); overflow: hidden;
      }
      .wp-chat-messages { flex: 1; overflow-y: auto; padding: 12px; display: flex; flex-direction: column; gap: 8px; }
      .wp-msg { font-size: 0.92rem; line-height: 1.35; }
      .wp-msg-user { color: var(--text-main, #3a0b3c); }
      .wp-msg-user .wp-msg-name { font-weight: 700; color: var(--accent-strong, #db2777); margin-right: 6px; }
      .wp-msg-system { font-style: italic; color: var(--text-muted, #7b4b82); }
      .wp-chat-input { display: flex; gap: 8px; padding: 10px; border-top: 1px solid var(--border-soft, rgba(168,85,247,.16)); }
      .wp-chat-input input { flex: 1; min-width: 0; padding: 10px 12px; border-radius: 12px; border: 1px solid var(--border-soft, rgba(168,85,247,.16)); background: var(--bg-soft, #faecff); color: var(--text-main, #3a0b3c); }
      .wp-admin {
        display: flex; flex-direction: column; gap: 12px; padding: 18px;
        border-radius: 16px; background: var(--bg-soft, #faecff);
        border: 1px solid var(--border-soft, rgba(168,85,247,.16));
      }
      .wp-admin h3 { margin: 0; }
      .wp-admin-row { display: flex; flex-wrap: wrap; gap: 10px; align-items: center; }
      .wp-admin select, .wp-admin input[type="datetime-local"], .wp-admin input[type="number"] {
        padding: 8px 10px; border-radius: 10px; border: 1px solid var(--border-soft, rgba(168,85,247,.16));
        background: var(--bg-card, #fff); color: var(--text-main, #3a0b3c);
      }
      .wp-list { display: flex; flex-direction: column; gap: 8px; }
      .wp-list-item {
        display: flex; justify-content: space-between; gap: 12px; padding: 12px 14px;
        border-radius: 12px; background: var(--bg-soft, #faecff);
        border: 1px solid var(--border-soft, rgba(168,85,247,.16));
      }
      .wp-muted { color: var(--text-muted, #7b4b82); }
    `}</style>
  );
}

export default function WatchPartyPage() {
  const { user, isAuthenticated } = useAuth();
  const isAdmin = !!user && (
    user.isOwner || user.isPlatformAdmin ||
    user.platformRole === "Admin" || user.platformRole === "Owner"
  );

  const [announcements, setAnnouncements] = useState([]);
  const [sync, setSync] = useState(null);
  const [messages, setMessages] = useState([]);
  const [draft, setDraft] = useState("");
  const [connected, setConnected] = useState(false);

  const [seasons, setSeasons] = useState([]);
  const [videos, setVideos] = useState([]);
  const [selectedVideoId, setSelectedVideoId] = useState("");
  const [selectedSeasonId, setSelectedSeasonId] = useState("");
  const [scheduleAt, setScheduleAt] = useState("");
  const [seekSeconds, setSeekSeconds] = useState("");

  const videoRef = useRef(null);
  const messagesEndRef = useRef(null);
  const connectionRef = useRef(null);
  const syncRef = useRef(null);

  const loadAnnouncements = useCallback(async () => {
    try {
      const response = await apiFetch("/api/stream/announcements");
      if (response.ok) {
        setAnnouncements(await response.json());
      }
    } catch {
      // ignore
    }
  }, []);

  useEffect(() => {
    loadAnnouncements();
  }, [loadAnnouncements]);

  // Каталог для админа: список сезонов.
  useEffect(() => {
    if (!isAdmin) return;
    (async () => {
      try {
        const response = await apiFetch("/api/season");
        if (response.ok) setSeasons(await response.json());
      } catch {
        // ignore
      }
    })();
  }, [isAdmin]);

  // Видео выбранного сезона/категории.
  useEffect(() => {
    if (!isAdmin || !selectedSeasonId) {
      setVideos([]);
      return;
    }
    (async () => {
      try {
        const seasonNumber = Number(selectedSeasonId);
        const endpoint = seasonNumber === 10
          ? "/api/video/film"
          : seasonNumber === 11
            ? "/api/video/equestria-girls"
            : `/api/video/season/${seasonNumber}`;
        const response = await apiFetch(endpoint);
        if (response.ok) setVideos(await response.json());
      } catch {
        // ignore
      }
    })();
  }, [isAdmin, selectedSeasonId]);

  const applySync = useCallback((next) => {
    syncRef.current = next;
    setSync(next);
    const video = videoRef.current;
    if (!video) return;
    if (!next.isLive) {
      video.pause();
      return;
    }
    if (video.dataset.videoId !== String(next.videoId)) {
      video.dataset.videoId = String(next.videoId);
      video.src = next.videoUrl;
    }
    const offsetMs = Date.now() - new Date(next.serverTimeUtc).getTime();
    const serverNowMs = Date.now() - offsetMs;
    const startedMs = next.startedAtUtc ? new Date(next.startedAtUtc).getTime() : 0;
    const target = next.isPaused
      ? next.pausedAtSeconds
      : Math.max(0, (serverNowMs - startedMs) / 1000);
    video.currentTime = target;
    if (next.isPaused) video.pause();
    else video.play().catch(() => {});
  }, []);

  // Подключение к хабу.
  useEffect(() => {
    if (!isAuthenticated) return undefined;
    let disposed = false;
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL, { withCredentials: true })
      .withAutomaticReconnect()
      .build();
    connectionRef.current = connection;

    connection.on("ReceiveSyncState", (state) => {
      if (!disposed) applySync(state);
    });
    connection.on("ReceiveChatMessage", (msg) => {
      if (!disposed) setMessages((prev) => [...prev.slice(-199), msg]);
    });
    connection.on("ReceiveSystemMessage", (text) => {
      if (disposed) return;
      setMessages((prev) => [
        ...prev.slice(-199),
        { username: "", text, sentAtUtc: new Date().toISOString(), isSystem: true }
      ]);
    });
    connection.on("StreamEnded", () => {
      if (disposed) return;
      setSync(null);
      setMessages((prev) => [
        ...prev.slice(-199),
        { username: "", text: "Трансляция завершена.", sentAtUtc: new Date().toISOString(), isSystem: true }
      ]);
    });

    connection.start()
      .then(() => {
        setConnected(true);
        return connection.invoke("JoinWatchParty");
      })
      .catch(() => setConnected(false));

    return () => {
      disposed = true;
      connection.stop().catch(() => {});
    };
  }, [isAuthenticated, applySync]);

  // Периодическая коррекция дрифта (раз в ~7 секунд).
  useEffect(() => {
    const id = setInterval(() => {
      const s = syncRef.current;
      const video = videoRef.current;
      if (!s || !s.isLive || !video || video.paused) return;
      const offsetMs = Date.now() - new Date(s.serverTimeUtc).getTime();
      const serverNowMs = Date.now() - offsetMs;
      const startedMs = s.startedAtUtc ? new Date(s.startedAtUtc).getTime() : 0;
      const target = Math.max(0, (serverNowMs - startedMs) / 1000);
      const drift = target - video.currentTime;
      if (Math.abs(drift) > 1.5) {
        video.currentTime = target;
        video.playbackRate = 1;
      } else if (drift > 0.3) {
        video.playbackRate = 1.06;
        window.setTimeout(() => { if (videoRef.current) videoRef.current.playbackRate = 1; }, 1200);
      } else if (drift < -0.3) {
        video.playbackRate = 0.94;
        window.setTimeout(() => { if (videoRef.current) videoRef.current.playbackRate = 1; }, 1200);
      }
    }, 7000);
    return () => clearInterval(id);
  }, []);

  // Автоскролл чата.
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages]);

  const sendMessage = async (event) => {
    event.preventDefault();
    const text = draft.trim();
    if (!text || !connectionRef.current) return;
    setDraft("");
    try {
      await connectionRef.current.invoke("SendChatMessage", text);
    } catch {
      // ignore
    }
  };

  const startStream = async () => {
    if (!selectedVideoId || !connectionRef.current) return;
    try {
      await connectionRef.current.invoke("StartStream", selectedVideoId, null);
    } catch (error) {
      console.error(error);
    }
  };

  const pauseStream = () => connectionRef.current?.invoke("Pause").catch(() => {});
  const resumeStream = () => connectionRef.current?.invoke("Resume").catch(() => {});
  const seekStream = () => {
    const seconds = Number(seekSeconds);
    if (!Number.isFinite(seconds) || seconds < 0) return;
    connectionRef.current?.invoke("Seek", seconds).catch(() => {});
  };
  const endStream = () => connectionRef.current?.invoke("EndStream").catch(() => {});

  const createAnnouncement = async (event) => {
    event.preventDefault();
    if (!selectedVideoId || !scheduleAt) return;
    const scheduledAtUtc = new Date(scheduleAt).toISOString();
    try {
      await apiFetch("/api/stream/announcements", {
        method: "POST",
        body: JSON.stringify({ videoId: selectedVideoId, scheduledAtUtc })
      });
      setScheduleAt("");
      await loadAnnouncements();
    } catch {
      // ignore
    }
  };

  const cancelAnnouncement = async (id) => {
    try {
      await apiFetch(`/api/stream/announcements/${id}/cancel`, { method: "POST" });
      await loadAnnouncements();
    } catch {
      // ignore
    }
  };

  const now = Date.now();
  const upcoming = announcements
    .filter((a) => a.status === "scheduled" && new Date(a.scheduledAtUtc).getTime() >= now)
    .sort((a, b) => new Date(a.scheduledAtUtc) - new Date(b.scheduledAtUtc));
  const past = announcements
    .filter((a) => a.status !== "scheduled" || new Date(a.scheduledAtUtc).getTime() < now)
    .sort((a, b) => new Date(b.scheduledAtUtc) - new Date(a.scheduledAtUtc));

  const nextAnnouncement = upcoming[0];
  const isLive = sync && sync.isLive;

  return (
    <div className="wp-page">
      <WatchPartyStyles />
      <div className="wp-head">
        <Radio size={24} />
        <h1>Стримы</h1>
      </div>

      {isLive ? (
        <div className="wp-live">
          <div className="wp-player">
            <video ref={videoRef} controls playsInline />
            <p className="wp-muted">{sync.videoTitle || "Трансляция"}</p>
          </div>
          <div className="wp-chat">
            <div className="wp-chat-messages">
              {messages.map((msg, index) => (
                <div key={index} className={`wp-msg ${msg.isSystem ? "wp-msg-system" : "wp-msg-user"}`}>
                  {msg.isSystem ? (
                    msg.text
                  ) : (
                    <>
                      <span className="wp-msg-name">{msg.username}</span>
                      {msg.text}
                    </>
                  )}
                </div>
              ))}
              <div ref={messagesEndRef} />
            </div>
            {isAuthenticated ? (
              <form className="wp-chat-input" onSubmit={sendMessage}>
                <input
                  type="text"
                  value={draft}
                  onChange={(event) => setDraft(event.target.value)}
                  placeholder="Сообщение…"
                  maxLength={500}
                />
                <button type="submit" className="primary-btn">
                  <Send size={16} />
                </button>
              </form>
            ) : (
              <div className="wp-chat-input wp-muted">Войдите, чтобы писать в чат.</div>
            )}
          </div>
        </div>
      ) : nextAnnouncement ? (
        <div className="wp-announcement">
          <CalendarClock size={32} />
          <h2>{nextAnnouncement.videoTitle || "Стрим"}</h2>
          <div className="wp-date">{formatDateTime(nextAnnouncement.scheduledAtUtc)}</div>
          {!isAuthenticated && <p className="wp-muted">Войдите, чтобы смотреть и общаться в чате.</p>}
        </div>
      ) : (
        <div className="wp-announcement">
          <Radio size={32} />
          <p className="wp-muted">Пока нет запланированных стримов.</p>
        </div>
      )}

      {isAdmin && (
        <div className="wp-admin">
          <h3>Управление стримом</h3>

          <div className="wp-admin-row">
            <select value={selectedSeasonId} onChange={(event) => setSelectedSeasonId(event.target.value)}>
              <option value="">Сезон / категория…</option>
              {seasons.map((season) => (
                <option key={season.id ?? season.number} value={season.number}>
                  {season.number === 10 ? "Фильм MLP" : season.number === 11 ? "Equestria Girls" : `Сезон ${season.number}`}
                </option>
              ))}
            </select>
            <select value={selectedVideoId} onChange={(event) => setSelectedVideoId(event.target.value)}>
              <option value="">Видео…</option>
              {videos.map((video) => (
                <option key={video.id} value={video.id}>
                  {video.episodeNumber ? `${video.episodeNumber}. ` : ""}{video.title}
                </option>
              ))}
            </select>
          </div>

          <div className="wp-admin-row">
            <button type="button" className="primary-btn" onClick={startStream} disabled={!connected || !selectedVideoId}>
              <Play size={16} /> Запустить сейчас
            </button>
            {isLive && (
              <>
                <button type="button" className="secondary-btn" onClick={pauseStream} disabled={sync?.isPaused}>
                  <Pause size={16} /> Пауза
                </button>
                <button type="button" className="secondary-btn" onClick={resumeStream} disabled={!sync?.isPaused}>
                  <Play size={16} /> Продолжить
                </button>
                <input
                  type="number"
                  min="0"
                  step="1"
                  value={seekSeconds}
                  onChange={(event) => setSeekSeconds(event.target.value)}
                  placeholder="Секунды"
                />
                <button type="button" className="secondary-btn" onClick={seekStream}>
                  <SkipForward size={16} /> Перемотать
                </button>
                <button type="button" className="secondary-btn" onClick={endStream}>
                  <Square size={16} /> Завершить
                </button>
              </>
            )}
          </div>

          <form className="wp-admin-row" onSubmit={createAnnouncement}>
            <input type="datetime-local" value={scheduleAt} onChange={(event) => setScheduleAt(event.target.value)} />
            <button type="submit" className="secondary-btn" disabled={!selectedVideoId || !scheduleAt}>
              <CalendarClock size={16} /> Запланировать анонс
            </button>
          </form>
        </div>
      )}

      <div className="wp-list">
        <h3>Ближайшие</h3>
        {upcoming.length === 0 && <p className="wp-muted">Нет запланированных стримов.</p>}
        {upcoming.map((a) => (
          <div className="wp-list-item" key={a.id}>
            <span>
              <strong>{a.videoTitle || "Стрим"}</strong>
              <span className="wp-muted">{a.seasonTitle ? ` · ${a.seasonTitle}` : ""}</span>
            </span>
            <span>{formatDateTime(a.scheduledAtUtc)}</span>
            {isAdmin && (
              <button type="button" className="secondary-btn small" onClick={() => cancelAnnouncement(a.id)}>
                Отменить
              </button>
            )}
          </div>
        ))}
      </div>

      <div className="wp-list">
        <h3>Прошедшие</h3>
        {past.length === 0 && <p className="wp-muted">Прошедших стримов пока нет.</p>}
        {past.map((a) => (
          <div className="wp-list-item" key={a.id}>
            <span>
              <strong>{a.videoTitle || "Стрим"}</strong>
              <span className="wp-muted">
                {a.status === "completed" ? " · состоялся" : a.status === "cancelled" ? " · отменён" : ""}
              </span>
            </span>
            <span>{formatDateTime(a.scheduledAtUtc)}</span>
          </div>
        ))}
      </div>
    </div>
  );
}
