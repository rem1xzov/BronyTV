import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import { apiFetch } from "./api";
import { normalizeAuthUser } from "./user";

export { normalizeAuthUser };

const AuthContext = createContext(null);

export const RACE_OPTIONS = [
  { id: "pegasus", label: "пегасы" },
  { id: "unicorn", label: "единороги" },
  { id: "earth_pony", label: "земные пони" }
];

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);

  const refreshUser = useCallback(async () => {
    try {
      const response = await apiFetch("/auth/me");
      if (!response.ok) {
        setUser(null);
        return null;
      }
      const profile = normalizeAuthUser(await response.json());
      setUser(profile);
      return profile;
    } catch (error) {
      setUser(null);
      return null;
    }
  }, []);

  useEffect(() => {
    let active = true;
    (async () => {
      await refreshUser();
      if (active) {
        setLoading(false);
      }
    })();
    return () => {
      active = false;
    };
  }, [refreshUser]);

  const register = useCallback(async ({ email, password, race, username }) => {
    const response = await apiFetch("/auth/register", {
      method: "POST",
      body: JSON.stringify({ email, password, race, username })
    });
    const raw = await response.json().catch(() => ({}));
    if (!response.ok) {
      throw new Error(raw.message || "Не удалось зарегистрироваться.");
    }
    const requiresConfirmation = Boolean(
      raw.requiresEmailConfirmation ?? raw.RequiresEmailConfirmation
    );
    if (requiresConfirmation) {
      // Account created, but the email must be confirmed with a 6-digit code.
      return { email: raw.email ?? email, requiresEmailConfirmation: true };
    }
    const payload = normalizeAuthUser(raw);
    setUser(payload);
    return payload;
  }, []);

  const login = useCallback(async ({ email, password }) => {
    const response = await apiFetch("/auth/signin", {
      method: "POST",
      body: JSON.stringify({ email, password })
    });
    const raw = await response.json().catch(() => ({}));
    if (!response.ok) {
      if (raw.requiresEmailConfirmation) {
        const confirmationError = new Error(
          raw.message || "Email ещё не подтверждён. Введите код из письма."
        );
        confirmationError.requiresEmailConfirmation = true;
        confirmationError.email = raw.email ?? email;
        throw confirmationError;
      }
      throw new Error(raw.message || "Неверный email или пароль.");
    }
    const payload = normalizeAuthUser(raw);
    setUser(payload);
    return payload;
  }, []);

  const logout = useCallback(async () => {
    await apiFetch("/auth/logout", { method: "POST" });
    setUser(null);
  }, []);

  const updateUsername = useCallback(async (username) => {
    const response = await apiFetch("/auth/update-username", {
      method: "PUT",
      body: JSON.stringify({ username })
    });
    const raw = await response.json().catch(() => ({}));
    if (!response.ok) {
      throw new Error(raw.message || "Не удалось сохранить юзернейм.");
    }
    const payload = normalizeAuthUser(raw);
    setUser(payload);
    return payload;
  }, []);

  const updatePassword = useCallback(async ({ newPassword, confirmPassword }) => {
    const response = await apiFetch("/auth/update-password", {
      method: "PUT",
      body: JSON.stringify({ newPassword, confirmPassword })
    });
    const raw = await response.json().catch(() => ({}));
    if (!response.ok) {
      throw new Error(raw.message || "Не удалось изменить пароль.");
    }
    return raw;
  }, []);

  const updateAvatarEmoji = useCallback(async (emoji) => {
    const response = await apiFetch("/auth/update-avatar-emoji", {
      method: "PUT",
      body: JSON.stringify({ emoji })
    });
    const raw = await response.json().catch(() => ({}));
    if (!response.ok) {
      throw new Error(raw.message || "Не удалось сохранить эмодзи.");
    }
    const payload = normalizeAuthUser(raw);
    setUser(payload);
    return payload;
  }, []);

  const confirmEmail = useCallback(async ({ email, token }) => {
    const response = await apiFetch("/auth/confirm-email", {
      method: "POST",
      body: JSON.stringify({ email, token })
    });
    const raw = await response.json().catch(() => ({}));
    if (!response.ok) {
      throw new Error(raw.message || "Не удалось подтвердить email.");
    }
    // A successful confirmation activates the account and returns the user.
    const payload = normalizeAuthUser(raw);
    if (payload) {
      setUser(payload);
    }
    return payload ?? raw;
  }, []);

  const resendEmailConfirmation = useCallback(async (email) => {
    const response = await apiFetch("/auth/resend-email-confirmation", {
      method: "POST",
      body: JSON.stringify({ email })
    });
    const raw = await response.json().catch(() => ({}));
    if (!response.ok) {
      throw new Error(raw.message || "Не удалось отправить письмо.");
    }
    return raw;
  }, []);

  const value = useMemo(
    () => ({
      user,
      loading,
      isAuthenticated: Boolean(user),
      register,
      login,
      logout,
      refreshUser,
      updateUsername,
      updatePassword,
      updateAvatarEmoji,
      confirmEmail,
      resendEmailConfirmation
    }),
    [
      user,
      loading,
      register,
      login,
      logout,
      refreshUser,
      updateUsername,
      updatePassword,
      updateAvatarEmoji,
      confirmEmail,
      resendEmailConfirmation
    ]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within AuthProvider.");
  }
  return context;
};
