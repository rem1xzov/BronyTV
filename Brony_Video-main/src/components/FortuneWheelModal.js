import React, { useEffect, useRef, useState } from "react";
import { useI18n } from "../i18n";
import { spinFortuneWheel } from "../streak/api";

const SEGMENTS = [
  { key: "vpn30", label: "30 дней VPN", color: "#7c4dff" },
  { key: "premium1y", label: "1 год премиум", color: "#ff9800" },
  { key: "vpn1y", label: "1 год VPN", color: "#00bcd4" },
  { key: "nft", label: "NFT", color: "#e91e63" }
];

const SEGMENT_ANGLE = 360 / SEGMENTS.length; // 90°
const SPIN_DURATION_MS = 4000;

function buildConicGradient() {
  const stops = [];
  let from = 0;
  SEGMENTS.forEach((segment, index) => {
    const to = from + 100 / SEGMENTS.length;
    stops.push(`${segment.color} ${from}% ${to}%`);
    from = to;
  });
  return `conic-gradient(from 0deg, ${stops.join(", ")})`;
}

export default function FortuneWheelModal({ isOpen, onClose, onResult }) {
  const { t } = useI18n();
  const [spinning, setSpinning] = useState(false);
  const [result, setResult] = useState(null);
  const [error, setError] = useState("");
  const [rotation, setRotation] = useState(0);
  const rotationRef = useRef(0);

  // Сбрасываем состояние при каждом открытии, чтобы не показывать прошлый результат.
  useEffect(() => {
    if (isOpen) {
      setSpinning(false);
      setResult(null);
      setError("");
      rotationRef.current = 0;
      setRotation(0);
    }
  }, [isOpen]);

  if (!isOpen) {
    return null;
  }

  const handleSpin = async () => {
    if (spinning) return;
    setSpinning(true);
    setError("");
    setResult(null);

    const payload = await spinFortuneWheel();
    if (!payload || payload.success === false) {
      setSpinning(false);
      setError(payload?.error || t("streak.wheelError"));
      return;
    }

    const index = Math.max(0, Math.min(SEGMENTS.length - 1, Number(payload.prizeIndex ?? 0)));
    const base = 360 - (index * SEGMENT_ANGLE + SEGMENT_ANGLE / 2);
    const delta = ((base - (rotationRef.current % 360)) % 360 + 360) % 360;
    const next = rotationRef.current + 360 * 6 + delta;
    rotationRef.current = next;
    setRotation(next);

    window.setTimeout(() => {
      setSpinning(false);
      setResult(payload);
      if (onResult) {
        onResult(payload);
      }
    }, SPIN_DURATION_MS);
  };

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div
        className="modal-card fortune-wheel-modal"
        onClick={(event) => event.stopPropagation()}
        role="dialog"
        aria-modal="true"
      >
        <button type="button" className="modal-close" onClick={onClose} aria-label="Закрыть">
          ×
        </button>
        <h2 className="modal-title">{t("streak.wheelTitle")}</h2>
        <p className="muted">{t("streak.wheelSubtitle")}</p>

        <div className="fortune-wheel-wrap">
          <div className="fortune-wheel-pointer" />
          <div
            className="fortune-wheel"
            style={{
              background: buildConicGradient(),
              transform: `rotate(${rotation}deg)`,
              transition: spinning ? `transform ${SPIN_DURATION_MS}ms cubic-bezier(0.16, 1, 0.3, 1)` : "none"
            }}
          >
            {SEGMENTS.map((segment, index) => {
              const angle = index * SEGMENT_ANGLE + SEGMENT_ANGLE / 2;
              return (
                <span
                  key={segment.key}
                  className="fortune-wheel-label"
                  style={{
                    transform: `rotate(${angle}deg) translateY(-72px) rotate(-${angle}deg)`
                  }}
                >
                  {segment.label}
                </span>
              );
            })}
          </div>
        </div>

        {!result && !error ? (
          <button
            type="button"
            className="primary-btn"
            onClick={handleSpin}
            disabled={spinning}
          >
            {spinning ? t("streak.wheelSpinning") : t("streak.wheelSpin")}
          </button>
        ) : null}

        {error ? <p className="error-text">{error}</p> : null}

        {result ? (
          <div className="fortune-wheel-result">
            <p className="fortune-wheel-prize">
              {result.requiresManualAction
                ? t("streak.wheelNftTitle")
                : `${t("streak.wheelYouWon")} ${result.prizeDescription}`}
            </p>
            <p className="muted">{result.message}</p>
            {result.requiresManualAction && result.supportTelegramUrl ? (
              <a
                className="primary-btn"
                href={result.supportTelegramUrl}
                target="_blank"
                rel="noopener noreferrer"
              >
                {t("streak.wheelTelegram")}
              </a>
            ) : null}
          </div>
        ) : null}
      </div>
    </div>
  );
}
