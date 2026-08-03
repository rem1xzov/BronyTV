import React, { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { AlertCircle, CheckCircle2, Loader2 } from "lucide-react";
import { useAuth } from "../auth/AuthContext";

function useQueryParams() {
  const params = new URLSearchParams(window.location.hash.split("?")[1] || "");
  return {
    token: params.get("token"),
    email: params.get("email")
  };
}

export default function ConfirmEmail() {
  const { confirmEmail, isAuthenticated, refreshUser } = useAuth();
  const { token, email } = useQueryParams();
  const [status, setStatus] = useState("loading"); // 'loading' | 'success' | 'error'
  const [message, setMessage] = useState("");
  const [hasToken, setHasToken] = useState(false);

  useEffect(() => {
    let active = true;
    const runConfirmation = async () => {
      setHasToken(Boolean(token && email));
      if (!token || !email) {
        setStatus("error");
        setMessage("Неверная или неполная ссылка подтверждения. Запросите письмо повторно.");
        return;
      }

      setStatus("loading");
      setMessage("");

      try {
        await confirmEmail({ email, token });
        if (!active) {
          return;
        }
        setStatus("success");
        setMessage("Email подтверждён! Теперь вы можете пользоваться всеми функциями BronyTV.");
        if (isAuthenticated) {
          await refreshUser();
        }
      } catch (error) {
        if (!active) {
          return;
        }
        setStatus("error");
        setMessage(error.message || "Не удалось подтвердить email. Попробуйте ещё раз.");
      }
    };

    runConfirmation();

    return () => {
      active = false;
    };
  }, [token, email, isAuthenticated, refreshUser, confirmEmail]);

  return (
    <div className="panel confirm-email-page">
      <div className={`confirm-email-card confirm-email-card--${status}`}>
        {status === "loading" && hasToken ? (
          <>
            <div className="confirm-email-icon" aria-hidden="true">
              <Loader2 className="confirm-email-spinner" size={40} />
            </div>
            <h2>Подтверждение email</h2>
            <p className="muted">Проверяем вашу ссылку…</p>
          </>
        ) : null}

        {status === "success" ? (
          <>
            <div className="confirm-email-icon confirm-email-icon--success" aria-hidden="true">
              <CheckCircle2 size={44} />
            </div>
            <h2>Email подтверждён</h2>
            <p>{message}</p>
            <div className="button-row">
              <Link className="primary-btn" to="/">
                На главную
              </Link>
            </div>
          </>
        ) : null}

        {status === "error" ? (
          <>
            <div className="confirm-email-icon confirm-email-icon--error" aria-hidden="true">
              <AlertCircle size={40} />
            </div>
            <h2>Не удалось подтвердить</h2>
            <p>{message}</p>
            <div className="button-row">
              <Link className="primary-btn" to="/">
                На главную
              </Link>
            </div>
          </>
        ) : null}
      </div>
    </div>
  );
}
