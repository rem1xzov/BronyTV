import React, { useEffect, useId, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { Link } from "react-router-dom";
import { AlertCircle, LogIn, Pencil, Shield, UserCircle, X } from "lucide-react";
import { isPlatformAdmin } from "../auth/adminAccess";
import { useAuth } from "../auth/AuthContext";
import { resolveAvatarEmoji, validateAvatarEmoji } from "../auth/avatar";
import { getRaceAvatarClassName, getRaceDisplay } from "../auth/race";
import { normalizeAuthUser } from "../auth/user";
import { validateUsername } from "../auth/username";
import { validateChangePassword } from "../auth/password";

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
  const { user, refreshUser, logout, updateUsername, updatePassword, updateAvatarEmoji } = useAuth();
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
  const [passwordFormOpen, setPasswordFormOpen] = useState(false);
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [passwordError, setPasswordError] = useState("");
  const [passwordSuccess, setPasswordSuccess] = useState("");
  const [passwordSaving, setPasswordSaving] = useState(false);
  const [usernameChangeFormOpen, setUsernameChangeFormOpen] = useState(false);
  const [changeUsernameInput, setChangeUsernameInput] = useState("");
  const [changeUsernameError, setChangeUsernameError] = useState("");
  const [changeUsernameSuccess, setChangeUsernameSuccess] = useState("");
  const [changeUsernameSaving, setChangeUsernameSaving] = useState(false);
  const [emojiFormOpen, setEmojiFormOpen] = useState(false);
  const [emojiInput, setEmojiInput] = useState("");
  const [emojiError, setEmojiError] = useState("");
  const [emojiSuccess, setEmojiSuccess] = useState("");
  const [emojiSaving, setEmojiSaving] = useState(false);

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
      setPasswordFormOpen(false);
      setNewPassword("");
      setConfirmPassword("");
      setPasswordError("");
      setPasswordSuccess("");
      setPasswordSaving(false);
      setUsernameChangeFormOpen(false);
      setChangeUsernameInput("");
      setChangeUsernameError("");
      setChangeUsernameSuccess("");
      setChangeUsernameSaving(false);
      setEmojiFormOpen(false);
      setEmojiInput("");
      setEmojiError("");
      setEmojiSuccess("");
      setEmojiSaving(false);
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
    const confirmed = window.confirm("Вы точно хотите выйти из аккаунта?");
    if (!confirmed) {
      return;
    }
    await logout();
    onClose();
    onRequestSignIn?.();
  };

  const displayUser = profileUser ?? normalizeAuthUser(user);
  const raceDisplay = getRaceDisplay(displayUser?.race);
  const avatarClassName = getRaceAvatarClassName(displayUser?.race);
  const avatarEmojiDisplay = resolveAvatarEmoji(displayUser);
  const hasUsername = Boolean(displayUser?.username);
  const showAdminPanel = isPlatformAdmin(displayUser);

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

  const resetPasswordForm = () => {
    setPasswordFormOpen(false);
    setNewPassword("");
    setConfirmPassword("");
    setPasswordError("");
    setPasswordSuccess("");
    setPasswordSaving(false);
  };

  const resetUsernameChangeForm = () => {
    setUsernameChangeFormOpen(false);
    setChangeUsernameInput(displayUser?.username || "");
    setChangeUsernameError("");
    setChangeUsernameSuccess("");
    setChangeUsernameSaving(false);
  };

  const handleChangeUsername = async (event) => {
    event.preventDefault();
    setChangeUsernameError("");
    setChangeUsernameSuccess("");

    const validation = validateUsername(changeUsernameInput);
    if (!validation.valid) {
      setChangeUsernameError(validation.error);
      return;
    }

    setChangeUsernameSaving(true);
    try {
      await updateUsername(validation.value);
      const profile = await refreshUser();
      const normalized = normalizeAuthUser(profile) ?? normalizeAuthUser(user);
      if (normalized) {
        setProfileUser(normalized);
      }
      setChangeUsernameSuccess("Юзернейм обновлён");
      setChangeUsernameInput(validation.value);
      window.setTimeout(() => {
        setUsernameChangeFormOpen(false);
        setChangeUsernameSuccess("");
      }, 1200);
    } catch (error) {
      setChangeUsernameError(error.message || "Не удалось сохранить юзернейм.");
    } finally {
      setChangeUsernameSaving(false);
    }
  };

  const handleSavePassword = async (event) => {
    event.preventDefault();
    setPasswordError("");
    setPasswordSuccess("");

    const validation = validateChangePassword(newPassword, confirmPassword);
    if (!validation.valid) {
      setPasswordError(validation.error);
      return;
    }

    setPasswordSaving(true);
    try {
      await updatePassword({ newPassword, confirmPassword });
      setPasswordSuccess("Пароль успешно изменен!");
      setNewPassword("");
      setConfirmPassword("");
      window.setTimeout(() => {
        setPasswordFormOpen(false);
        setPasswordSuccess("");
      }, 1500);
    } catch (error) {
      setPasswordError(error.message || "Не удалось изменить пароль.");
    } finally {
      setPasswordSaving(false);
    }
  };

  const handleSaveEmoji = async (event) => {
    event.preventDefault();
    setEmojiError("");
    setEmojiSuccess("");

    const validation = validateAvatarEmoji(emojiInput);
    if (!validation.valid) {
      setEmojiError(validation.error);
      return;
    }

    setEmojiSaving(true);
    try {
      const updated = await updateAvatarEmoji(validation.value);
      await refreshUser();
      const normalized = normalizeAuthUser(updated) ?? normalizeAuthUser(user);
      if (normalized) {
        setProfileUser(normalized);
      }
      setEmojiSuccess("Эмодзи сохранено!");
      setEmojiInput("");
      window.setTimeout(() => {
        setEmojiFormOpen(false);
        setEmojiSuccess("");
      }, 1200);
    } catch (error) {
      setEmojiError(error.message || "Не удалось сохранить эмодзи.");
    } finally {
      setEmojiSaving(false);
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

              <div className="profile-avatar-block">
                <div className={avatarClassName} aria-label="Аватар пользователя">
                  <span className="profile-avatar-emoji" aria-hidden="true">
                    {avatarEmojiDisplay}
                  </span>
                </div>

                {!emojiFormOpen ? (
                  <button
                    type="button"
                    className="secondary-btn profile-avatar-edit-btn"
                    onClick={() => {
                      setEmojiFormOpen(true);
                      setEmojiInput(displayUser.avatarEmoji || "");
                      setEmojiError("");
                      setEmojiSuccess("");
                    }}
                  >
                    <Pencil size={14} />
                    <span>Изменить эмодзи</span>
                  </button>
                ) : (
                  <form className="profile-emoji-form" onSubmit={handleSaveEmoji}>
                    <label className="profile-emoji-field">
                      <span>Эмодзи-аватар</span>
                      <input
                        type="text"
                        className="profile-emoji-input"
                        value={emojiInput}
                        onChange={(event) => {
                          const next = event.target.value;
                          const graphemes =
                            typeof Intl !== "undefined" && typeof Intl.Segmenter === "function"
                              ? [...new Intl.Segmenter(undefined, { granularity: "grapheme" }).segment(next)].map(
                                  (part) => part.segment
                                )
                              : next;
                          setEmojiInput(graphemes.slice(0, 1).join(""));
                          setEmojiError("");
                          setEmojiSuccess("");
                        }}
                        placeholder="😀"
                        maxLength={8}
                        inputMode="text"
                        autoComplete="off"
                        aria-label="Эмодзи-аватар"
                        aria-invalid={Boolean(emojiError)}
                      />
                    </label>
                    {emojiError ? (
                      <p className="profile-emoji-message profile-emoji-message--error" role="alert">
                        {emojiError}
                      </p>
                    ) : null}
                    {emojiSuccess ? (
                      <p className="profile-emoji-message profile-emoji-message--success" role="status">
                        {emojiSuccess}
                      </p>
                    ) : null}
                    <div className="profile-emoji-actions">
                      <button type="submit" className="primary-btn profile-emoji-save" disabled={emojiSaving}>
                        {emojiSaving ? "Сохранение…" : "Сохранить"}
                      </button>
                      <button
                        type="button"
                        className="secondary-btn profile-emoji-cancel"
                        onClick={() => {
                          setEmojiFormOpen(false);
                          setEmojiInput("");
                          setEmojiError("");
                          setEmojiSuccess("");
                        }}
                        disabled={emojiSaving}
                      >
                        Отмена
                      </button>
                    </div>
                  </form>
                )}
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
                            maxLength={25}
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

              {showAdminPanel ? (
                <div className="profile-admin-section">
                  <Link
                    to="/admin"
                    className="primary-btn profile-admin-panel-btn"
                    onClick={onClose}
                  >
                    <Shield size={18} aria-hidden="true" />
                    <span>Админ-панель</span>
                  </Link>
                </div>
              ) : null}

              <div className="profile-password-section">
                {!passwordFormOpen ? (
                  <button
                    type="button"
                    className="secondary-btn profile-password-toggle"
                    onClick={() => {
                      setPasswordFormOpen(true);
                      setPasswordError("");
                      setPasswordSuccess("");
                    }}
                  >
                    Сменить пароль
                  </button>
                ) : (
                  <form className="profile-password-form" onSubmit={handleSavePassword}>
                    <p className="profile-password-form-title">Смена пароля</p>
                    <label className="profile-password-field">
                      <span>Новый пароль</span>
                      <input
                        type="password"
                        value={newPassword}
                        onChange={(event) => {
                          setNewPassword(event.target.value);
                          setPasswordError("");
                          setPasswordSuccess("");
                        }}
                        autoComplete="new-password"
                        aria-invalid={Boolean(passwordError)}
                      />
                    </label>
                    <label className="profile-password-field">
                      <span>Повторите новый пароль</span>
                      <input
                        type="password"
                        value={confirmPassword}
                        onChange={(event) => {
                          setConfirmPassword(event.target.value);
                          setPasswordError("");
                          setPasswordSuccess("");
                        }}
                        autoComplete="new-password"
                        aria-invalid={Boolean(passwordError)}
                      />
                    </label>
                    {passwordError ? (
                      <p className="profile-password-message profile-password-message--error" role="alert">
                        {passwordError}
                      </p>
                    ) : null}
                    {passwordSuccess ? (
                      <p className="profile-password-message profile-password-message--success" role="status">
                        {passwordSuccess}
                      </p>
                    ) : null}
                    <div className="profile-password-actions">
                      <button
                        type="submit"
                        className="primary-btn profile-password-save"
                        disabled={passwordSaving}
                      >
                        {passwordSaving ? "Сохранение…" : "Сохранить новый пароль"}
                      </button>
                      <button
                        type="button"
                        className="secondary-btn profile-password-cancel"
                        onClick={resetPasswordForm}
                        disabled={passwordSaving}
                      >
                        Отмена
                      </button>
                    </div>
                  </form>
                )}
              </div>

              {hasUsername ? (
                <div className="profile-username-change-section">
                  {!usernameChangeFormOpen ? (
                    <button
                      type="button"
                      className="secondary-btn profile-username-change-toggle"
                      onClick={() => {
                        setUsernameChangeFormOpen(true);
                        setChangeUsernameInput(displayUser.username || "");
                        setChangeUsernameError("");
                        setChangeUsernameSuccess("");
                      }}
                    >
                      Сменить юзернейм
                    </button>
                  ) : (
                    <form className="profile-username-change-form" onSubmit={handleChangeUsername}>
                      <p className="profile-username-change-form-title">Смена юзернейма</p>
                      <div className="profile-username-input-wrap">
                        <span className="profile-username-prefix" aria-hidden="true">
                          @
                        </span>
                        <input
                          type="text"
                          className="profile-username-input"
                          value={changeUsernameInput}
                          onChange={(event) => {
                            setChangeUsernameInput(event.target.value);
                            setChangeUsernameError("");
                            setChangeUsernameSuccess("");
                          }}
                          placeholder="новый_юзернейм"
                          autoComplete="username"
                          autoCapitalize="off"
                          autoCorrect="off"
                          spellCheck={false}
                          maxLength={25}
                          aria-label="Новый юзернейм"
                          aria-invalid={Boolean(changeUsernameError)}
                        />
                      </div>
                      {changeUsernameError ? (
                        <p className="profile-username-message profile-username-message--error" role="alert">
                          {changeUsernameError}
                        </p>
                      ) : null}
                      {changeUsernameSuccess ? (
                        <p className="profile-username-message profile-username-message--success" role="status">
                          {changeUsernameSuccess}
                        </p>
                      ) : null}
                      <div className="profile-username-change-actions">
                        <button
                          type="submit"
                          className="primary-btn profile-username-change-save"
                          disabled={changeUsernameSaving}
                        >
                          {changeUsernameSaving ? "Сохранение…" : "Сохранить юзернейм"}
                        </button>
                        <button
                          type="button"
                          className="secondary-btn profile-username-change-cancel"
                          onClick={resetUsernameChangeForm}
                          disabled={changeUsernameSaving}
                        >
                          Отмена
                        </button>
                      </div>
                    </form>
                  )}
                </div>
              ) : null}
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
