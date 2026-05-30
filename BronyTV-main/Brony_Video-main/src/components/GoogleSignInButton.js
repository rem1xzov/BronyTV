import React from "react";
import { GoogleLogin } from "@react-oauth/google";
import { useAuth } from "../auth/AuthContext";

export default function GoogleSignInButton() {
  const { loginWithGoogleCredential, logout, user, loading } = useAuth();

  if (loading) {
    return <div className="auth-widget muted">Загрузка…</div>;
  }

  if (user) {
    return (
      <div className="auth-widget">
        <span className="auth-user-label" title={user.email}>
          {user.displayName}
        </span>
        <button type="button" className="secondary-btn auth-logout-btn" onClick={logout}>
          Выйти
        </button>
      </div>
    );
  }

  const clientId = process.env.REACT_APP_GOOGLE_CLIENT_ID;
  if (!clientId) {
    return <div className="auth-widget muted">Google Client ID не настроен</div>;
  }

  return (
    <div className="auth-widget">
      <GoogleLogin
        onSuccess={async (response) => {
          if (!response?.credential) {
            return;
          }
          try {
            await loginWithGoogleCredential(response.credential);
          } catch (error) {
            // eslint-disable-next-line no-alert
            alert("Не удалось войти через Google.");
          }
        }}
        onError={() => {
          // eslint-disable-next-line no-alert
          alert("Ошибка входа через Google.");
        }}
        useOneTap={false}
        theme="filled_black"
        shape="pill"
        text="signin_with"
        locale="ru"
      />
    </div>
  );
}
