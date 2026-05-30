import React, { useEffect, useId, useState } from "react";
import { createPortal } from "react-dom";
import { AlertCircle, LogIn, UserCircle, X } from "lucide-react";
import { useAuth } from "../auth/AuthContext";
import { getRaceBadgeClassName, getRaceLabel } from "../auth/race";

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
  const { user, refreshUser, logout } = useAuth();
  const titleId = useId();
  const [profileLoading, setProfileLoading] = useState(false);
  const [sessionExpired, setSessionExpired] = useState(false);
  const [fetchError, setFetchError] = useState("");

  useEffect(() => {
    if (!isOpen) {
      return undefined;
    }

    let cancelled = false;

    const loadProfile = async () => {
      setProfileLoading(true);
      setSessionExpired(false);
      setFetchError("");

      try {
        const profile = await refreshUser();
        if (cancelled) {
          return;
        }
        if (!profile) {
          setSessionExpired(true);
        }
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

    const handleKeyDown = (event) => {
      if (event.key === "Escape") {
        onClose();
      }
    };

    document.body.style.overflow = "hidden";
    window.addEventListener("keydown", handleKeyDown);

    return () => {
      cancelled = true;
      document.body.style.overflow = "";
      window.removeEventListener("keydown", handleKeyDown);
    };
  }, [isOpen, onClose, refreshUser]);

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

  const displayUser = user;
  const raceLabel = getRaceLabel(displayUser?.race);

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
          {profileLoading ? (
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
                <button
                  type="button"
                  className="primary-btn profile-modal-action-btn"
                  onClick={() => {
                    setFetchError("");
                    setProfileLoading(true);
                    refreshUser()
                      .then((profile) => {
                        if (!profile) {
                          setSessionExpired(true);
                        }
                      })
                      .catch(() => {
                        setFetchError("Не удалось загрузить профиль. Проверьте подключение и попробуйте снова.");
                      })
                      .finally(() => setProfileLoading(false));
                  }}
                >
                  Повторить
                </button>
                <button type="button" className="secondary-btn profile-modal-action-btn" onClick={onClose}>
                  Закрыть
                </button>
              </div>
            </div>
          ) : displayUser ? (
            <div className="profile-modal-content">
              <div className="profile-modal-avatar" aria-hidden="true">
                <UserCircle size={40} />
              </div>

              <dl className="profile-details">
                <div className="profile-detail-row">
                  <dt>Ваш Email</dt>
                  <dd className="profile-detail-email" title={displayUser.email}>
                    {displayUser.email}
                  </dd>
                </div>

                <div className="profile-detail-row profile-detail-row--race">
                  <dt>Раса</dt>
                  <dd>
                    <span className={getRaceBadgeClassName(displayUser.race)}>
                      {raceLabel}
                    </span>
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
