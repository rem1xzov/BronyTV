import React, { useEffect, useId, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { LogIn, UserPlus, X } from "lucide-react";
import { RACE_OPTIONS, useAuth } from "../auth/AuthContext";
import { getRaceLabel } from "../auth/race";

export default function AuthModal({ isOpen, mode, onClose, onSwitchMode }) {
  const { login, register } = useAuth();
  const titleId = useId();
  const firstFieldRef = useRef(null);
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [race, setRace] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const isSignup = mode === "signup";

  useEffect(() => {
    if (!isOpen) {
      return undefined;
    }
    setEmail("");
    setPassword("");
    setRace("");
    setError("");
    setSuccess("");
    setSubmitting(false);

    const timer = window.setTimeout(() => {
      firstFieldRef.current?.focus();
    }, 0);

    const handleKeyDown = (event) => {
      if (event.key === "Escape") {
        onClose();
      }
    };

    document.body.style.overflow = "hidden";
    window.addEventListener("keydown", handleKeyDown);

    return () => {
      window.clearTimeout(timer);
      document.body.style.overflow = "";
      window.removeEventListener("keydown", handleKeyDown);
    };
  }, [isOpen, mode, onClose]);

  if (!isOpen) {
    return null;
  }

  const handleSubmit = async (event) => {
    event.preventDefault();
    setSubmitting(true);
    setError("");
    setSuccess("");

    try {
      if (isSignup) {
        if (!race) {
          setError("Выберите расу пони — выбор нельзя изменить позже.");
          return;
        }
        await register({ email, password, race });
        setSuccess(`Добро пожаловать! Ваша раса: ${getRaceLabel(race)}.`);
      } else {
        await login({ email, password });
        setSuccess("Вы успешно вошли в аккаунт.");
      }
      window.setTimeout(() => {
        onClose();
      }, 700);
    } catch (submitError) {
      setError(submitError.message || "Ошибка авторизации.");
    } finally {
      setSubmitting(false);
    }
  };

  const handleBackdropClick = (event) => {
    if (event.target === event.currentTarget) {
      onClose();
    }
  };

  return createPortal(
    <div className="auth-modal-overlay" onClick={handleBackdropClick} role="presentation">
      <div
        className="auth-modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        onClick={(event) => event.stopPropagation()}
      >
        <button type="button" className="auth-modal-close" onClick={onClose} aria-label="Закрыть">
          <X size={20} />
        </button>

        <div className="auth-modal-header">
          <div className="auth-modal-icon" aria-hidden="true">
            {isSignup ? <UserPlus size={28} /> : <LogIn size={28} />}
          </div>
          <h2 id={titleId}>{isSignup ? "Регистрация" : "Вход"}</h2>
          <p className="auth-modal-subtitle">
            {isSignup
              ? "Создайте аккаунт BronyTV и выберите расу пони навсегда."
              : "Войдите, чтобы продолжить просмотр на BronyTV."}
          </p>
        </div>

        <form className="auth-modal-form" onSubmit={handleSubmit}>
          <label className="auth-modal-field">
            <span>Email</span>
            <input
              ref={firstFieldRef}
              type="email"
              autoComplete="email"
              required
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              placeholder="you@example.com"
            />
          </label>

          <label className="auth-modal-field">
            <span>Пароль</span>
            <input
              type="password"
              autoComplete={isSignup ? "new-password" : "current-password"}
              required
              minLength={8}
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              placeholder="Минимум 8 символов"
            />
          </label>

          {isSignup ? (
            <div className="auth-modal-field auth-modal-field--race">
              <label htmlFor="auth-race-select">Раса пони</label>
              <div className="auth-modal-select-wrap">
                <select
                  id="auth-race-select"
                  required
                  value={race}
                  onChange={(event) => setRace(event.target.value)}
                >
                  <option value="">— выберите расу —</option>
                  {RACE_OPTIONS.map((option) => (
                    <option key={option.id} value={option.id}>
                      {option.label}
                    </option>
                  ))}
                </select>
              </div>
              <p className="auth-modal-notice">
                Выбор расы навсегда и не подлежит изменению.
              </p>
            </div>
          ) : null}

          {error ? (
            <div className="auth-modal-message auth-modal-message--error" role="alert">
              {error}
            </div>
          ) : null}

          {success ? (
            <div className="auth-modal-message auth-modal-message--success" role="status">
              {success}
            </div>
          ) : null}

          <button type="submit" className="primary-btn auth-modal-submit" disabled={submitting}>
            {submitting ? "Подождите…" : isSignup ? "Зарегистрироваться" : "Войти"}
          </button>
        </form>

        <p className="auth-modal-switch">
          {isSignup ? (
            <>
              Уже есть аккаунт?{" "}
              <button type="button" className="auth-modal-link" onClick={() => onSwitchMode("signin")}>
                Войти
              </button>
            </>
          ) : (
            <>
              Нет аккаунта?{" "}
              <button type="button" className="auth-modal-link" onClick={() => onSwitchMode("signup")}>
                Зарегистрироваться
              </button>
            </>
          )}
        </p>
      </div>
    </div>,
    document.body
  );
}
