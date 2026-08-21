import React, { useCallback, useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import {
  Check,
  Copy,
  Download,
  ExternalLink,
  Gift,
  Link as LinkIcon,
  LogIn,
  Plus,
  RefreshCw,
  Shield,
  X
} from "lucide-react";
import { useI18n } from "../i18n";
import {
  activateVpnPromo,
  fetchVpnStatus,
  reviveVpn,
  startVpnTrial
} from "../vpn/api";

const copyText = async (text) => {
  try {
    await navigator.clipboard.writeText(text);
    return true;
  } catch {
    const textarea = document.createElement("textarea");
    textarea.value = text;
    textarea.style.position = "fixed";
    textarea.style.opacity = "0";
    document.body.appendChild(textarea);
    textarea.select();
    try {
      document.execCommand("copy");
      return true;
    } catch {
      return false;
    } finally {
      document.body.removeChild(textarea);
    }
  }
};

export default function VpnModal({ isOpen, onClose, isAuthenticated, onRequestSignIn }) {
  const { t } = useI18n();
  const [status, setStatus] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [trialBusy, setTrialBusy] = useState(false);
  const [promoCode, setPromoCode] = useState("");
  const [promoBusy, setPromoBusy] = useState(false);
  const [promoMessage, setPromoMessage] = useState("");
  const [copied, setCopied] = useState("");

  const loadStatus = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const response = await fetchVpnStatus();
      const raw = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(raw.message || "Не удалось загрузить статус VPN.");
      }
      setStatus(raw);
    } catch (loadError) {
      setError(loadError.message || "Не удалось загрузить статус VPN.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (isOpen && isAuthenticated) {
      loadStatus();
    } else if (isOpen && !isAuthenticated) {
      setLoading(false);
      setStatus(null);
    }
  }, [isOpen, isAuthenticated, loadStatus]);

  useEffect(() => {
    if (!isOpen) {
      return undefined;
    }
    const handleKeyDown = (event) => {
      if (event.key === "Escape") {
        onClose();
      }
    };
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    window.addEventListener("keydown", handleKeyDown);
    return () => {
      window.removeEventListener("keydown", handleKeyDown);
      document.body.style.overflow = previousOverflow;
    };
  }, [isOpen, onClose]);

  if (!isOpen) {
    return null;
  }

  const handleStartTrial = async () => {
    setTrialBusy(true);
    setError("");
    try {
      const response = await startVpnTrial();
      const raw = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(raw.message || "Не удалось активировать trial.");
      }
      await loadStatus();
    } catch (trialError) {
      setError(trialError.message || "Не удалось активировать trial.");
    } finally {
      setTrialBusy(false);
    }
  };

  const handleActivatePromo = async (event) => {
    event.preventDefault();
    setPromoBusy(true);
    setPromoMessage("");
    setError("");
    try {
      const response = await activateVpnPromo(promoCode.trim());
      const raw = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(raw.message || "Не удалось активировать промо-код.");
      }
      setPromoMessage("Промо-код активирован!");
      setPromoCode("");
      await loadStatus();
    } catch (promoError) {
      setPromoMessage("");
      setError(promoError.message || "Не удалось активировать промо-код.");
    } finally {
      setPromoBusy(false);
    }
  };

  const handleCopy = async (key, text) => {
    const ok = await copyText(text);
    if (ok) {
      setCopied(key);
      window.setTimeout(() => setCopied(""), 1600);
    }
  };

  const referralLink = status?.referralCode
    ? `${window.location.origin}/?ref=${status.referralCode}`
    : "";

  return createPortal(
    <div className="vpn-modal-overlay" onClick={onClose} role="presentation">
      <div
        className="vpn-modal vpn-modal--full"
        role="dialog"
        aria-modal="true"
        onClick={(event) => event.stopPropagation()}
      >
        <button type="button" className="vpn-modal-close" onClick={onClose} aria-label={t("vpn.close")}>
          <X size={20} />
        </button>

        <div className="vpn-modal-icon" aria-hidden="true">
          <Shield size={30} />
        </div>
        <h2>{t("vpn.modalTitle")}</h2>

        {!isAuthenticated ? (
          <div className="vpn-modal-state">
            <p className="vpn-modal-text">{t("vpn.loginPrompt")}</p>
            <div className="vpn-modal-actions">
              <button type="button" className="primary-btn vpn-modal-close-btn" onClick={onRequestSignIn}>
                <LogIn size={16} />
                <span>{t("vpn.signin")}</span>
              </button>
            </div>
          </div>
        ) : loading ? (
          <div className="vpn-modal-state" aria-live="polite">
            <div className="vpn-loader" aria-hidden="true" />
            <p className="vpn-modal-text">{t("vpn.loading")}</p>
          </div>
        ) : error && !status ? (
          <div className="vpn-modal-state">
            <p className="vpn-modal-text vpn-error-text">{error}</p>
            <div className="vpn-modal-actions">
              <button type="button" className="secondary-btn vpn-modal-close-btn" onClick={loadStatus}>
                <RefreshCw size={16} />
                <span>{t("vpn.retry")}</span>
              </button>
            </div>
          </div>
        ) : !status?.enabled ? (
          <div className="vpn-modal-state">
            <p className="vpn-modal-text">{t("vpn.comingSoon")}</p>
          </div>
        ) : status.isActive ? (
          <div className="vpn-account">
            <div className="vpn-status-badge vpn-status-badge--active">
              <Check size={16} />
              <span>{t("vpn.active")}</span>
            </div>

            <dl className="vpn-details">
              <div className="vpn-detail-row">
                <dt>{t("vpn.plan")}</dt>
                <dd>{status.planName || t("vpn.planDefault")}</dd>
              </div>
              {status.daysLeft !== null && status.daysLeft !== undefined ? (
                <div className="vpn-detail-row">
                  <dt>{t("vpn.daysLeft")}</dt>
                  <dd>{status.daysLeft}</dd>
                </div>
              ) : status.expiresAtUtc ? (
                <div className="vpn-detail-row">
                  <dt>{t("vpn.expires")}</dt>
                  <dd>{new Date(status.expiresAtUtc).toLocaleDateString()}</dd>
                </div>
              ) : null}
            </dl>

            {status.vlessLink ? (
              <div className="vpn-link-block">
                <span className="vpn-link-label">{t("vpn.connectionLink")}</span>
                <div className="vpn-link-row">
                  <input readOnly value={status.vlessLink} aria-label={t("vpn.connectionLink")} />
                  <button
                    type="button"
                    className="icon-btn"
                    onClick={() => handleCopy("vless", status.vlessLink)}
                    aria-label={t("vpn.copy")}
                  >
                    {copied === "vless" ? <Check size={16} /> : <Copy size={16} />}
                  </button>
                </div>
              </div>
            ) : null}

            <div className="vpn-actions">
              {status.clientDownloadUrl ? (
                <a className="secondary-btn vpn-action-btn" href={status.clientDownloadUrl} target="_blank" rel="noreferrer">
                  <Download size={16} />
                  <span>{t("vpn.clients")}</span>
                </a>
              ) : null}
              {status.panelClientUrl ? (
                <a className="secondary-btn vpn-action-btn" href={status.panelClientUrl} target="_blank" rel="noreferrer">
                  <ExternalLink size={16} />
                  <span>{t("vpn.panel")}</span>
                </a>
              ) : null}
              <button type="button" className="secondary-btn vpn-action-btn" onClick={() => window.location.reload()}>
                <Plus size={16} />
                <span>{t("vpn.renew")}</span>
              </button>
            </div>
          </div>
        ) : (
          <div className="vpn-account">
            <p className="vpn-modal-text">{t("vpn.noSubscription")}</p>

            <div className="vpn-actions vpn-actions--stack">
              <button
                type="button"
                className="primary-btn vpn-trial-btn"
                onClick={handleStartTrial}
                disabled={trialBusy || status.isTrialUsed}
              >
                <Gift size={16} />
                <span>{status.isTrialUsed ? t("vpn.trialUsed") : t("vpn.trialStart", { days: status.trialDays ?? 14 })}</span>
              </button>
            </div>

            <form className="vpn-promo-form" onSubmit={handleActivatePromo}>
              <label className="vpn-promo-label">{t("vpn.promoLabel")}</label>
              <div className="vpn-promo-row">
                <input
                  type="text"
                  value={promoCode}
                  onChange={(event) => {
                    setPromoCode(event.target.value);
                    setPromoMessage("");
                    setError("");
                  }}
                  placeholder={t("vpn.promoPlaceholder")}
                  autoCapitalize="chars"
                  autoCorrect="off"
                  spellCheck={false}
                  maxLength={16}
                />
                <button type="submit" className="primary-btn" disabled={promoBusy}>
                  {promoBusy ? "…" : t("vpn.activate")}
                </button>
              </div>
              {promoMessage ? (
                <p className="vpn-promo-message vpn-promo-message--success" role="status">
                  {promoMessage}
                </p>
              ) : null}
            </form>
          </div>
        )}

        {error && (
          <p className="vpn-error-text vpn-error-banner" role="alert">
            {error}
          </p>
        )}

        {status?.referralCode && (
          <div className="vpn-referral">
            <div className="vpn-referral-head">
              <LinkIcon size={16} />
              <span>{t("vpn.referralTitle")}</span>
            </div>
            <p className="vpn-referral-text">{t("vpn.referralText")}</p>
            <div className="vpn-link-row">
              <input readOnly value={referralLink} aria-label={t("vpn.referralTitle")} />
              <button
                type="button"
                className="icon-btn"
                onClick={() => handleCopy("ref", referralLink)}
                aria-label={t("vpn.copy")}
              >
                {copied === "ref" ? <Check size={16} /> : <Copy size={16} />}
              </button>
            </div>
          </div>
        )}
      </div>
    </div>,
    document.body
  );
}
