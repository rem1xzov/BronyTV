import React, { useState } from "react";
import { RACE_OPTIONS, useAuth } from "../auth/AuthContext";

const RACE_LABEL_BY_ID = RACE_OPTIONS.reduce((acc, race) => {
  acc[race.id] = race.label;
  return acc;
}, {});

export default function AuthPanel() {
  const { user, loading, login, register, logout } = useAuth();
  const [mode, setMode] = useState("signin");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [race, setRace] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");

  if (loading) {
    return <div className="auth-panel muted">Загрузка…</div>;
  }

  if (user) {
    return (
      <div className="auth-panel auth-panel--signed-in">
        <p className="auth-panel-email" title={user.email}>
          {user.email}
        </p>
        <p className="auth-panel-race muted">Раса: {RACE_LABEL_BY_ID[user.race] || user.race}</p>
        <button type="button" className="secondary-btn auth-panel-btn" onClick={logout}>
          Выйти
        </button>
      </div>
    );
  }

  const handleSubmit = async (event) => {
    event.preventDefault();
    setSubmitting(true);
    setError("");
    try {
      if (mode === "signup") {
        if (!race) {
          setError("Выберите расу пони — выбор нельзя изменить позже.");
          return;
        }
        await register({ email, password, race });
      } else {
        await login({ email, password });
      }
    } catch (submitError) {
      setError(submitError.message || "Ошибка авторизации.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="auth-panel">
      <div className="auth-panel-tabs">
        <button
          type="button"
          className={`auth-panel-tab${mode === "signin" ? " is-active" : ""}`}
          onClick={() => {
            setMode("signin");
            setError("");
          }}
        >
          Вход
        </button>
        <button
          type="button"
          className={`auth-panel-tab${mode === "signup" ? " is-active" : ""}`}
          onClick={() => {
            setMode("signup");
            setError("");
          }}
        >
          Регистрация
        </button>
      </div>
      <form className="auth-panel-form" onSubmit={handleSubmit}>
        <label className="auth-field">
          <span>Email</span>
          <input
            type="email"
            autoComplete="email"
            required
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            placeholder="you@example.com"
          />
        </label>
        <label className="auth-field">
          <span>Пароль</span>
          <input
            type="password"
            autoComplete={mode === "signup" ? "new-password" : "current-password"}
            required
            minLength={8}
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            placeholder="Минимум 8 символов"
          />
        </label>
        {mode === "signup" ? (
          <label className="auth-field">
            <span>Выберите вашу расу пони</span>
            <select required value={race} onChange={(event) => setRace(event.target.value)}>
              <option value="">— выберите расу —</option>
              {RACE_OPTIONS.map((option) => (
                <option key={option.id} value={option.id}>
                  {option.label}
                </option>
              ))}
            </select>
            <small className="auth-field-hint">Выбор рассы навсегда и не подлежит изменению.</small>
          </label>
        ) : null}
        {error ? <p className="auth-panel-error">{error}</p> : null}
        <button type="submit" className="primary-btn auth-panel-btn" disabled={submitting}>
          {submitting ? "Подождите…" : mode === "signup" ? "Зарегистрироваться" : "Войти"}
        </button>
      </form>
    </div>
  );
}
