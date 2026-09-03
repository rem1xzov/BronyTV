import React from "react";
import { Flame } from "lucide-react";

/**
 * Огонёк стрика рядом с никнеймом.
 * - Оранжевый (горящий), если сегодняшний день уже засчитан (active).
 * - Серый (погашенный), если стрик есть, но сегодня ещё не выполнено условие.
 */
export default function StreakFlame({ streak = 0, active = false, size = 14, showCount = true, showZero = false }) {
  const displayStreak = streak || 0;
  if (!displayStreak && !showZero) {
    return null;
  }

  const color = active ? "#ff8c00" : "#9e9e9e";
  const title = active
    ? "Стрик активен сегодня"
    : "Стрик ждёт сегодняшней активности";

  return (
    <span
      className="streak-flame"
      title={title}
      aria-label={title}
      style={{
        display: "inline-flex",
        alignItems: "center",
        gap: "2px",
        color,
        fontSize: "0.85em",
        fontWeight: 600,
        lineHeight: 1,
        verticalAlign: "middle"
      }}
    >
      <Flame size={size} fill={active ? color : "none"} stroke={color} />
      {showCount ? <span>{displayStreak}</span> : null}
    </span>
  );
}
