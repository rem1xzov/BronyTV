import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import { apiFetch } from "./api";

const AuthContext = createContext(null);

export const RACE_OPTIONS = [
  {
    id: "pegasus",
    title: "Пегасы",
    subtitle: "Pegasi",
    description: "Быстрые и отважные небесные пони с крыльями."
  },
  {
    id: "unicorn",
    title: "Единороги",
    subtitle: "Unicorns",
    description: "Магически одарённые пони с рогом."
  },
  {
    id: "earth_pony",
    title: "Земные пони",
    subtitle: "Earth Ponies",
    description: "Сильные и надёжные хранители природы."
  }
];

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);
  const [raceModalOpen, setRaceModalOpen] = useState(false);

  const refreshUser = useCallback(async () => {
    try {
      const response = await apiFetch("/api/auth/me");
      if (!response.ok) {
        setUser(null);
        setRaceModalOpen(false);
        return null;
      }
      const profile = await response.json();
      setUser(profile);
      setRaceModalOpen(Boolean(profile.needsRaceSelection));
      return profile;
    } catch (error) {
      setUser(null);
      setRaceModalOpen(false);
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

  const loginWithGoogleCredential = useCallback(
    async (credential) => {
      const response = await apiFetch("/api/auth/google", {
        method: "POST",
        body: JSON.stringify({ idToken: credential })
      });
      if (!response.ok) {
        throw new Error("Google authentication failed.");
      }
      const profile = await response.json();
      setUser(profile);
      setRaceModalOpen(Boolean(profile.needsRaceSelection));
      return profile;
    },
    []
  );

  const selectRace = useCallback(async (race) => {
    const response = await apiFetch("/api/auth/select-race", {
      method: "POST",
      body: JSON.stringify({ race })
    });
    if (!response.ok) {
      throw new Error("Race selection failed.");
    }
    const profile = await response.json();
    setUser(profile);
    setRaceModalOpen(false);
    return profile;
  }, []);

  const logout = useCallback(async () => {
    await apiFetch("/api/auth/logout", { method: "POST" });
    setUser(null);
    setRaceModalOpen(false);
  }, []);

  const value = useMemo(
    () => ({
      user,
      loading,
      raceModalOpen,
      isAuthenticated: Boolean(user),
      loginWithGoogleCredential,
      selectRace,
      logout,
      refreshUser
    }),
    [user, loading, raceModalOpen, loginWithGoogleCredential, selectRace, logout, refreshUser]
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
