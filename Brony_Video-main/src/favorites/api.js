import { apiFetch } from "../auth/api";

function normalizeFavorite(raw) {
  if (!raw || typeof raw !== "object") {
    return null;
  }

  const id = raw.id ?? raw.Id;
  const videoId = raw.videoId ?? raw.VideoId;
  if (!videoId) {
    return null;
  }

  return {
    id: id ?? null,
    videoId,
    title: raw.title ?? raw.Title ?? "",
    seasonNumber: raw.seasonNumber ?? raw.SeasonNumber ?? null,
    episodeNumber: raw.episodeNumber ?? raw.EpisodeNumber ?? null,
    addedAt: raw.addedAt ?? raw.AddedAt ?? new Date().toISOString()
  };
}

/**
 * Список избранных серий текущего пользователя (от новых к старым).
 */
export async function fetchFavorites() {
  const response = await apiFetch("/favorites");
  if (!response.ok) {
    const payload = await response.json().catch(() => ({}));
    throw new Error(payload.message || "Не удалось загрузить избранное.");
  }
  const payload = await response.json();
  if (Array.isArray(payload)) {
    return payload.map(normalizeFavorite).filter(Boolean);
  }
  return [];
}

/**
 * Отмечено ли конкретное видео как избранное у текущего пользователя.
 */
export async function fetchFavoriteStatus(videoId) {
  if (!videoId) {
    return false;
  }
  const response = await apiFetch(`/favorites/${videoId}/status`);
  if (!response.ok) {
    return false;
  }
  const payload = await response.json().catch(() => ({}));
  return Boolean(payload?.isFavorite ?? payload?.IsFavorite);
}

/**
 * Добавить видео в избранное.
 */
export async function addFavorite(videoId) {
  const response = await apiFetch(`/favorites/${videoId}`, { method: "POST" });
  if (!response.ok) {
    const payload = await response.json().catch(() => ({}));
    throw new Error(payload.message || "Не удалось добавить в избранное.");
  }
  const payload = await response.json().catch(() => ({}));
  return Boolean(payload?.isFavorite ?? payload?.IsFavorite);
}

/**
 * Убрать видео из избранного.
 */
export async function removeFavorite(videoId) {
  const response = await apiFetch(`/favorites/${videoId}`, { method: "DELETE" });
  if (!response.ok) {
    const payload = await response.json().catch(() => ({}));
    throw new Error(payload.message || "Не удалось убрать из избранного.");
  }
  const payload = await response.json().catch(() => ({}));
  return Boolean(payload?.isFavorite ?? payload?.IsFavorite);
}

export function formatFavoriteDate(value) {
  if (!value) {
    return "";
  }
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "";
  }
  return date.toLocaleString("ru-RU", {
    day: "numeric",
    month: "short",
    year: "numeric"
  });
}
