import React, { useEffect, useId, useState } from "react";
import { createPortal } from "react-dom";
import { useNavigate } from "react-router-dom";
import { Bookmark, Loader, Play, X } from "lucide-react";
import { fetchFavorites, formatFavoriteDate, removeFavorite } from "../favorites/api";

function buildPlayerUrl(favorite) {
  const season = favorite.seasonNumber ?? 1;
  const episode = favorite.episodeNumber ?? 1;
  return `/player/${season}/${episode}`;
}

function formatLabel(favorite) {
  const seasonName =
    favorite.seasonNumber != null ? `Сезон ${favorite.seasonNumber}` : "";
  const episodeName = favorite.episodeNumber != null ? `Серия ${favorite.episodeNumber}` : "";
  if (!seasonName && !episodeName) {
    return "";
  }
  return [seasonName, episodeName].filter(Boolean).join(" · ");
}

export default function FavoritesModal({ isOpen, onClose }) {
  const titleId = useId();
  const navigate = useNavigate();
  const [loading, setLoading] = useState(true);
  const [favorites, setFavorites] = useState([]);
  const [error, setError] = useState("");
  const [removingId, setRemovingId] = useState(null);

  useEffect(() => {
    if (!isOpen) {
      setLoading(false);
      setFavorites([]);
      setError("");
      setRemovingId(null);
      return undefined;
    }

    let cancelled = false;

    const load = async () => {
      setLoading(true);
      setError("");
      try {
        const list = await fetchFavorites();
        if (!cancelled) {
          setFavorites(list);
        }
      } catch (loadError) {
        if (!cancelled) {
          setError(loadError.message || "Не удалось загрузить избранное.");
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    };

    load();

    return () => {
      cancelled = true;
    };
  }, [isOpen]);

  useEffect(() => {
    if (!isOpen) {
      return undefined;
    }
    const handleKeyDown = (event) => {
      if (event.key === "Escape") {
        onClose();
      }
    };
    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [isOpen, onClose]);

  if (!isOpen) {
    return null;
  }

  const handleBackdropClick = (event) => {
    if (event.target === event.currentTarget) {
      onClose();
    }
  };

  const handleOpenFavorite = (favorite) => {
    navigate(buildPlayerUrl(favorite));
    onClose();
  };

  const handleRemove = async (event, favorite) => {
    event.stopPropagation();
    if (removingId) {
      return;
    }
    setRemovingId(favorite.videoId);
    try {
      await removeFavorite(favorite.videoId);
      setFavorites((prev) => prev.filter((item) => item.videoId !== favorite.videoId));
    } catch (removeError) {
      setError(removeError.message || "Не удалось убрать из избранного.");
    } finally {
      setRemovingId(null);
    }
  };

  return createPortal(
    <div className="favorites-modal-overlay" onClick={handleBackdropClick} role="presentation">
      <div
        className="favorites-modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        onClick={(event) => event.stopPropagation()}
      >
        <button type="button" className="favorites-modal-close" onClick={onClose} aria-label="Закрыть">
          <X size={18} />
        </button>

        <header className="favorites-modal-header">
          <div className="favorites-modal-icon" aria-hidden="true">
            <Bookmark size={22} />
          </div>
          <h2 id={titleId}>Избранное</h2>
          <p className="favorites-modal-subtitle">Ваши сохранённые серии</p>
        </header>

        <div className="favorites-modal-body">
          {loading ? (
            <div className="favorites-modal-state">
              <Loader size={20} className="favorites-modal-spinner" aria-hidden="true" />
              <p className="muted">Загрузка…</p>
            </div>
          ) : error ? (
            <div className="favorites-modal-state">
              <p className="favorites-modal-error" role="alert">
                {error}
              </p>
              <button
                type="button"
                className="secondary-btn favorites-modal-retry"
                onClick={() => {
                  setLoading(true);
                  setError("");
                  fetchFavorites()
                    .then((list) => setFavorites(list))
                    .catch((loadError) =>
                      setError(loadError.message || "Не удалось загрузить избранное.")
                    )
                    .finally(() => setLoading(false));
                }}
              >
                Повторить
              </button>
            </div>
          ) : favorites.length === 0 ? (
            <div className="favorites-modal-state">
              <div className="favorites-modal-empty-icon" aria-hidden="true">
                <Bookmark size={22} />
              </div>
              <p className="favorites-modal-empty-title">В избранном пока ничего нет</p>
              <p className="muted favorites-modal-empty-text">
                Добавляйте серии в избранное с плеера — они появятся здесь.
              </p>
            </div>
          ) : (
            <ul className="favorites-list">
              {favorites.map((favorite) => (
                <li key={favorite.videoId}>
                  <div
                    className="favorites-card"
                    role="button"
                    tabIndex={0}
                    onClick={() => handleOpenFavorite(favorite)}
                    onKeyDown={(event) => {
                      if (event.key === "Enter" || event.key === " ") {
                        event.preventDefault();
                        handleOpenFavorite(favorite);
                      }
                    }}
                    aria-label="Открыть серию"
                  >
                    <span className="favorites-card-icon" aria-hidden="true">
                      <Play size={16} />
                    </span>
                    <span className="favorites-card-main">
                      <strong className="favorites-card-title">
                        {favorite.title || formatLabel(favorite) || "Серия"}
                      </strong>
                      {formatLabel(favorite) ? (
                        <span className="favorites-card-meta">{formatLabel(favorite)}</span>
                      ) : null}
                      <span className="favorites-card-date muted">
                        {formatFavoriteDate(favorite.addedAt)}
                      </span>
                    </span>
                    <button
                      type="button"
                      className="favorites-remove-btn"
                      aria-label="Убрать из избранного"
                      disabled={removingId === favorite.videoId}
                      onClick={(event) => handleRemove(event, favorite)}
                    >
                      {removingId === favorite.videoId ? (
                        <Loader size={14} className="favorites-modal-spinner" />
                      ) : (
                        <X size={14} />
                      )}
                    </button>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>
    </div>,
    document.body
  );
}
