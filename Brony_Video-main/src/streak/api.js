import { apiFetch } from "../auth/api";

export async function getStreakStatus() {
  const response = await apiFetch("/streak/status");
  if (!response.ok) {
    throw new Error("Не удалось загрузить статус стрика.");
  }
  return response.json();
}

export async function recordVideoWatch(seconds) {
  const response = await apiFetch("/streak/video-watch", {
    method: "POST",
    body: JSON.stringify({ seconds })
  });
  return response.json().catch(() => ({}));
}

export async function setStreakFreeze() {
  const response = await apiFetch("/streak/freeze", { method: "POST" });
  return response.json().catch(() => ({}));
}

export async function markStreakRewardsSeen() {
  const response = await apiFetch("/streak/rewards/seen", { method: "POST" });
  return response.ok;
}

export async function getLeaderboard(sort = "current", limit = 50) {
  const response = await apiFetch(
    `/streak/leaderboard?sort=${encodeURIComponent(sort)}&limit=${encodeURIComponent(limit)}`
  );
  if (!response.ok) {
    throw new Error("Не удалось загрузить таблицу лидеров.");
  }
  return response.json();
}

export async function spinFortuneWheel() {
  const response = await apiFetch("/streak/fortune-wheel/spin", { method: "POST" });
  return response.json().catch(() => ({}));
}
