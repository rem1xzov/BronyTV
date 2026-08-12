import React, { useEffect, useId, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { LogIn, MailCheck, UserPlus, X } from "lucide-react";
import { RACE_OPTIONS, useAuth } from "../auth/AuthContext";
import { validateUsername } from "../auth/username";

export default function AuthModal({ isOpen, mode, onClose, onSwitchMode }) {
  const { login, register, confirmEmail, resendEmailConfirmation } = useAuth();
  const titleId = useId();
  const firstFieldRef = useRef(null);
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [username, setUsername] = useState("");
  const [race, setRace] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [confirmEmailAddress, setConfirmEmailAddress] = useState("");
  const [confirmCode, setConfirmCode] = useState("");
  const [confirmError, setConfirmError] = useState("");
  const [confirmSuccess, setConfirmSuccess] = useState("");
  const [confirmSubmitting, setConfirmSubmitting] = useState(false);
  const [resendCooldown, setResendCooldown] = useState(0);
  const [resendSending, setResendSending] = useState(false);

  const isSignup = mode === "signup";

  useEffect(() => {
    if (!isOpen) {
      return undefined;
    }
    setEmail("");
    setPassword("");
    setUsername("");
    setRace("");
    setError("");
    setSuccess("");
    setSubmitting(false);
    setConfirmOpen(false);
    setConfirmEmailAddress("");
    setConfirmCode("");
    setConfirmError("");
    setConfirmSuccess("");
    setConfirmSubmitting(false);
    setResendCooldown(0);
    setResendSending(false);

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

  // Auto-resend cooldown (60 seconds) for the 6-digit confirmation code.
  useEffect(() => {
    if (!confirmOpen || resendCooldown <= 0) {
      return undefined;
    }
    const id = window.setTimeout(() => setResendCooldown((value) => value - 1), 1000);
    return () => window.clearTimeout(id);
  }, [confirmOpen, resendCooldown]);

  if (!isOpen) {
    return null;
  }

  const handleSubmit = async (event) => {
    event.preventDefault();
    setSubmitting(true);
    setError("");
    setSuccess("");

    const handleEnterConfirmation = (confirmationEmail) => {
      setConfirmEmailAddress(confirmationEmail);
      setConfirmCode("");
      setConfirmError("");
      setConfirmSuccess("");
      setResendCooldown(60);
      setConfirmOpen(true);
    };

    try {
      if (isSignup) {
        if (!race) {
          setError("Выберите расу пони — выбор нельзя изменить позже.");
          return;
        }

        const usernameValidation = validateUsername(username);
        if (!usernameValidation.valid) {
          setError(usernameValidation.error);
          return;
        }

        const registration = await register({
          email,
          password,
          race,
          username: usernameValidation.value
        });
        handleEnterConfirmation(registration?.email || email.trim().toLowerCase());
        return;
      } else {
        await login({ email, password });
        setSuccess("Вы успешно вошли в аккаунт.");
      }
      window.setTimeout(() => {
        onClose();
      }, 700);
    } catch (submitError) {
      if (submitError.requiresEmailConfirmation) {
        handleEnterConfirmation(submitError.email || email.trim().toLowerCase());
        return;
      }
      setError(submitError.message || "Ошибка авторизации.");
    } finally {
      setSubmitting(false);
    }
  };

  const handleConfirmCode = async (event) => {
    event.preventDefault();
    const code = confirmCode.trim();
    if (!/^\d{6}$/.test(code)) {
      setConfirmError("Введите 6-значный код из письма.");
      return;
    }
    setConfirmSubmitting(true);
    setConfirmError("");
    setConfirmSuccess("");
    try {
      await confirmEmail({ email: confirmEmailAddress, token: code });
      setConfirmSuccess("Email подтверждён! Вы вошли в аккаунт.");
      window.setTimeout(() => {
        onClose();
      }, 700);
    } catch (confirmCallError) {
      setConfirmError(confirmCallError.message || "Не удалось подтвердить код. Попробуйте ещё раз.");
    } finally {
      setConfirmSubmitting(false);
    }
  };

  const handleResendCode = async () => {
    if (resendSending || resendCooldown > 0) {
      return;
    }
    setResendSending(true);
    setConfirmError("");
    try {
      await resendEmailConfirmation(confirmEmailAddress);
      setResendCooldown(60);
      setConfirmSuccess("Новый код отправлен. Проверьте почту.");
    } catch (resendError) {
      setConfirmError(resendError.message || "Не удалось отправить код. Попробуйте позже.");
    } finally {
      setResendSending(false);
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

        {confirmOpen ? (
          <div className="auth-modal-confirm">
            <div className="auth-modal-icon" aria-hidden="true">
              <MailCheck size={28} />
            </div>
            <h2 id={titleId}>Подтверждение email</h2>
            <p className="auth-modal-subtitle">
              Мы отправили 6-значный код на вашу почту{" "}
              <span className="auth-modal-confirm-email">{confirmEmailAddress}</span>. Введите его, чтобы
              завершить регистрацию и войти в аккаунт.
            </p>

            <form className="auth-modal-form" onSubmit={handleConfirmCode}>
              <label className="auth-modal-field">
                <span>6-значный код</span>
                <input
                  type="text"
                  inputMode="numeric"
                  autoComplete="one-time-code"
                  required
                  minLength={6}
                  maxLength={6}
                  value={confirmCode}
                  onChange={(event) => {
                    setConfirmCode(event.target.value.replace(/\D/g, ""));
                    setConfirmError("");
                    setConfirmSuccess("");
                  }}
                  placeholder="••••••"
                  autoFocus
                />
              </label>

              {confirmError ? (
                <div className="auth-modal-message auth-modal-message--error" role="alert">
                  {confirmError}
                </div>
              ) : null}

              {confirmSuccess ? (
                <div className="auth-modal-message auth-modal-message--success" role="status">
                  {confirmSuccess}
                </div>
              ) : null}

              <button type="submit" className="primary-btn auth-modal-submit" disabled={confirmSubmitting}>
                {confirmSubmitting ? "Проверяем…" : "Подтвердить"}
              </button>

              <button
                type="button"
                className="auth-modal-link auth-modal-resend"
                onClick={handleResendCode}
                disabled={resendSending || resendCooldown > 0}
              >
                {resendCooldown > 0
                  ? `Отправить код повторно (${resendCooldown} с)`
                  : resendSending
                    ? "Отправляем…"
                    : "Отправить код повторно"}
              </button>

              <p className="auth-modal-confirm-hint auth-modal-confirm-hint--muted">
                Код отправлен на ваш email. Если письмо не приходит в течение минуты, обязательно проверьте папку «Спам».
              </p>
            </form>
          </div>
        ) : (
        <>
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
            <label className="auth-modal-field">
              <span>Юзернейм</span>
              <div className="auth-modal-username-wrap">
                <span className="auth-modal-username-prefix" aria-hidden="true">
                  @
                </span>
                <input
                  type="text"
                  required
                  value={username}
                  onChange={(event) => setUsername(event.target.value)}
                  placeholder="rainbowdash"
                  autoComplete="username"
                  autoCapitalize="off"
                  autoCorrect="off"
                  spellCheck={false}
                  maxLength={25}
                />
              </div>
              <p className="auth-modal-notice">4–25 символов: латиница, цифры и _</p>
            </label>
          ) : null}

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
        </>
        )}
      </div>
    </div>,
    document.body
  );
}
