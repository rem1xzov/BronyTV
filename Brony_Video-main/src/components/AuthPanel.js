import React, { useState } from "react";
import { LogIn, LogOut, UserPlus } from "lucide-react";
import { RACE_OPTIONS, useAuth } from "../auth/AuthContext";
import AuthModal from "./AuthModal";

const RACE_LABEL_BY_ID = RACE_OPTIONS.reduce((acc, race) => {
  acc[race.id] = race.label;
  return acc;
}, {});

const getEmailPrefix = (email) => {
  if (!email || typeof email !== "string") {
    return "Пользователь";
  }
  const prefix = email.split("@")[0]?.trim();
  return prefix || email;
};

export default function AuthPanel() {
  const { user, loading, logout } = useAuth();
  const [modalOpen, setModalOpen] = useState(false);
  const [modalMode, setModalMode] = useState("signin");

  const openModal = (mode) => {
    setModalMode(mode);
    setModalOpen(true);
  };

  const closeModal = () => {
    setModalOpen(false);
  };

  if (loading) {
    return (
      <div className="sidebar-auth">
        <div className="sidebar-auth-loading nav-pill" aria-busy="true">
          <span>…</span>
        </div>
      </div>
    );
  }

  if (user) {
    const raceLabel = RACE_LABEL_BY_ID[user.race] || user.race;

    return (
      <div className="sidebar-auth sidebar-auth--signed-in">
        <div className="sidebar-user-card" title={user.email}>
          <span className="sidebar-user-name">{getEmailPrefix(user.email)}</span>
          <span className="sidebar-user-race">{raceLabel}</span>
        </div>
        <button type="button" className="sidebar-auth-btn sidebar-auth-btn--logout" onClick={logout}>
          <LogOut size={14} />
          <span>Выйти</span>
        </button>
      </div>
    );
  }

  return (
    <>
      <div className="sidebar-auth">
        <button type="button" className="sidebar-auth-btn sidebar-auth-btn--primary" onClick={() => openModal("signin")}>
          <LogIn size={14} />
          <span>Вход</span>
        </button>
        <button type="button" className="sidebar-auth-btn" onClick={() => openModal("signup")}>
          <UserPlus size={14} />
          <span>Регистрация</span>
        </button>
      </div>
      <AuthModal
        isOpen={modalOpen}
        mode={modalMode}
        onClose={closeModal}
        onSwitchMode={setModalMode}
      />
    </>
  );
}
