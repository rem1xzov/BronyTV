import React, { useEffect, useState } from "react";
import { LogIn, LogOut, UserCircle, UserPlus } from "lucide-react";
import { useAuth } from "../auth/AuthContext";
import AuthModal from "./AuthModal";
import ProfileModal from "./ProfileModal";

export default function AuthPanel() {
  const { user, loading, logout } = useAuth();
  const [authModalOpen, setAuthModalOpen] = useState(false);
  const [authModalMode, setAuthModalMode] = useState("signin");
  const [profileOpen, setProfileOpen] = useState(false);

  const openAuthModal = (mode) => {
    setAuthModalMode(mode);
    setAuthModalOpen(true);
  };

  const closeAuthModal = () => {
    setAuthModalOpen(false);
  };

  const openProfile = () => {
    setProfileOpen(true);
  };

  const closeProfile = () => {
    setProfileOpen(false);
  };

  useEffect(() => {
    const handleOpenProfile = () => {
      if (user) {
        setProfileOpen(true);
      }
    };

    window.addEventListener("bronytv:open-profile", handleOpenProfile);
    return () => window.removeEventListener("bronytv:open-profile", handleOpenProfile);
  }, [user]);

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
    return (
      <>
        <div className="sidebar-auth sidebar-auth--signed-in">
          <button
            type="button"
            className="sidebar-profile-btn"
            onClick={openProfile}
            aria-label="Личный кабинет"
          >
            <span className="sidebar-profile-avatar" aria-hidden="true">
              <UserCircle size={18} />
            </span>
            <span className="sidebar-profile-label">
              <span className="sidebar-profile-label-full">Личный кабинет</span>
              <span className="sidebar-profile-label-short">Кабинет</span>
            </span>
          </button>
          <button
            type="button"
            className="sidebar-auth-btn sidebar-auth-btn--logout"
            onClick={logout}
            aria-label="Выйти"
          >
            <LogOut size={16} />
            <span>Выйти</span>
          </button>
        </div>
        <ProfileModal
          isOpen={profileOpen}
          onClose={closeProfile}
          onRequestSignIn={() => openAuthModal("signin")}
        />
      </>
    );
  }

  return (
    <>
      <div className="sidebar-auth">
        <button
          type="button"
          className="sidebar-auth-btn sidebar-auth-btn--primary"
          onClick={() => openAuthModal("signin")}
        >
          <LogIn size={14} />
          <span>Вход</span>
        </button>
        <button type="button" className="sidebar-auth-btn" onClick={() => openAuthModal("signup")}>
          <UserPlus size={14} />
          <span>Регистрация</span>
        </button>
      </div>
      <AuthModal
        isOpen={authModalOpen}
        mode={authModalMode}
        onClose={closeAuthModal}
        onSwitchMode={setAuthModalMode}
      />
    </>
  );
}
