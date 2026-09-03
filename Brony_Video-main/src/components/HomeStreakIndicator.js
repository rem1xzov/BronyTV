import React, { useCallback, useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useI18n } from "../i18n";
import { useAuth } from "../auth/AuthContext";
import StreakFlame from "./StreakFlame";
import FortuneWheelModal from "./FortuneWheelModal";
import { getStreakStatus, markStreakRewardsSeen } from "../streak/api";

/**
 * Огонёк стрика на главной странице (рядом с переключателем языка).
 * - Серый «0», если сегодняшний порог не выполнен или стрика нет.
 * - Оранжевый с числом CurrentStreak, если сегодняшний день засчитан.
 * По клику переходит на страницу роадмапа наград (/streak). Также показывает
 * одноразовую модалку поздравления/колеса, если есть непоказанная награда.
 */
export default function HomeStreakIndicator() {
  const { t } = useI18n();
  const { isAuthenticated } = useAuth();
  const navigate = useNavigate();
  const [status, setStatus] = useState(null);
  const [wheelOpen, setWheelOpen] = useState(false);
  const [congrats, setCongrats] = useState(null);

  const loadStatus = useCallback(async () => {
    if (!isAuthenticated) {
      setStatus(null);
      return;
    }
    try {
      const payload = await getStreakStatus();
      setStatus(payload);
      if (payload?.pendingReward) {
        if (payload.pendingReward.isWheel) {
          setWheelOpen(true);
        } else {
          setCongrats({
            milestone: payload.pendingReward.milestone,
            description: payload.pendingReward.rewardDescription
          });
        }
        // Плашка показана один раз — помечаем как показанную.
        markStreakRewardsSeen().catch(() => {});
      }
    } catch {
      setStatus(null);
    }
  }, [isAuthenticated]);

  useEffect(() => {
    loadStatus();
  }, [loadStatus]);

  const refreshAfterWheel = useCallback(() => {
    loadStatus();
  }, [loadStatus]);

  return (
    <>
      <button
        type="button"
        className="home-streak-btn"
        onClick={() => navigate("/streak")}
        title={t("streak.roadmapTitle")}
        aria-label={t("streak.roadmapTitle")}
      >
        <StreakFlame
          streak={status?.currentStreak || 0}
          active={Boolean(status?.isStreakCreditedToday)}
          size={28}
          showZero
        />
      </button>

      <FortuneWheelModal
        isOpen={wheelOpen}
        onClose={() => setWheelOpen(false)}
        onResult={refreshAfterWheel}
      />

      {congrats ? (
        <div className="modal-backdrop" onClick={() => setCongrats(null)}>
          <div
            className="modal-card streak-reward-modal"
            onClick={(event) => event.stopPropagation()}
            role="dialog"
            aria-modal="true"
          >
            <button
              type="button"
              className="modal-close"
              onClick={() => setCongrats(null)}
              aria-label="Закрыть"
            >
              ×
            </button>
            <h2 className="modal-title">{t("streak.rewardModalTitle")}</h2>
            <p className="streak-reward-milestone">
              {t("streak.rewardClaimed", { n: congrats.milestone })}
            </p>
            {congrats.description ? (
              <p className="streak-reward-description">
                {t("streak.rewardDescription", { desc: congrats.description })}
              </p>
            ) : null}
            <button type="button" className="primary-btn" onClick={() => setCongrats(null)}>
              Отлично
            </button>
          </div>
        </div>
      ) : null}
    </>
  );
}
