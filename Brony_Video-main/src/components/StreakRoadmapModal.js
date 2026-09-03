import React from "react";
import { useI18n } from "../i18n";

const FALLBACK_MILESTONES = [
  { milestone: 3, reward: "Бейдж на профиле + 1 день VPN", isWheel: false },
  { milestone: 7, reward: "5 дней VPN + 7 дней премиум", isWheel: false },
  { milestone: 14, reward: "7 дней VPN + 10 дней премиум", isWheel: false },
  { milestone: 30, reward: "14 дней VPN + 14 дней премиум", isWheel: false },
  { milestone: 50, reward: "Колесо фортуны", isWheel: true },
  { milestone: 100, reward: "Колесо фортуны", isWheel: true }
];

export default function StreakRoadmapModal({ isOpen, onClose, status, onSpinWheel }) {
  const { t } = useI18n();
  if (!isOpen) return null;

  const milestones =
    Array.isArray(status?.milestones) && status.milestones.length > 0
      ? status.milestones.map((item) => ({
          milestone: Number(item.milestone),
          reward: item.rewardDescription || "",
          isWheel: Boolean(item.isWheel),
          state: item.state || "locked"
        }))
      : FALLBACK_MILESTONES.map((item) => ({ ...item, state: "locked" }));

  const hasNext = typeof status?.nextMilestone === "number";
  const nextMilestone = hasNext ? status.nextMilestone : null;
  const daysToNext = status?.daysToNextMilestone ?? 0;
  const currentStreak = status?.currentStreak ?? 0;
  const nextReward = hasNext
    ? status.nextMilestoneRewardDescription ||
      milestones.find((m) => m.milestone === nextMilestone)?.reward ||
      ""
    : "";

  const thresholdMinutes = status?.thresholdMinutes ?? 10;
  const minWords = status?.minCommentWordCount ?? 5;
  const maxComments = status?.maxQualifyingCommentsPerDay ?? 3;
  const freezesAvailable = status?.freezesAvailable ?? 3;

  const progressPercent = hasNext
    ? Math.min(100, Math.round((currentStreak / nextMilestone) * 100))
    : 100;

  const hasUnspunWheel = (status?.rewards || []).some(
    (reward) =>
      (Number(reward.milestone) === 50 || Number(reward.milestone) === 100) &&
      reward.rewardDescription === "Колесо фортуны"
  );

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div
        className="modal-card streak-roadmap-modal"
        onClick={(event) => event.stopPropagation()}
        role="dialog"
        aria-modal="true"
      >
        <button type="button" className="modal-close" onClick={onClose} aria-label="Закрыть">
          ×
        </button>
        <h2 className="modal-title">{t("streak.roadmapTitle")}</h2>

        {/* Верхний блок — ближайшая недостигнутая веха */}
        <div className="streak-roadmap-next">
          {hasNext ? (
            <>
              <div className="streak-roadmap-next-head">
                <span className="streak-roadmap-next-day">{nextMilestone} дней</span>
                <span className="streak-roadmap-next-reward">{nextReward}</span>
              </div>
              <p className="muted" style={{ margin: "6px 0" }}>
                {t("streak.daysLeft", { n: daysToNext })}
              </p>
              <div className="streak-profile-progress">
                <div
                  className="streak-profile-progress-fill"
                  style={{ width: `${progressPercent}%` }}
                />
              </div>
            </>
          ) : (
            <p style={{ margin: 0, fontWeight: 600 }}>{t("streak.noNextReward")}</p>
          )}
        </div>

        {/* Список всех вех */}
        <ul className="streak-roadmap-list">
          {milestones.map((item) => (
            <li
              key={item.milestone}
              className={`streak-roadmap-item streak-roadmap-item--${item.state}`}
            >
              <span className="streak-roadmap-item-day">{item.milestone} дн.</span>
              <span className="streak-roadmap-item-reward">
                {item.isWheel ? "Колесо фортуны" : item.reward}
              </span>
              <span className="streak-roadmap-item-mark">
                {item.state === "achieved" ? "✓" : item.state === "next" ? "→" : ""}
              </span>
            </li>
          ))}
        </ul>

        {/* Условия начисления */}
        <div className="streak-roadmap-conditions">
          <strong style={{ fontSize: "0.9rem" }}>{t("streak.conditionsTitle")}</strong>
          <p className="muted" style={{ margin: "6px 0 0" }}>
            {t("streak.conditionsText", { minutes: thresholdMinutes, maxComments, minWords })}
          </p>
          <p className="muted" style={{ margin: "6px 0 0" }}>
            {t("streak.freezeInfo", { n: freezesAvailable })}
          </p>
        </div>

        {hasUnspunWheel ? (
          <button
            type="button"
            className="primary-btn"
            style={{ marginTop: "16px" }}
            onClick={() => {
              onClose();
              if (onSpinWheel) onSpinWheel();
            }}
          >
            {t("streak.claimPrize")}
          </button>
        ) : null}
      </div>
    </div>
  );
}
