import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import { apiFetch } from "./api";

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
      const response = await apiFetch("/api/auth/me");
      if (!response.ok) {
        setUser(null);
        return null;
      }
      const profile = await response.json();
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

  const register = useCallback(async ({ email, password, race }) => {
    const response = await apiFetch("/api/auth/register", {
      method: "POST",
      body: JSON.stringify({ email, password, race })
    });
    const payload = await response.json().catch(() => ({}));
    if (!response.ok) {
      throw new Error(payload.message || "Не удалось зарегистрироваться.");
    }
    setUser(payload);
    return payload;
  }, []);

  const login = useCallback(async ({ email, password }) => {
    const response = await apiFetch("/api/auth/signin", {
      method: "POST",
      body: JSON.stringify({ email, password })
    });
    const payload = await response.json().catch(() => ({}));
    if (!response.ok) {
      throw new Error(payload.message || "Неверный email или пароль.");
    }
    setUser(payload);
    return payload;
  }, []);

  const logout = useCallback(async () => {
    await apiFetch("/api/auth/logout", { method: "POST" });
    setUser(null);
  }, []);

  const value = useMemo(
    () => ({
      user,
      loading,
      isAuthenticated: Boolean(user),
      register,
      login,
      logout,
      refreshUser
    }),
    [user, loading, register, login, logout, refreshUser]
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
