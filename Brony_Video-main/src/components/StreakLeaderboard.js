import React, { useEffect, useState } from "react";
import { useI18n } from "../i18n";
import { getLeaderboard } from "../streak/api";
import StreakFlame from "./StreakFlame";

/**
 * Таблица лидеров стриков (переиспользуемый блок, без обёртки-страницы).
 * Встраивается в личный кабинет.
 */
export default function StreakLeaderboard() {
  const { t } = useI18n();
  const [sort, setSort] = useState("current");
  const [entries, setEntries] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError("");
    getLeaderboard(sort, 50)
      .then((payload) => {
        if (cancelled) return;
        const list = Array.isArray(payload?.entries) ? payload.entries : [];
        setEntries(list);
      })
      .catch((err) => {
        if (cancelled) return;
        setError(err.message || t("streak.leaderboardError"));
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [sort, t]);

  const isLongest = sort === "longest";

  return (
    <div className="streak-leaderboard streak-leaderboard--embed">
      <div className="streak-leaderboard-tabs" role="tablist">
        <button
          type="button"
          className={`primary-btn ${!isLongest ? "active" : ""}`}
          onClick={() => setSort("current")}
        >
          {t("streak.leaderboardCurrent")}
        </button>
        <button
          type="button"
          className={`primary-btn ${isLongest ? "active" : ""}`}
          onClick={() => setSort("longest")}
        >
          {t("streak.leaderboardLongest")}
        </button>
      </div>

      {loading ? (
        <p className="muted">{t("streak.leaderboardLoading")}</p>
      ) : error ? (
        <p className="error-text">{error}</p>
      ) : entries.length === 0 ? (
        <p className="muted">{t("streak.leaderboardEmpty")}</p>
      ) : (
        <ol className="streak-leaderboard-list">
          {entries.map((entry) => (
            <li key={entry.rank} className="streak-leaderboard-item">
              <span className="streak-leaderboard-rank">{entry.rank}</span>
              <span className="streak-leaderboard-user">
                <StreakFlame
                  streak={entry.currentStreak}
                  active={entry.isStreakCreditedToday}
                  size={16}
                />
                <span className="streak-leaderboard-username">{entry.username}</span>
              </span>
              <span className="streak-leaderboard-value">
                {isLongest ? entry.longestStreak : entry.currentStreak}
              </span>
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}
