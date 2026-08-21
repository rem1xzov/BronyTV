import React, { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Activity as ActivityIcon, ArrowLeft, Home, LifeBuoy, Shield, Star, Upload, Users } from "lucide-react";
import { useAuth } from "../auth/AuthContext";
import { isPlatformAdmin } from "../auth/adminAccess";
import { apiFetch, apiUpload } from "../auth/api";
import AdminSupportPanel from "./AdminSupportPanel";
import AdminActivityPanel from "./AdminActivityPanel";

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

  const role = raw.role ?? raw.Role ?? raw.platformRole ?? raw.PlatformRole ?? "User";
  const isOwner = Boolean(raw.isOwner ?? raw.IsOwner ?? (role === "Owner"));
  const isPlatformAdmin = Boolean(raw.isPlatformAdmin ?? raw.IsPlatformAdmin ?? (role === "Admin" || role === "Owner"));

  return {
    id,
    email: raw.email ?? raw.Email ?? "",
    username: raw.username ?? raw.Username ?? null,
    race: raw.race ?? raw.Race ?? "",
    role,
    platformRole: role,
    isOwner,
    isPlatformAdmin,
    isBannedFromCommenting: Boolean(
      raw.isBannedFromCommenting ?? raw.IsBannedFromCommenting ?? false
    ),
    createdAtUtc: raw.createdAtUtc ?? raw.CreatedAtUtc ?? null
  };
}

const USERS_PAGE_SIZE = 20;

function formatRoleLabel(user) {
  if (user.isOwner || user.platformRole === "Owner" || user.role === "Owner") {
    return "Владелец";
  }
  if (user.isPlatformAdmin || user.platformRole === "Admin" || user.role === "Admin") {
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

  const [newUsername, setNewUsername] = useState("");
  const [newEmail, setNewEmail] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [newRole, setNewRole] = useState("User");
  const [createUserError, setCreateUserError] = useState("");
  const [createUserSuccess, setCreateUserSuccess] = useState("");
  const [creatingUser, setCreatingUser] = useState(false);

  const [premiumKeyLoading, setPremiumKeyLoading] = useState(false);
  const [premiumKeyError, setPremiumKeyError] = useState("");
    const [premiumKeys, setPremiumKeys] = useState([]);
  const [premiumKeysTotal, setPremiumKeysTotal] = useState(0);
  const [premiumKeyCopied, setPremiumKeyCopied] = useState("");

    const [vpnPromoLoading, setVpnPromoLoading] = useState(false);
  const [vpnPromoError, setVpnPromoError] = useState("");
  const [vpnPromoKeys, setVpnPromoKeys] = useState([]);
  const [vpnPromoTotal, setVpnPromoTotal] = useState(0);
  const [vpnPromoCopied, setVpnPromoCopied] = useState("");
  const [vpnPromoDuration, setVpnPromoDuration] = useState("1");
  const [vpnSubscriptions, setVpnSubscriptions] = useState([]);
  const [vpnSubsLoading, setVpnSubsLoading] = useState(false);
  const [vpnSubsError, setVpnSubsError] = useState("");
  const [vpnReferrals, setVpnReferrals] = useState([]);
  const [vpnReferralsLoading, setVpnReferralsLoading] = useState(false);
  const [vpnReferralsError, setVpnReferralsError] = useState("");


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
        `/users?page=${page}&pageSize=${USERS_PAGE_SIZE}`
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

  const loadPremiumKeys = async () => {
    setPremiumKeyError("");
    try {
      const response = await fetch("/api/admin/premium-keys/list", {
        credentials: "include"
      });
      const payload = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(
          (response.status === 403
            ? "Нет прав: только владелец или администратор может просматривать ключи. "
            : "") +
            (payload.message || `Сервер ответил: ${response.status}`)
        );
      }

      const keys = Array.isArray(payload.keys ?? payload.Keys)
        ? payload.keys ?? payload.Keys
        : [];
      setPremiumKeys(keys);
      setPremiumKeysTotal(Number(payload.total ?? payload.Total ?? keys.length));
    } catch (error) {
      setPremiumKeyError(error.message || "Не удалось загрузить список ключей.");
    }
  };

    useEffect(() => {
    if (activeTab === "users") {
      loadUsers(1);
      loadPremiumKeys();
    }
  }, [activeTab]);

  useEffect(() => {
    if (activeTab === "vpn") {
      loadVpnPromoKeys();
      loadVpnSubscriptions();
      loadVpnReferrals();
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
      const response = await apiFetch(`/users/${targetUser.id}`, { method: "DELETE" });
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
      const response = await apiFetch(`/users/${targetUser.id}`, {
        method: "PATCH",
        body: JSON.stringify({ isBannedFromCommenting: !targetUser.isBannedFromCommenting })
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
      const response = await apiFetch(`/users/${targetUser.id}`, {
        method: "PATCH",
        body: JSON.stringify({ role: "Admin" })
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
      const response = await apiFetch(`/users/${targetUser.id}`, {
        method: "PATCH",
        body: JSON.stringify({ role: "User" })
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

  const handleCreateUser = async (event) => {
    event.preventDefault();
    setCreateUserError("");
    setCreateUserSuccess("");
    setCreatingUser(true);

    try {
      const response = await apiFetch("/users", {
        method: "POST",
        body: JSON.stringify({
          username: newUsername,
          email: newEmail,
          password: newPassword,
          role: newRole,
          race: "earth_pony"
        })
      });
      const raw = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(raw.message || "Не удалось создать пользователя.");
      }

      setCreateUserSuccess(`Пользователь @${raw.username || newUsername} успешно создан!`);
      setNewUsername("");
      setNewEmail("");
      setNewPassword("");
      setNewRole("User");
      await loadUsers(1);
    } catch (error) {
      setCreateUserError(error.message || "Не удалось создать пользователя.");
    } finally {
      setCreatingUser(false);
    }
  };

  const handleGeneratePremiumKey = async () => {
    setPremiumKeyLoading(true);
    setPremiumKeyError("");

    try {
      const response = await fetch("/api/admin/premium-keys/generate", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        credentials: "include",
        body: "{}"
      });
      const payload = await response.json().catch(() => ({}));

      if (!response.ok) {
        throw new Error(
          (response.status === 403
            ? "Нет прав: только владелец или администратор может генерировать ключи. "
            : "") +
            (payload.message || `Сервер ответил: ${response.status}`)
        );
      }

      // Перечитываем весь список с сервера, чтобы новый ключ появился в нём.
      await loadPremiumKeys();
    } catch (error) {
      setPremiumKeyError(error.message || "Не удалось сгенерировать ключ.");
    } finally {
      setPremiumKeyLoading(false);
    }
  };

    const handleCopyPremiumKey = async (key) => {
    if (!key) return;
    try {
      await navigator.clipboard.writeText(key);
      setPremiumKeyCopied(key);
      setTimeout(() => setPremiumKeyCopied(""), 2000);
    } catch {
      setPremiumKeyError("Не удалось скопировать ключ в буфер обмена.");
    }
  };

  const loadVpnPromoKeys = async () => {
    setVpnPromoError("");
    try {
      const response = await fetch("/api/admin/vpn/promo-keys/list", {
        credentials: "include"
      });
      const payload = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(payload.message || `Сервер ответил: ${response.status}`);
      }
      const items = Array.isArray(payload.items ?? payload.Items)
        ? payload.items ?? payload.Items
        : [];
      setVpnPromoKeys(items);
      setVpnPromoTotal(Number(payload.total ?? payload.Total ?? items.length));
    } catch (error) {
      setVpnPromoError(error.message || "Не удалось загрузить VPN промо-ключи.");
    }
  };

  const handleGenerateVpnPromo = async () => {
    setVpnPromoLoading(true);
    setVpnPromoError("");
    try {
            const duration = Number.parseInt(vpnPromoDuration, 10) || 1;
      const response = await fetch("/api/admin/vpn/promo-keys/generate", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        credentials: "include",
        body: JSON.stringify({ durationMonths: duration })
      });
      const payload = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(payload.message || `Сервер ответил: ${response.status}`);
      }
      await loadVpnPromoKeys();
    } catch (error) {
      setVpnPromoError(error.message || "Не удалось сгенерировать VPN промо-ключ.");
    } finally {
      setVpnPromoLoading(false);
    }
  };

  const handleCopyVpnPromo = async (key) => {
    if (!key) return;
    try {
      await navigator.clipboard.writeText(key);
      setVpnPromoCopied(key);
      setTimeout(() => setVpnPromoCopied(""), 2000);
    } catch {
      setVpnPromoError("Не удалось скопировать ключ в буфер обмена.");
    }
  };

  const loadVpnSubscriptions = async () => {
    setVpnSubsLoading(true);
    setVpnSubsError("");
    try {
      const response = await fetch("/api/admin/vpn/subscriptions?page=1&pageSize=50", {
        credentials: "include"
      });
      const payload = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(payload.message || `Сервер ответил: ${response.status}`);
      }
      setVpnSubscriptions(Array.isArray(payload.items ?? payload.Items) ? payload.items ?? payload.Items : []);
    } catch (error) {
      setVpnSubsError(error.message || "Не удалось загрузить подписки VPN.");
    } finally {
      setVpnSubsLoading(false);
    }
  };

  const loadVpnReferrals = async () => {
    setVpnReferralsLoading(true);
    setVpnReferralsError("");
    try {
      const response = await fetch("/api/admin/vpn/referral-rewards", {
        credentials: "include"
      });
      const payload = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(payload.message || `Сервер ответил: ${response.status}`);
      }
      setVpnReferrals(Array.isArray(payload.items ?? payload.Items) ? payload.items ?? payload.Items : []);
    } catch (error) {
      setVpnReferralsError(error.message || "Не удалось загрузить реферальные начисления.");
    } finally {
      setVpnReferralsLoading(false);
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
        <button
          type="button"
          role="tab"
          aria-selected={activeTab === "support"}
          className={`admin-panel-tab${activeTab === "support" ? " is-active" : ""}`}
          onClick={() => setActiveTab("support")}
        >
          <LifeBuoy size={16} aria-hidden="true" />
          <span>Обращения в поддержку</span>
        </button>
                <button
          type="button"
          role="tab"
          aria-selected={activeTab === "activity"}
          className={`admin-panel-tab${activeTab === "activity" ? " is-active" : ""}`}
          onClick={() => setActiveTab("activity")}
        >
          <ActivityIcon size={16} aria-hidden="true" />
          <span>Активность</span>
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={activeTab === "vpn"}
          className={`admin-panel-tab${activeTab === "vpn" ? " is-active" : ""}`}
          onClick={() => setActiveTab("vpn")}
        >
          <Shield size={16} aria-hidden="true" />
          <span>VPN</span>
        </button>
      </div>

      {activeTab === "vpn" ? (
        <div className="admin-panel-grid">
          {/* Генерация VPN промо-ключей */}
          <article className="admin-card admin-card--premium-key">
            <h2>
              <Shield size={20} aria-hidden="true" />
              <span>Сгенерировать VPN промо-ключ</span>
            </h2>
            <p className="muted">
              Создаёт одноразовый ключ для активации VPN-подписки. Скопируйте его покупателю.
            </p>

            {vpnPromoError ? (
              <p className="admin-message admin-message--error" role="alert">
                {vpnPromoError}
              </p>
            ) : null}

            <div className="admin-users-header">
              <span className="admin-premium-key-list-title">VPN промо-ключи</span>
              <p className="muted">Всего: {vpnPromoTotal}</p>
            </div>

            {vpnPromoKeys.length === 0 ? (
              <p className="muted">Неиспользованных ключей пока нет.</p>
            ) : (
              <ul className="admin-premium-key-list">
                                {vpnPromoKeys.map((key) => (
                  <li key={key.code} className="admin-premium-key-box">
                    <div className="admin-premium-key-row">
                                            <code className="admin-premium-key">{key.code}</code>
                      <span style={{ marginLeft: 8, opacity: 0.75, whiteSpace: "nowrap" }}>
                        {key.durationMonths ? `${key.durationMonths} мес.` : "—"}
                      </span>
                      <button
                        type="button"
                        className="secondary-btn"
                        onClick={() => handleCopyVpnPromo(key.code)}
                      >
                        {vpnPromoCopied === key.code ? "Скопировано ✓" : "Скопировать"}
                      </button>
                    </div>
                  </li>
                ))}
              </ul>
            )}

                        <label className="admin-field">
              <span>Срок ключа</span>
              <select
                value={vpnPromoDuration}
                onChange={(event) => setVpnPromoDuration(event.target.value)}
              >
                <option value="1">1 месяц</option>
                <option value="3">3 месяца</option>
                <option value="6">6 месяцев</option>
                <option value="12">12 месяцев</option>
              </select>
            </label>

            <button
              type="button"
              className="primary-btn admin-submit-btn"
              onClick={handleGenerateVpnPromo}
              disabled={vpnPromoLoading}
            >
              {vpnPromoLoading ? "Генерация..." : "Сгенерировать VPN промо-ключ"}
            </button>
          </article>

          {/* Список подписок VPN */}
          <article className="admin-card admin-card--subscriptions">
            <div className="admin-users-header">
              <h2>Подписки VPN</h2>
              <button
                type="button"
                className="secondary-btn"
                onClick={loadVpnSubscriptions}
                disabled={vpnSubsLoading}
              >
                {vpnSubsLoading ? "Загрузка…" : "Обновить"}
              </button>
            </div>
            {vpnSubsError ? (
              <p className="admin-message admin-message--error" role="alert">
                {vpnSubsError}
              </p>
            ) : null}
            {vpnSubsLoading && vpnSubscriptions.length === 0 ? (
              <p className="muted">Загрузка подписок…</p>
            ) : vpnSubscriptions.length === 0 ? (
              <p className="muted">Подписок пока нет.</p>
            ) : (
              <ul className="admin-user-results admin-user-results--scroll">
                {vpnSubscriptions.map((sub) => (
                  <li key={sub.subscriptionId} className="admin-sub-card">
                    <div className="admin-user-card-meta">
                      <strong>{sub.username ? `@${sub.username}` : sub.email || sub.userId}</strong>
                      <span className="muted">
                        {sub.planName || sub.kind}
                        {sub.expiresAtUtc ? ` · до ${new Date(sub.expiresAtUtc).toLocaleDateString()}` : ""}
                      </span>
                      <span className="admin-user-role-badge">{sub.kind}</span>
                      {sub.isRevoked ? (
                        <span className="admin-user-ban-badge">Отключена</span>
                      ) : (
                        <span className="admin-user-ok-badge">Активна</span>
                      )}
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </article>

          {/* Реферальные начисления */}
          <article className="admin-card admin-card--referrals">
            <div className="admin-users-header">
              <h2>Реферальные начисления</h2>
              <button
                type="button"
                className="secondary-btn"
                onClick={loadVpnReferrals}
                disabled={vpnReferralsLoading}
              >
                {vpnReferralsLoading ? "Загрузка…" : "Обновить"}
              </button>
            </div>
            {vpnReferralsError ? (
              <p className="admin-message admin-message--error" role="alert">
                {vpnReferralsError}
              </p>
            ) : null}
            {vpnReferralsLoading && vpnReferrals.length === 0 ? (
              <p className="muted">Загрузка начислений…</p>
            ) : vpnReferrals.length === 0 ? (
              <p className="muted">Начислений пока нет.</p>
            ) : (
              <ul className="admin-user-results admin-user-results--scroll">
                {vpnReferrals.map((reward, index) => (
                  <li key={`${reward.referrerId}-${reward.referralUserId}-${index}`} className="admin-sub-card">
                    <div className="admin-user-card-meta">
                      <strong>
                        {reward.referrerUsername ? `@${reward.referrerUsername}` : reward.referrerId} →
                        {reward.referralUsername ? `@${reward.referralUsername}` : reward.referralUserId}
                      </strong>
                      <span className="muted">
                        +{reward.bonusDays} дней · {reward.reason}
                        {reward.isRedeemed ? " · выдано" : " · ожидает"}
                      </span>
                      <span className="muted">{new Date(reward.createdAtUtc).toLocaleDateString()}</span>
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </article>
        </div>
      ) : activeTab === "activity" ? (
        <AdminActivityPanel onBack={() => setActiveTab("users")} />
      ) : activeTab === "support" ? (
        <AdminSupportPanel />
      ) : activeTab === "users" ? (
        <div className="admin-panel-grid">
          {/* Форма создания нового аккаунта */}
          <article className="admin-card">
            <h2>
              <Users size={20} aria-hidden="true" />
              <span>Создать пользователя</span>
            </h2>
            <form className="admin-upload-form" onSubmit={handleCreateUser}>
              {createUserError ? (
                <p className="admin-message admin-message--error" role="alert">
                  {createUserError}
                </p>
              ) : null}
              {createUserSuccess ? (
                <p className="admin-message admin-message--success" role="status">
                  {createUserSuccess}
                </p>
              ) : null}

              <label className="admin-field">
                <span>Имя пользователя (Username)</span>
                <input
                  type="text"
                  placeholder="Юзернейм"
                  value={newUsername}
                  onChange={(event) => setNewUsername(event.target.value)}
                  required
                />
              </label>

              <label className="admin-field">
                <span>Электронная почта (Email)</span>
                <input
                  type="email"
                  placeholder="email@example.com"
                  value={newEmail}
                  onChange={(event) => setNewEmail(event.target.value)}
                  required
                />
              </label>

              <label className="admin-field">
                <span>Пароль (Password)</span>
                <input
                  type="password"
                  placeholder="Минимум 8 символов"
                  value={newPassword}
                  onChange={(event) => setNewPassword(event.target.value)}
                  required
                />
              </label>

              <label className="admin-field">
                <span>Роль (Role)</span>
                <select
                  value={newRole}
                  onChange={(event) => setNewRole(event.target.value)}
                  required
                >
                  <option value="User">Пользователь</option>
                  <option value="Admin">Администратор</option>
                  <option value="Owner">Владелец</option>
                </select>
              </label>

              <button
                type="submit"
                className="primary-btn admin-submit-btn"
                disabled={creatingUser}
              >
                {creatingUser ? "Создание..." : "Создать аккаунт"}
              </button>
            </form>
          </article>

          {/* Генерация премиум-ключа */}
          <article className="admin-card admin-card--premium-key">
            <h2>
              <Star size={20} aria-hidden="true" />
              <span>Сгенерировать премиум-ключ</span>
            </h2>
            <p className="muted">
              Создаёт один одноразовый ключ для выдачи покупателю на Boosty. Скопируйте его в пост
              или личное сообщение.
            </p>

            {premiumKeyError ? (
              <p className="admin-message admin-message--error" role="alert">
                {premiumKeyError}
              </p>
            ) : null}

            <div className="admin-users-header">
              <span className="admin-premium-key-list-title">Премиум-ключи</span>
              <p className="muted">Всего: {premiumKeysTotal}</p>
            </div>

            {premiumKeys.length === 0 ? (
              <p className="muted">Неиспользованных ключей пока нет.</p>
            ) : (
              <ul className="admin-premium-key-list">
                {premiumKeys.map((key) => (
                  <li key={key} className="admin-premium-key-box">
                    <div className="admin-premium-key-row">
                      <code className="admin-premium-key">{key}</code>
                      <button
                        type="button"
                        className="secondary-btn"
                        onClick={() => handleCopyPremiumKey(key)}
                      >
                        {premiumKeyCopied === key ? "Скопировано ✓" : "Скопировать"}
                      </button>
                    </div>
                  </li>
                ))}
              </ul>
            )}

            <button
              type="button"
              className="primary-btn admin-submit-btn"
              onClick={handleGeneratePremiumKey}
              disabled={premiumKeyLoading}
            >
              {premiumKeyLoading ? "Генерация..." : "Сгенерировать премиум-ключ"}
            </button>
          </article>

          {/* Список пользователей */}
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
                  const isAdmin = foundUser.isPlatformAdmin || foundUser.role === "Admin" || foundUser.platformRole === "Admin";
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
        </div>
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
