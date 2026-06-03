import React, { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { ArrowLeft, Home, Upload, Users } from "lucide-react";
import { useAuth } from "../auth/AuthContext";
import { isPlatformAdmin } from "../auth/adminAccess";
import { apiFetch, apiUpload } from "../auth/api";

function normalizeSeason(raw) {
  if (!raw || typeof raw !== "object") {
    return null;
  }

  const id = raw.id ?? raw.Id;
  const number = raw.number ?? raw.Number;
  const title = raw.title ?? raw.Title ?? "";

  if (!id || number == null) {
    return null;
  }

  return { id, number, title };
}

function normalizeAdminUser(raw) {
  if (!raw || typeof raw !== "object") {
    return null;
  }

  const id = raw.id ?? raw.Id;
  if (!id) {
    return null;
  }

  return {
    id,
    email: raw.email ?? raw.Email ?? "",
    username: raw.username ?? raw.Username ?? null,
    race: raw.race ?? raw.Race ?? "",
    isBannedFromCommenting: Boolean(
      raw.isBannedFromCommenting ?? raw.IsBannedFromCommenting ?? false
    ),
    createdAtUtc: raw.createdAtUtc ?? raw.CreatedAtUtc ?? null
  };
}

export default function AdminPanelPage() {
  const navigate = useNavigate();
  const { user, loading, refreshUser } = useAuth();
  const [seasons, setSeasons] = useState([]);
  const [seasonsLoading, setSeasonsLoading] = useState(true);
  const [seasonsError, setSeasonsError] = useState("");
  const [seasonId, setSeasonId] = useState("");
  const [title, setTitle] = useState("");
  const [episodeNumber, setEpisodeNumber] = useState("1");
  const [description, setDescription] = useState("");
  const [videoFile, setVideoFile] = useState(null);
  const [previewFile, setPreviewFile] = useState(null);
  const [uploadError, setUploadError] = useState("");
  const [uploadSuccess, setUploadSuccess] = useState("");
  const [uploading, setUploading] = useState(false);
  const [activeTab, setActiveTab] = useState("upload");
  const [userQuery, setUserQuery] = useState("");
  const [userResults, setUserResults] = useState([]);
  const [userSearchLoading, setUserSearchLoading] = useState(false);
  const [userActionError, setUserActionError] = useState("");
  const [userActionMessage, setUserActionMessage] = useState("");
  const [userActionId, setUserActionId] = useState(null);

  useEffect(() => {
    if (loading) {
      return;
    }

    if (!isPlatformAdmin(user)) {
      navigate("/", { replace: true });
    }
  }, [loading, navigate, user]);

  useEffect(() => {
    let cancelled = false;

    const loadSeasons = async () => {
      setSeasonsLoading(true);
      setSeasonsError("");

      try {
        const response = await apiFetch("/season");
        if (!response.ok) {
          throw new Error("Не удалось загрузить сезоны.");
        }

        const payload = await response.json();
        const list = Array.isArray(payload) ? payload : [];
        const normalized = list.map(normalizeSeason).filter(Boolean);
        normalized.sort((a, b) => a.number - b.number);

        if (!cancelled) {
          setSeasons(normalized);
          if (normalized.length > 0) {
            setSeasonId((current) => current || normalized[0].id);
          }
        }
      } catch (error) {
        if (!cancelled) {
          setSeasonsError(error.message || "Не удалось загрузить сезоны.");
        }
      } finally {
        if (!cancelled) {
          setSeasonsLoading(false);
        }
      }
    };

    loadSeasons();

    return () => {
      cancelled = true;
    };
  }, []);

  const handleUpload = async (event) => {
    event.preventDefault();
    setUploadError("");
    setUploadSuccess("");

    if (!seasonId) {
      setUploadError("Выберите сезон.");
      return;
    }

    if (!title.trim()) {
      setUploadError("Укажите название серии.");
      return;
    }

    const episode = Number.parseInt(episodeNumber, 10);
    if (!Number.isFinite(episode) || episode < 1) {
      setUploadError("Номер серии должен быть не меньше 1.");
      return;
    }

    if (!description.trim()) {
      setUploadError("Добавьте описание.");
      return;
    }

    if (!videoFile) {
      setUploadError("Выберите видеофайл.");
      return;
    }

    setUploading(true);

    try {
      await refreshUser();

      const formData = new FormData();
      formData.append("Title", title.trim());
      formData.append("EpisodeNumber", String(episode));
      formData.append("SeasonId", seasonId);
      formData.append("Description", description.trim());
      formData.append("VideoFile", videoFile);
      if (previewFile) {
        formData.append("PreviewFile", previewFile);
      }

      const response = await apiUpload("/video/upload", formData);
      const raw = await response.json().catch(() => ({}));

      if (!response.ok) {
        const message =
          raw.title ||
          raw.detail ||
          raw.message ||
          (typeof raw === "string" ? raw : null) ||
          "Не удалось загрузить видео.";
        throw new Error(message);
      }

      setUploadSuccess("Видео успешно загружено и добавлено в каталог.");
      setTitle("");
      setEpisodeNumber(String(episode + 1));
      setDescription("");
      setVideoFile(null);
      setPreviewFile(null);
      event.target.reset();
    } catch (error) {
      setUploadError(error.message || "Не удалось загрузить видео.");
    } finally {
      setUploading(false);
    }
  };

  const handleUserSearch = async (event) => {
    event.preventDefault();
    setUserActionError("");
    setUserActionMessage("");

    const query = userQuery.trim();
    if (!query) {
      setUserResults([]);
      return;
    }

    setUserSearchLoading(true);

    try {
      const response = await apiFetch(
        `/admin/users/search?query=${encodeURIComponent(query)}`
      );
      const raw = await response.json().catch(() => []);
      if (!response.ok) {
        const message = raw?.message || "Не удалось найти пользователей.";
        throw new Error(message);
      }

      const list = Array.isArray(raw) ? raw : [];
      setUserResults(list.map(normalizeAdminUser).filter(Boolean));
    } catch (error) {
      setUserActionError(error.message || "Не удалось найти пользователей.");
      setUserResults([]);
    } finally {
      setUserSearchLoading(false);
    }
  };

  const handleDeleteUser = async (targetUser) => {
    const label = targetUser.username ? `@${targetUser.username}` : targetUser.email;
    const confirmed = window.confirm(
      `Полностью удалить пользователя ${label}? Это действие необратимо.`
    );
    if (!confirmed) {
      return;
    }

    setUserActionId(targetUser.id);
    setUserActionError("");
    setUserActionMessage("");

    try {
      const response = await apiFetch(`/admin/users/${targetUser.id}`, { method: "DELETE" });
      if (!response.ok) {
        const raw = await response.json().catch(() => ({}));
        throw new Error(raw.message || "Не удалось удалить пользователя.");
      }

      setUserResults((prev) => prev.filter((item) => item.id !== targetUser.id));
      setUserActionMessage("Пользователь удалён.");
    } catch (error) {
      setUserActionError(error.message || "Не удалось удалить пользователя.");
    } finally {
      setUserActionId(null);
    }
  };

  const handleToggleBan = async (targetUser) => {
    setUserActionId(targetUser.id);
    setUserActionError("");
    setUserActionMessage("");

    try {
      const response = await apiFetch(`/admin/users/${targetUser.id}/toggle-comment-ban`, {
        method: "PUT"
      });
      const raw = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(raw.message || "Не удалось изменить статус бана.");
      }

      const updated = normalizeAdminUser(raw);
      if (updated) {
        setUserResults((prev) =>
          prev.map((item) => (item.id === updated.id ? updated : item))
        );
        setUserActionMessage(
          updated.isBannedFromCommenting
            ? "Пользователь забанен в комментариях."
            : "Пользователь разбанен в комментариях."
        );
      }
    } catch (error) {
      setUserActionError(error.message || "Не удалось изменить статус бана.");
    } finally {
      setUserActionId(null);
    }
  };

  if (loading || !isPlatformAdmin(user)) {
    return (
      <section className="admin-panel admin-panel--loading">
        <p className="muted">Загрузка админ-панели…</p>
      </section>
    );
  }

  return (
    <section className="admin-panel">
      <header className="admin-panel-header">
        <div className="admin-panel-heading">
          <h1>Админ-панель</h1>
          <p className="admin-panel-subtitle">Загрузка серий, пользователи и модерация</p>
        </div>
        <div className="admin-panel-nav">
          <Link className="secondary-btn admin-panel-nav-btn" to="/">
            <Home size={16} />
            <span>На главную</span>
          </Link>
          <button type="button" className="secondary-btn admin-panel-nav-btn" onClick={() => navigate(-1)}>
            <ArrowLeft size={16} />
            <span>Назад</span>
          </button>
        </div>
      </header>

      <div className="admin-panel-tabs" role="tablist" aria-label="Разделы админ-панели">
        <button
          type="button"
          role="tab"
          aria-selected={activeTab === "upload"}
          className={`admin-panel-tab${activeTab === "upload" ? " is-active" : ""}`}
          onClick={() => setActiveTab("upload")}
        >
          <Upload size={16} aria-hidden="true" />
          <span>Загрузка видео</span>
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={activeTab === "users"}
          className={`admin-panel-tab${activeTab === "users" ? " is-active" : ""}`}
          onClick={() => setActiveTab("users")}
        >
          <Users size={16} aria-hidden="true" />
          <span>Управление пользователями</span>
        </button>
      </div>

      {activeTab === "users" ? (
        <article className="admin-card admin-card--users">
          <h2>Управление пользователями</h2>
          <form className="admin-user-search-form" onSubmit={handleUserSearch}>
            <input
              type="search"
              value={userQuery}
              onChange={(event) => setUserQuery(event.target.value)}
              placeholder="Юзернейм или email"
              aria-label="Поиск пользователя"
            />
            <button type="submit" className="primary-btn" disabled={userSearchLoading}>
              {userSearchLoading ? "Поиск…" : "Найти"}
            </button>
          </form>

          {userActionError ? (
            <p className="admin-message admin-message--error" role="alert">
              {userActionError}
            </p>
          ) : null}
          {userActionMessage ? (
            <p className="admin-message admin-message--success" role="status">
              {userActionMessage}
            </p>
          ) : null}

          {userResults.length === 0 && !userSearchLoading ? (
            <p className="muted">Введите юзернейм или email и нажмите «Найти».</p>
          ) : (
            <ul className="admin-user-results">
              {userResults.map((foundUser) => (
                <li key={foundUser.id} className="admin-user-card">
                  <div className="admin-user-card-meta">
                    <strong>
                      {foundUser.username ? `@${foundUser.username}` : "— без юзернейма —"}
                    </strong>
                    <span className="muted">{foundUser.email}</span>
                    <span className="muted">ID: {foundUser.id}</span>
                    {foundUser.isBannedFromCommenting ? (
                      <span className="admin-user-ban-badge">Бан в комментариях</span>
                    ) : null}
                  </div>
                  <div className="admin-user-card-actions">
                    <button
                      type="button"
                      className="secondary-btn"
                      disabled={userActionId === foundUser.id}
                      onClick={() => handleToggleBan(foundUser)}
                    >
                      {foundUser.isBannedFromCommenting
                        ? "Разбанить"
                        : "Забанить в комментариях"}
                    </button>
                    <button
                      type="button"
                      className="danger-btn"
                      disabled={userActionId === foundUser.id}
                      onClick={() => handleDeleteUser(foundUser)}
                    >
                      Полностью удалить
                    </button>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </article>
      ) : (
      <div className="admin-panel-grid">
        <article className="admin-card">
          <h2>Сезоны в каталоге</h2>
          {seasonsLoading ? (
            <p className="muted">Загрузка сезонов…</p>
          ) : seasonsError ? (
            <p className="admin-message admin-message--error" role="alert">
              {seasonsError}
            </p>
          ) : seasons.length === 0 ? (
            <p className="muted">Сезоны не найдены.</p>
          ) : (
            <ul className="admin-season-list">
              {seasons.map((season) => (
                <li key={season.id}>
                  <span className="admin-season-number">Сезон {season.number}</span>
                  <span className="admin-season-title">{season.title}</span>
                  <code className="admin-season-id">{season.id}</code>
                </li>
              ))}
            </ul>
          )}
        </article>

        <article className="admin-card admin-card--upload">
          <h2>
            <Upload size={20} aria-hidden="true" />
            <span>Загрузить серию</span>
          </h2>
          <form className="admin-upload-form" onSubmit={handleUpload}>
            <label className="admin-field">
              <span>Сезон</span>
              <select
                value={seasonId}
                onChange={(event) => setSeasonId(event.target.value)}
                disabled={seasonsLoading || seasons.length === 0}
                required
              >
                {seasons.map((season) => (
                  <option key={season.id} value={season.id}>
                    Сезон {season.number} — {season.title}
                  </option>
                ))}
              </select>
            </label>

            <label className="admin-field">
              <span>Название</span>
              <input
                type="text"
                value={title}
                onChange={(event) => setTitle(event.target.value)}
                placeholder="Серия 1"
                maxLength={255}
                required
              />
            </label>

            <label className="admin-field">
              <span>Номер серии</span>
              <input
                type="number"
                min={1}
                value={episodeNumber}
                onChange={(event) => setEpisodeNumber(event.target.value)}
                required
              />
            </label>

            <label className="admin-field">
              <span>Описание</span>
              <textarea
                value={description}
                onChange={(event) => setDescription(event.target.value)}
                rows={4}
                maxLength={4000}
                required
              />
            </label>

            <label className="admin-field">
              <span>Видеофайл (.mp4)</span>
              <input
                type="file"
                accept="video/mp4,video/*"
                onChange={(event) => setVideoFile(event.target.files?.[0] ?? null)}
                required
              />
            </label>

            <label className="admin-field">
              <span>Превью (необязательно)</span>
              <input
                type="file"
                accept="image/*"
                onChange={(event) => setPreviewFile(event.target.files?.[0] ?? null)}
              />
            </label>

            {uploadError ? (
              <p className="admin-message admin-message--error" role="alert">
                {uploadError}
              </p>
            ) : null}
            {uploadSuccess ? (
              <p className="admin-message admin-message--success" role="status">
                {uploadSuccess}
              </p>
            ) : null}

            <button type="submit" className="primary-btn admin-upload-submit" disabled={uploading}>
              {uploading ? "Загрузка…" : "Загрузить на сервер"}
            </button>
          </form>
        </article>
      </div>
      )}
    </section>
  );
}
