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
    role: raw.role ?? raw.Role ?? "User",
    isOwner: Boolean(raw.isOwner ?? raw.IsOwner ?? false),
    isBannedFromCommenting: Boolean(
      raw.isBannedFromCommenting ?? raw.IsBannedFromCommenting ?? false
    ),
    createdAtUtc: raw.createdAtUtc ?? raw.CreatedAtUtc ?? null
  };
}

const USERS_PAGE_SIZE = 20;

function formatRoleLabel(user) {
  if (user.isOwner || user.role === "Owner") {
    return "Владелец";
  }
  if (user.role === "Admin") {
    return "Админ";
  }
  return "Пользователь";
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
  const [userResults, setUserResults] = useState([]);
  const [userPage, setUserPage] = useState(1);
  const [userTotal, setUserTotal] = useState(0);
  const [userHasMore, setUserHasMore] = useState(false);
  const [userListLoading, setUserListLoading] = useState(false);
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

  const loadUsers = async (page = userPage) => {
    setUserListLoading(true);
    setUserActionError("");

    try {
      const response = await apiFetch(
        `/admin/users?page=${page}&pageSize=${USERS_PAGE_SIZE}`
      );
      const raw = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(raw.message || "Не удалось загрузить пользователей.");
      }

      const items = Array.isArray(raw.items ?? raw.Items) ? raw.items ?? raw.Items : [];
      setUserResults(items.map(normalizeAdminUser).filter(Boolean));
      setUserPage(Number(raw.page ?? raw.Page ?? page));
      setUserTotal(Number(raw.totalCount ?? raw.TotalCount ?? 0));
      setUserHasMore(Boolean(raw.hasMore ?? raw.HasMore));
    } catch (error) {
      setUserActionError(error.message || "Не удалось загрузить пользователей.");
      setUserResults([]);
    } finally {
      setUserListLoading(false);
    }
  };

  useEffect(() => {
    if (activeTab === "users") {
      loadUsers(1);
    }
  }, [activeTab]);

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

      setUserActionMessage("Пользователь удалён.");
      await loadUsers(userPage);
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

  const handlePromoteAdmin = async (targetUser) => {
    const confirmed = window.confirm(
      "Вы уверены, что хотите назначить этого пользователя администратором?"
    );
    if (!confirmed) {
      return;
    }

    setUserActionId(targetUser.id);
    setUserActionError("");
    setUserActionMessage("");

    try {
      const response = await apiFetch(`/admin/users/${targetUser.id}/promote-admin`, {
        method: "PUT"
      });
      const raw = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(raw.message || "Не удалось назначить администратора.");
      }

      const updated = normalizeAdminUser(raw);
      if (updated) {
        setUserResults((prev) => prev.map((item) => (item.id === updated.id ? updated : item)));
      }
      setUserActionMessage("Пользователь назначен администратором.");
    } catch (error) {
      setUserActionError(error.message || "Не удалось назначить администратора.");
    } finally {
      setUserActionId(null);
    }
  };

  const handleDemoteAdmin = async (targetUser) => {
    const confirmed = window.confirm("Убрать у пользователя права администратора?");
    if (!confirmed) {
      return;
    }

    setUserActionId(targetUser.id);
    setUserActionError("");
    setUserActionMessage("");

    try {
      const response = await apiFetch(`/admin/users/${targetUser.id}/demote-admin`, {
        method: "PUT"
      });
      const raw = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(raw.message || "Не удалось снять права администратора.");
      }

      const updated = normalizeAdminUser(raw);
      if (updated) {
        setUserResults((prev) => prev.map((item) => (item.id === updated.id ? updated : item)));
      }
      setUserActionMessage("Права администратора сняты.");
    } catch (error) {
      setUserActionError(error.message || "Не удалось снять права администратора.");
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
          <div className="admin-users-header">
            <h2>Пользователи</h2>
            <p className="muted">
              Всего: {userTotal}
              {userListLoading ? " · загрузка…" : ""}
            </p>
          </div>

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

          {userListLoading && userResults.length === 0 ? (
            <p className="muted">Загрузка списка пользователей…</p>
          ) : userResults.length === 0 ? (
            <p className="muted">Пользователи не найдены.</p>
          ) : (
            <ul className="admin-user-results admin-user-results--scroll">
              {userResults.map((foundUser) => {
                const isProtected = foundUser.isOwner;
                const isAdmin = foundUser.role === "Admin";
                const busy = userActionId === foundUser.id;

                return (
                  <li
                    key={foundUser.id}
                    className={`admin-user-card${isProtected ? " admin-user-card--owner" : ""}`}
                  >
                    <div className="admin-user-card-meta">
                      <strong>
                        {foundUser.username ? `@${foundUser.username}` : "— без юзернейма —"}
                      </strong>
                      <span className="muted">{foundUser.email}</span>
                      <span className="admin-user-role-badge">{formatRoleLabel(foundUser)}</span>
                      {foundUser.isBannedFromCommenting ? (
                        <span className="admin-user-ban-badge">Бан в комментариях</span>
                      ) : (
                        <span className="admin-user-ok-badge">Комментарии разрешены</span>
                      )}
                    </div>
                    {isProtected ? (
                      <p className="admin-user-owner-note muted">
                        Аккаунт владельца — действия недоступны.
                      </p>
                    ) : (
                      <div className="admin-user-card-actions">
                        {isAdmin ? (
                          <button
                            type="button"
                            className="secondary-btn"
                            disabled={busy}
                            onClick={() => handleDemoteAdmin(foundUser)}
                          >
                            Удалить из админов
                          </button>
                        ) : (
                          <button
                            type="button"
                            className="secondary-btn"
                            disabled={busy}
                            onClick={() => handlePromoteAdmin(foundUser)}
                          >
                            Сделать админом
                          </button>
                        )}
                        <button
                          type="button"
                          className="secondary-btn"
                          disabled={busy}
                          onClick={() => handleToggleBan(foundUser)}
                        >
                          {foundUser.isBannedFromCommenting ? "Разбанить" : "Забанить"}
                        </button>
                        <button
                          type="button"
                          className="danger-btn"
                          disabled={busy}
                          onClick={() => handleDeleteUser(foundUser)}
                        >
                          Полностью удалить
                        </button>
                      </div>
                    )}
                  </li>
                );
              })}
            </ul>
          )}

          {userTotal > USERS_PAGE_SIZE ? (
            <div className="admin-users-pagination">
              <button
                type="button"
                className="secondary-btn"
                disabled={userPage <= 1 || userListLoading}
                onClick={() => loadUsers(userPage - 1)}
              >
                Назад
              </button>
              <span className="muted">
                Страница {userPage} из {Math.max(1, Math.ceil(userTotal / USERS_PAGE_SIZE))}
              </span>
              <button
                type="button"
                className="secondary-btn"
                disabled={!userHasMore || userListLoading}
                onClick={() => loadUsers(userPage + 1)}
              >
                Далее
              </button>
            </div>
          ) : null}
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
