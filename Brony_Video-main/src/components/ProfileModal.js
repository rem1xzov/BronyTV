import React, { useEffect, useId, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { AlertCircle, LogIn, UserCircle, X } from "lucide-react";
import { useAuth } from "../auth/AuthContext";
import { getRaceDisplay } from "../auth/race";
import { normalizeAuthUser } from "../auth/user";
import { validateUsername } from "../auth/username";

function ProfileSkeleton() {
  return (
    <div className="profile-skeleton" aria-hidden="true">
      <div className="profile-skeleton-avatar" />
      <div className="profile-skeleton-line profile-skeleton-line--title" />
      <div className="profile-skeleton-line profile-skeleton-line--wide" />
      <div className="profile-skeleton-badge" />
      <div className="profile-skeleton-line profile-skeleton-line--notice" />
    </div>
  );
}

export default function ProfileModal({ isOpen, onClose, onRequestSignIn }) {
  const { user, refreshUser, logout, updateUsername } = useAuth();
  const titleId = useId();
  const onCloseRef = useRef(onClose);
  const [profileLoading, setProfileLoading] = useState(false);
  const [profileUser, setProfileUser] = useState(null);
  const [sessionExpired, setSessionExpired] = useState(false);
  const [fetchError, setFetchError] = useState("");
  const [usernameInput, setUsernameInput] = useState("");
  const [usernameError, setUsernameError] = useState("");
  const [usernameSaving, setUsernameSaving] = useState(false);
  const [usernameSuccess, setUsernameSuccess] = useState("");

  onCloseRef.current = onClose;

  useEffect(() => {
    if (!isOpen) {
      return undefined;
    }

    const handleKeyDown = (event) => {
      if (event.key === "Escape") {
        onCloseRef.current();
      }
    };

    document.body.style.overflow = "hidden";
    window.addEventListener("keydown", handleKeyDown);

    return () => {
      document.body.style.overflow = "";
      window.removeEventListener("keydown", handleKeyDown);
    };
  }, [isOpen]);

  useEffect(() => {
    if (!isOpen) {
      setProfileLoading(false);
      setProfileUser(null);
      setSessionExpired(false);
      setFetchError("");
      setUsernameInput("");
      setUsernameError("");
      setUsernameSuccess("");
      return undefined;
    }

    let cancelled = false;
    const cachedUser = normalizeAuthUser(user);
    if (cachedUser) {
      setProfileUser(cachedUser);
    }

    const loadProfile = async () => {
      setProfileLoading(true);
      setSessionExpired(false);
      setFetchError("");

      try {
        const profile = await refreshUser();
        if (cancelled) {
          return;
        }

        const normalized = normalizeAuthUser(profile);
        if (!normalized) {
          setProfileUser(null);
          setSessionExpired(true);
          return;
        }

        setProfileUser(normalized);
      } catch (error) {
        if (!cancelled) {
          setFetchError("Не удалось загрузить профиль. Проверьте подключение и попробуйте снова.");
        }
      } finally {
        if (!cancelled) {
          setProfileLoading(false);
        }
      }
    };

    loadProfile();

    return () => {
      cancelled = true;
    };
  }, [isOpen, refreshUser]);

  if (!isOpen) {
    return null;
  }

  const handleBackdropClick = (event) => {
    if (event.target === event.currentTarget) {
      onClose();
    }
  };

  const handleSignInAgain = async () => {
    await logout();
    onClose();
    onRequestSignIn?.();
  };

  const displayUser = profileUser ?? normalizeAuthUser(user);
  const raceDisplay = getRaceDisplay(displayUser?.race);
  const hasUsername = Boolean(displayUser?.username);

  const handleSaveUsername = async (event) => {
    event.preventDefault();
    setUsernameError("");
    setUsernameSuccess("");

    const validation = validateUsername(usernameInput);
    if (!validation.valid) {
      setUsernameError(validation.error);
      return;
    }

    setUsernameSaving(true);
    try {
      const updated = await updateUsername(validation.value);
      await refreshUser();
      const normalized = normalizeAuthUser(updated) ?? normalizeAuthUser(user);
      if (normalized) {
        setProfileUser(normalized);
      }
      setUsernameSuccess("Юзернейм сохранён");
      setUsernameInput(validation.value);
    } catch (error) {
      setUsernameError(error.message || "Не удалось сохранить юзернейм.");
    } finally {
      setUsernameSaving(false);
    }
  };

  const handleRetry = async () => {
    setFetchError("");
    setProfileLoading(true);
    try {
      const profile = await refreshUser();
      const normalized = normalizeAuthUser(profile);
      if (!normalized) {
        setProfileUser(null);
        setSessionExpired(true);
        return;
      }
      setProfileUser(normalized);
      setSessionExpired(false);
    } catch (error) {
      setFetchError("Не удалось загрузить профиль. Проверьте подключение и попробуйте снова.");
    } finally {
      setProfileLoading(false);
    }
  };

  return createPortal(
    <div className="profile-modal-overlay" onClick={handleBackdropClick} role="presentation">
      <div
        className="profile-modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        onClick={(event) => event.stopPropagation()}
      >
        <button type="button" className="profile-modal-close" onClick={onClose} aria-label="Закрыть">
          <X size={20} />
        </button>

        <header className="profile-modal-header">
          <div className="profile-modal-icon" aria-hidden="true">
            <UserCircle size={32} />
          </div>
          <h2 id={titleId}>Личный кабинет</h2>
          <p className="profile-modal-subtitle">Ваш аккаунт BronyTV</p>
        </header>

        <div className="profile-modal-body">
          {profileLoading && !displayUser ? (
            <ProfileSkeleton />
          ) : sessionExpired ? (
            <div className="profile-modal-state profile-modal-state--expired">
              <div className="profile-modal-state-icon" aria-hidden="true">
                <AlertCircle size={28} />
              </div>
              <p className="profile-modal-state-title">Сессия истекла</p>
              <p className="profile-modal-state-text">
                Войдите снова, чтобы открыть личный кабинет.
              </p>
              <div className="profile-modal-actions">
                <button type="button" className="primary-btn profile-modal-action-btn" onClick={handleSignInAgain}>
                  <LogIn size={18} />
                  <span>Войти</span>
                </button>
                <button type="button" className="secondary-btn profile-modal-action-btn" onClick={onClose}>
                  Закрыть
                </button>
              </div>
            </div>
          ) : fetchError ? (
            <div className="profile-modal-state profile-modal-state--error">
              <div className="profile-modal-state-icon" aria-hidden="true">
                <AlertCircle size={28} />
              </div>
              <p className="profile-modal-state-title">Ошибка загрузки</p>
              <p className="profile-modal-state-text">{fetchError}</p>
              <div className="profile-modal-actions">
                <button type="button" className="primary-btn profile-modal-action-btn" onClick={handleRetry}>
                  Повторить
                </button>
                <button type="button" className="secondary-btn profile-modal-action-btn" onClick={onClose}>
                  Закрыть
                </button>
              </div>
            </div>
          ) : displayUser ? (
            <div className="profile-modal-content">
              {profileLoading ? (
                <p className="profile-modal-refresh-hint muted" aria-live="polite">
                  Обновление данных…
                </p>
              ) : null}
              <div className="profile-modal-avatar" aria-hidden="true">
                <UserCircle size={40} />
              </div>

              <dl className="profile-details">
                <div className="profile-detail-row">
                  <dt>Ваш Email</dt>
                  <dd className="profile-detail-email" title={displayUser.email}>
                    {displayUser.email || "—"}
                  </dd>
                </div>

                <div className="profile-detail-row profile-detail-row--username">
                  <dt>Юзернейм</dt>
                  {hasUsername ? (
                    <dd className="profile-username-value" title={`@${displayUser.username}`}>
                      @{displayUser.username}
                    </dd>
                  ) : (
                    <dd className="profile-username-editor">
                      <form className="profile-username-form" onSubmit={handleSaveUsername}>
                        <div className="profile-username-input-wrap">
                          <span className="profile-username-prefix" aria-hidden="true">
                            @
                          </span>
                          <input
                            type="text"
                            className="profile-username-input"
                            value={usernameInput}
                            onChange={(event) => {
                              setUsernameInput(event.target.value);
                              setUsernameError("");
                              setUsernameSuccess("");
                            }}
                            placeholder="username"
                            autoComplete="username"
                            autoCapitalize="off"
                            autoCorrect="off"
                            spellCheck={false}
                            maxLength={15}
                            aria-label="Юзернейм"
                            aria-invalid={Boolean(usernameError)}
                          />
                        </div>
                        {usernameError ? (
                          <p className="profile-username-message profile-username-message--error" role="alert">
                            {usernameError}
                          </p>
                        ) : null}
                        {usernameSuccess ? (
                          <p className="profile-username-message profile-username-message--success" role="status">
                            {usernameSuccess}
                          </p>
                        ) : null}
                        <button
                          type="submit"
                          className="primary-btn profile-username-save"
                          disabled={usernameSaving}
                        >
                          {usernameSaving ? "Сохранение…" : "Сохранить"}
                        </button>
                      </form>
                    </dd>
                  )}
                </div>

                <div className="profile-detail-row profile-detail-row--race">
                  <dt>Раса</dt>
                  <dd>
                    <span className={raceDisplay.badgeClass}>{raceDisplay.label}</span>
                  </dd>
                </div>
              </dl>

              <p className="profile-modal-notice">
                Ваша раса выбрана при регистрации и не может быть изменена.
              </p>
            </div>
          ) : (
            <div className="profile-modal-state profile-modal-state--expired">
              <p className="profile-modal-state-title">Профиль недоступен</p>
              <p className="profile-modal-state-text">Войдите в аккаунт, чтобы увидеть данные.</p>
              <button type="button" className="primary-btn profile-modal-action-btn" onClick={handleSignInAgain}>
                <LogIn size={18} />
                <span>Войти</span>
              </button>
            </div>
          )}
        </div>
      </div>
    </div>,
    document.body
  );
}
