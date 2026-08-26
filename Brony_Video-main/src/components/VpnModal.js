import React, { useCallback, useEffect, useState } from "react";
import { createPortal } from "react-dom";
import {
  AlertTriangle,
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

/**
 * Полноценный оверлей BronyVPN с затемнением, блюром и карточным дизайном.
 * Все стили — scoped (уникальные классы `.vpn-modal-…`), глобальный styles.css не трогается.
 */
function VpnModalStyles() {
  return (
    <style>{`
      /* ===== Scoped-стили BronyVPN (не конфликтуют с глобальными) ===== */
      .vpn-modal-overlay {
        position: fixed;
        inset: 0;
        z-index: 9999;
        display: flex;
        align-items: center;
        justify-content: center;
        padding: 20px;
        background: rgba(0, 0, 0, 0.65);
        backdrop-filter: blur(6px);
        -webkit-backdrop-filter: blur(6px);
        overflow: auto;
      }
      .vpn-modal {
        position: relative;
        width: min(100%, 440px);
        max-height: min(92vh, 760px);
        overflow: auto;
        background: var(--bg-card, #fff);
        border: 1px solid var(--border-soft, rgba(168, 85, 247, 0.16));
        border-radius: 28px;
        padding: 32px 28px 28px;
        text-align: center;
        box-shadow: 0 30px 90px rgba(58, 11, 60, 0.32);
        -webkit-box-shadow: 0 30px 90px rgba(58, 11, 60, 0.32);
      }
      .vpn-modal--full { width: min(100%, 460px); }
      .vpn-modal-close {
        position: absolute;
        top: 14px;
        right: 14px;
        width: 38px;
        height: 38px;
        display: inline-flex;
        align-items: center;
        justify-content: center;
        border-radius: 50%;
        border: 1px solid var(--border-soft, rgba(168, 85, 247, 0.16));
        background: var(--bg-soft, #faecff);
        color: var(--text-muted, #7b4b82);
        cursor: pointer;
        transition: background 0.2s ease, color 0.2s ease, transform 0.2s ease;
      }
      .vpn-modal-close:hover {
        background: var(--accent-soft, rgba(236, 72, 153, 0.16));
        color: var(--text-main, #3a0b3c);
        transform: rotate(90deg);
      }
      .vpn-modal-icon {
        width: 62px;
        height: 62px;
        margin: 0 auto 14px;
        border-radius: 20px;
        display: inline-flex;
        align-items: center;
        justify-content: center;
        background: linear-gradient(135deg, var(--accent-soft, rgba(236,72,153,.16)), color-mix(in srgb, #a855f7 18%, transparent));
        color: var(--accent-strong, #db2777);
        box-shadow: 0 8px 24px rgba(236, 72, 153, 0.18);
      }
      .vpn-modal h2 {
        margin: 0 0 10px;
        font-size: clamp(1.35rem, 4vw, 1.65rem);
        color: var(--text-main, #3a0b3c);
      }
      .vpn-modal-text {
        margin: 0 0 20px;
        color: var(--text-muted, #7b4b82);
        font-size: 0.95rem;
        line-height: 1.55;
      }
      .vpn-modal-state {
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 8px;
      }
      .vpn-modal-actions { width: 100%; display: flex; justify-content: center; }
      .vpn-modal-close-btn { min-width: 160px; justify-content: center; }
      .vpn-loader {
        width: 40px;
        height: 40px;
        margin: 12px auto;
        border: 3px solid var(--accent-soft, rgba(236,72,153,.16));
        border-top-color: var(--accent, #ec4899);
        border-radius: 50%;
        animation: vpn-modal-spin 0.9s linear infinite;
      }
      @keyframes vpn-modal-spin { to { transform: rotate(360deg); } }
      .vpn-error-text { color: #b91c1c !important; }
      .vpn-error-banner {
        margin-top: 16px;
        padding: 12px 16px;
        border-radius: 14px;
        background: rgba(239, 68, 68, 0.1);
        border: 1px solid rgba(239, 68, 68, 0.24);
        font-size: 0.9rem;
        line-height: 1.45;
        text-align: left;
      }

      /* ===== Карточка активной подписки ===== */
      .vpn-account {
        display: flex;
        flex-direction: column;
        gap: 18px;
        width: 100%;
      }
      .vpn-status-badge {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        gap: 6px;
        align-self: center;
        padding: 6px 18px;
        border-radius: 999px;
        font-weight: 700;
        font-size: 0.9rem;
      }
      .vpn-status-badge--active {
        background: rgba(34, 197, 94, 0.14);
        color: #15803d;
        border: 1px solid rgba(34, 197, 94, 0.32);
      }
      .vpn-details {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 12px;
        margin: 0;
        padding: 16px;
        border-radius: 18px;
        background: var(--bg-soft, #faecff);
        border: 1px solid var(--border-soft, rgba(168,85,247,.16));
      }
      .vpn-detail-row {
        display: flex;
        flex-direction: column;
        gap: 4px;
      }
      .vpn-detail-row dt {
        font-size: 0.78rem;
        font-weight: 600;
        color: var(--text-muted, #7b4b82);
        text-transform: uppercase;
        letter-spacing: 0.04em;
      }
      .vpn-detail-row dd {
        margin: 0;
        font-size: 1.05rem;
        font-weight: 700;
        color: var(--text-main, #3a0b3c);
      }

      /* ===== Блок ссылки VLESS ===== */
      .vpn-link-block {
        display: flex;
        flex-direction: column;
        gap: 8px;
        text-align: left;
      }
      .vpn-link-label {
        font-size: 0.85rem;
        font-weight: 600;
        color: var(--text-muted, #7b4b82);
      }
      .vpn-link-row {
        display: flex;
        align-items: stretch;
        gap: 8px;
        width: 100%;
      }
      .vpn-link-row input {
        flex: 1;
        min-width: 0;
        padding: 12px 14px;
        border-radius: 14px;
        border: 1px solid var(--border-soft, rgba(168,85,247,.16));
        background: var(--bg-soft, #faecff);
        color: var(--text-main, #3a0b3c);
        font-size: 0.85rem;
        font-family: "SFMono-Regular", Consolas, "Liberation Mono", Menlo, monospace;
        color: var(--accent-strong, #db2777);
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }
      .vpn-link-row input:focus { outline: none; border-color: color-mix(in srgb, var(--accent, #ec4899) 55%, var(--border-soft, rgba(168,85,247,.16))); }
      .icon-btn {
        position: relative;
        flex-shrink: 0;
        width: 46px;
        border-radius: 14px;
        border: 1px solid var(--border-soft, rgba(168,85,247,.16));
        background: var(--bg-soft, #faecff);
        color: var(--accent-strong, #db2777);
        display: inline-flex;
        align-items: center;
        justify-content: center;
        cursor: pointer;
        transition: background 0.2s ease, color 0.2s ease, transform 0.2s ease;
      }
      .icon-btn:hover { background: var(--accent, #ec4899); color: #fff; transform: translateY(-1px); }
      .vpn-copy-feedback {
        position: absolute;
        right: 54px;
        top: 50%;
        transform: translateY(-50%);
        white-space: nowrap;
        padding: 5px 10px;
        border-radius: 8px;
        background: rgba(16, 185, 129, 0.95);
        color: #fff;
        font-size: 0.78rem;
        font-weight: 700;
        z-index: 5;
        box-shadow: 0 6px 18px rgba(16,185,129,.35);
        animation: vpn-copied-pop 0.25s ease;
      }
      @keyframes vpn-copied-pop {
        from { opacity: 0; transform: translateY(-50%) scale(0.85); }
        to { opacity: 1; transform: translateY(-50%) scale(1); }
      }

      /* ===== Кнопки действий ===== */
      .vpn-actions {
        display: flex;
        flex-wrap: wrap;
        gap: 10px;
        justify-content: center;
        align-items: stretch;
      }
      .vpn-actions--stack { flex-direction: column; }
      .vpn-action-btn { flex: 1 1 auto; justify-content: center; }
      .vpn-trial-btn { width: 100%; justify-content: center; padding: 13px 18px; }
      .vpn-renew-btn { justify-content: center; padding: 12px 18px; }

      /* ===== Промо-код ===== */
      .vpn-promo-toggle-row { width: 100%; }
      .vpn-promo-box {
        display: flex;
        flex-direction: column;
        gap: 10px;
        padding: 16px;
        border-radius: 16px;
        background: var(--bg-soft, #faecff);
        border: 1px solid var(--border-soft, rgba(168,85,247,.16));
        text-align: left;
      }
      .vpn-promo-label {
        font-size: 0.88rem;
        font-weight: 600;
        color: var(--text-muted, #7b4b82);
      }
      .vpn-promo-row { display: flex; align-items: stretch; gap: 8px; width: 100%; }
      .vpn-promo-row input {
        flex: 1;
        min-width: 0;
        padding: 12px 14px;
        border-radius: 14px;
        border: 1px solid var(--border-soft, rgba(168,85,247,.16));
        background: var(--bg-card, #fff);
        color: var(--text-main, #3a0b3c);
        font-size: 0.95rem;
        letter-spacing: 0.05em;
        text-transform: uppercase;
        box-sizing: border-box;
      }
      .vpn-promo-row input:focus { outline: none; border-color: color-mix(in srgb, var(--accent, #ec4899) 55%, var(--border-soft, rgba(168,85,247,.16))); box-shadow: 0 0 0 4px var(--accent-soft, rgba(236,72,153,.16)); }
      .vpn-promo-apply-btn { flex-shrink: 0; justify-content: center; padding: 12px 18px; }
      .vpn-promo-message { margin: 0; font-size: 0.9rem; font-weight: 600; line-height: 1.4; }
      .vpn-promo-message--success { color: #047857; }
      .vpn-promo-message--error { color: #b91c1c; }

      /* ===== Реферальная ссылка ===== */
      .vpn-referral {
        display: flex;
        flex-direction: column;
        gap: 8px;
        text-align: left;
        margin-top: 8px;
        padding-top: 18px;
        border-top: 1px dashed var(--border-soft, rgba(168,85,247,.16));
      }
      .vpn-referral-head {
        display: inline-flex;
        align-items: center;
        gap: 8px;
        font-weight: 700;
        font-size: 0.95rem;
        color: var(--text-main, #3a0b3c);
      }
      .vpn-referral-text {
        margin: 0;
        font-size: 0.82rem;
        color: var(--text-muted, #7b4b82);
        line-height: 1.4;
      }

      /* ===== Примечание «Важно знать о BronyVPN» ===== */
      .vpn-notice-btn {
        color: #d97706;
        border-color: rgba(217, 119, 6, 0.28);
        background: rgba(251, 191, 36, 0.14);
      }
      .vpn-notice-btn:hover {
        background: #f59e0b;
        color: #fff;
        transform: translateY(-1px);
      }
      .vpn-notice-overlay { z-index: 10000; }
      .vpn-notice-icon {
        background: linear-gradient(135deg, rgba(251, 191, 36, 0.24), rgba(217, 119, 6, 0.14));
        color: #d97706;
        box-shadow: 0 8px 24px rgba(217, 119, 6, 0.22);
      }
      .vpn-notice-text { white-space: pre-line; }
    `}</style>
  );
}

export default function VpnModal({ isOpen, onClose, isAuthenticated, onRequestSignIn }) {
  const { t } = useI18n();
  const [status, setStatus] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [trialBusy, setTrialBusy] = useState(false);
  const [promoCode, setPromoCode] = useState("");
  const [promoBusy, setPromoBusy] = useState(false);
  const [promoMessage, setPromoMessage] = useState("");
  const [promoMessageType, setPromoMessageType] = useState("");
  const [copied, setCopied] = useState("");
  const [showPromoInput, setShowPromoInput] = useState(false);
  const [showNotice, setShowNotice] = useState(false);

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
      setShowPromoInput(false);
      setShowNotice(false);
      setPromoMessage("");
      setPromoCode("");
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
    setPromoMessage("");
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
    if (!promoCode.trim()) {
      setPromoMessage("Введите промо-код.");
      setPromoMessageType("error");
      return;
    }
    setPromoBusy(true);
    setPromoMessage("");
    setError("");
    try {
      const response = await activateVpnPromo(promoCode.trim());
      const raw = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(raw.message || "Не удалось активировать промо-код.");
      }
      setPromoMessage(t("vpn.promoSuccess"));
      setPromoMessageType("success");
      setPromoCode("");
      setShowPromoInput(false);
      await loadStatus();
    } catch (promoError) {
      setPromoMessage(promoError.message || "Не удалось активировать промо-код.");
      setPromoMessageType("error");
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
    <>
      <VpnModalStyles />
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
                      {copied === "vless" ? <span className="vpn-copy-feedback">Скопировано!</span> : null}
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
                <button
                  type="button"
                  className="secondary-btn vpn-renew-btn"
                  onClick={() => setShowPromoInput((prev) => !prev)}
                >
                  <Plus size={16} />
                  <span>{t("vpn.renew")}</span>
                </button>
                <button
                  type="button"
                  className="icon-btn vpn-notice-btn"
                  onClick={() => setShowNotice(true)}
                  aria-label={t("vpn.noticeTitle")}
                  title={t("vpn.noticeTitle")}
                >
                  <AlertTriangle size={16} />
                </button>
              </div>

              {showPromoInput ? (
                <form className="vpn-promo-box" onSubmit={handleActivatePromo}>
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
                      autoComplete="off"
                      autoCorrect="off"
                      spellCheck={false}
                      maxLength={16}
                    />
                    <button type="submit" className="primary-btn vpn-promo-apply-btn" disabled={promoBusy}>
                      {promoBusy ? "…" : t("vpn.activate")}
                    </button>
                  </div>
                  {promoMessage ? (
                    <p className={`vpn-promo-message vpn-promo-message--${promoMessageType}`} role="status">
                      {promoMessage}
                    </p>
                  ) : null}
                </form>
              ) : null}
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
                  <span>
                    {status.isTrialUsed
                      ? t("vpn.trialUsed")
                      : t("vpn.trialStart", { days: status.trialDays ?? 14 })}
                  </span>
                </button>
              </div>

              <form className="vpn-promo-box" onSubmit={handleActivatePromo}>
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
                    autoComplete="off"
                    autoCorrect="off"
                    spellCheck={false}
                    maxLength={16}
                  />
                  <button type="submit" className="primary-btn vpn-promo-apply-btn" disabled={promoBusy}>
                    {promoBusy ? "…" : t("vpn.activate")}
                  </button>
                </div>
                {promoMessage ? (
                  <p className={`vpn-promo-message vpn-promo-message--${promoMessageType}`} role="status">
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
                  {copied === "ref" ? <span className="vpn-copy-feedback">Скопировано!</span> : null}
                </button>
              </div>
            </div>
        )}

        {showNotice ? (
          <div
            className="vpn-modal-overlay vpn-notice-overlay"
            onClick={(event) => {
              event.stopPropagation();
              setShowNotice(false);
            }}
            role="presentation"
          >
            <div
              className="vpn-modal"
              role="dialog"
              aria-modal="true"
              onClick={(event) => event.stopPropagation()}
            >
              <button type="button" className="vpn-modal-close" onClick={() => setShowNotice(false)} aria-label={t("vpn.close")}>
                <X size={20} />
              </button>
              <div className="vpn-modal-icon vpn-notice-icon" aria-hidden="true">
                <AlertTriangle size={30} />
              </div>
              <h2>{t("vpn.noticeTitle")}</h2>
              <p className="vpn-modal-text vpn-notice-text">{t("vpn.noticeText")}</p>
              <div className="vpn-modal-actions">
                <button type="button" className="primary-btn vpn-modal-close-btn" onClick={() => setShowNotice(false)}>
                  {t("vpn.close")}
                </button>
              </div>
            </div>
          </div>
        ) : null}
        </div>
      </div>
    </>,
    document.body
  );
}
