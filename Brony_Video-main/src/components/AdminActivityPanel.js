import React, { useCallback, useEffect, useState } from "react";
import { Activity as ActivityIcon, ArrowLeft, RefreshCw, Users } from "lucide-react";
import { apiFetch } from "../auth/api";

const ACTIVITY_LABELS = {
  video_watch: "Просмотр серии",
  bot_chat: "Общение с ботом",
  vpn_click: "Клик по VPN",
  forum_view: "Просмотр темы",
  forum_post: "Написал в теме",
  news_view: "Просмотр новости"
};

// Человекочитаемое время (дата + часы/минуты) в локальном часовом поясе.
function formatActivityTime(timestamp) {
  if (!timestamp) {
    return "";
  }
  const date = new Date(timestamp);
  if (Number.isNaN(date.getTime())) {
    return "";
  }
  return date.toLocaleString("ru-RU", {
    day: "numeric",
    month: "short",
    hour: "2-digit",
    minute: "2-digit"
  });
}

function normalizeActivity(raw) {
  if (!raw || typeof raw !== "object") {
    return null;
  }
  return {
    userId: raw.userId ?? raw.UserId ?? "",
    username: raw.username ?? raw.Username ?? null,
    email: raw.email ?? raw.Email ?? "",
    type: raw.type ?? raw.Type ?? "",
    details: raw.details ?? raw.Details ?? null,
    timestamp: raw.timestamp ?? raw.Timestamp ?? null
  };
}

export default function AdminActivityPanel({ onBack }) {
  const [activities, setActivities] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const loadActivity = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const response = await apiFetch("/api/admin/activity/week");
      const raw = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(raw.message || "Не удалось загрузить активность.");
      }

      const list = Array.isArray(raw.activities ?? raw.Activities)
        ? raw.activities ?? raw.Activities
        : [];
      setActivities(list.map(normalizeActivity).filter(Boolean));
    } catch (loadError) {
      setError(loadError.message || "Не удалось загрузить активность.");
      setActivities([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadActivity();
  }, [loadActivity]);

  // Группировка по пользователю (аккаунт → список действий за неделю).
  const grouped = activities.reduce((acc, activity) => {
    const key = activity.userId || "unknown";
    if (!acc[key]) {
      acc[key] = { userId: key, username: activity.username, email: activity.email, items: [] };
    }
    acc[key].items.push(activity);
    return acc;
  }, {});

  const groups = Object.values(grouped).map((group) => ({
    ...group,
    items: group.items.slice().sort((a, b) => new Date(b.timestamp) - new Date(a.timestamp))
  }));

  // Сортируем пользователей по времени последнего действия (свежие сверху).
  groups.sort((a, b) => {
    const aLast = new Date(a.items[0]?.timestamp || 0);
    const bLast = new Date(b.items[0]?.timestamp || 0);
    return bLast - aLast;
  });

  return (
    <article className="admin-card admin-card--activity">
      <header className="admin-activity-page-header">
        <div className="admin-activity-page-heading">
          <h2>
            <ActivityIcon size={20} aria-hidden="true" />
            <span>Активность за последние 7 дней</span>
          </h2>
          <p className="muted">
            Действия всех пользователей за последнюю неделю, сгруппированы по аккаунту.
          </p>
        </div>
        <div className="admin-activity-page-actions">
          {onBack ? (
            <button type="button" className="secondary-btn" onClick={onBack}>
              <ArrowLeft size={14} />
              <span>Назад</span>
            </button>
          ) : null}
          <button type="button" className="secondary-btn" onClick={loadActivity} disabled={loading}>
            <RefreshCw size={14} />
            <span>Обновить</span>
          </button>
        </div>
      </header>

      {error ? (
        <p className="admin-message admin-message--error" role="alert">
          {error}
        </p>
      ) : null}

      {loading ? (
        <p className="muted">Загрузка активности…</p>
      ) : groups.length === 0 ? (
        <div className="admin-activity-empty">
          <Users size={28} aria-hidden="true" />
          <p className="muted">За последние 7 дней активности нет.</p>
        </div>
      ) : (
        <ul className="admin-activity-user-list">
          {groups.map((group) => (
            <li key={group.userId} className="admin-activity-user-card">
              <div className="admin-activity-user-head">
                <strong className="admin-activity-username">
                  {group.username ? `@${group.username}` : group.email ? group.email : "Пользователь"}
                </strong>
                <span className="muted">{group.items.length} действ.</span>
              </div>
              <ul className="admin-activity-list">
                {group.items.map((activity, index) => (
                  <li key={index} className="admin-activity-item">
                    <div className="admin-activity-main">
                      <span className="admin-activity-type">
                        {ACTIVITY_LABELS[activity.type] || activity.type}
                      </span>
                      {activity.details ? (
                        <span className="admin-activity-details">{activity.details}</span>
                      ) : null}
                    </div>
                    <span className="admin-activity-time muted">
                      {formatActivityTime(activity.timestamp)}
                    </span>
                  </li>
                ))}
              </ul>
            </li>
          ))}
        </ul>
      )}
    </article>
  );
}
