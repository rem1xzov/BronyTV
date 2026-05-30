import React, { useCallback, useEffect, useState } from "react";
import { Trash2 } from "lucide-react";
import { useAuth } from "../auth/AuthContext";
import { isPlatformAdmin } from "../auth/adminAccess";
import { apiFetch } from "../auth/api";

function normalizeComment(raw) {
  if (!raw || typeof raw !== "object") {
    return null;
  }

  const id = raw.id ?? raw.Id;
  const userId = raw.userId ?? raw.UserId;
  const username = raw.username ?? raw.Username ?? "";
  const text = raw.text ?? raw.Text ?? "";
  const createdAt = raw.createdAt ?? raw.CreatedAt;

  if (!id || !userId) {
    return null;
  }

  return { id, userId, username, text, createdAt };
}

function formatCommentDate(value) {
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
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit"
  });
}

export default function VideoCommentsSection({ videoId, onRequestSetUsername }) {
  const { user, loading: authLoading } = useAuth();
  const [comments, setComments] = useState([]);
  const [commentsLoading, setCommentsLoading] = useState(false);
  const [commentsError, setCommentsError] = useState("");
  const [commentText, setCommentText] = useState("");
  const [submitError, setSubmitError] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [deletingId, setDeletingId] = useState(null);

  const loadComments = useCallback(async () => {
    if (!videoId) {
      setComments([]);
      return;
    }

    setCommentsLoading(true);
    setCommentsError("");

    try {
      const response = await apiFetch(`/videos/${videoId}/comments`);
      if (!response.ok) {
        throw new Error("Не удалось загрузить комментарии.");
      }

      const payload = await response.json();
      const list = Array.isArray(payload) ? payload : [];
      setComments(list.map(normalizeComment).filter(Boolean));
    } catch (error) {
      setCommentsError(error.message || "Не удалось загрузить комментарии.");
      setComments([]);
    } finally {
      setCommentsLoading(false);
    }
  }, [videoId]);

  useEffect(() => {
    loadComments();
  }, [loadComments]);

  const handleSubmit = async (event) => {
    event.preventDefault();
    setSubmitError("");

    const trimmed = commentText.trim();
    if (!trimmed) {
      setSubmitError("Введите текст комментария.");
      return;
    }

    if (trimmed.length > 500) {
      setSubmitError("Комментарий не может быть длиннее 500 символов.");
      return;
    }

    if (!videoId) {
      setSubmitError("Комментарии для этой серии недоступны.");
      return;
    }

    setSubmitting(true);

    try {
      const response = await apiFetch(`/videos/${videoId}/comments`, {
        method: "POST",
        body: JSON.stringify({ text: trimmed })
      });
      const raw = await response.json().catch(() => ({}));

      if (!response.ok) {
        throw new Error(raw.message || "Не удалось отправить комментарий.");
      }

      setCommentText("");
      await loadComments();
    } catch (error) {
      setSubmitError(error.message || "Не удалось отправить комментарий.");
    } finally {
      setSubmitting(false);
    }
  };

  const handleDelete = async (commentId) => {
    setDeletingId(commentId);
    setSubmitError("");

    try {
      const response = await apiFetch(`/comments/${commentId}`, { method: "DELETE" });
      if (!response.ok) {
        const raw = await response.json().catch(() => ({}));
        throw new Error(raw.message || "Не удалось удалить комментарий.");
      }

      await loadComments();
    } catch (error) {
      setSubmitError(error.message || "Не удалось удалить комментарий.");
    } finally {
      setDeletingId(null);
    }
  };

  const canModerate = (comment) => {
    if (!user) {
      return false;
    }

    const currentUserId = user.id ?? user.Id;
    if (currentUserId && String(comment.userId) === String(currentUserId)) {
      return true;
    }

    return isPlatformAdmin(user);
  };

  if (!videoId) {
    return (
      <section className="video-comments">
        <h3>Комментарии</h3>
        <p className="muted">Комментарии доступны только для серий из каталога на сервере.</p>
      </section>
    );
  }

  return (
    <section className="video-comments">
      <h3>Комментарии</h3>

      {authLoading ? (
        <p className="muted">Проверка входа…</p>
      ) : user ? (
        user.username ? (
          <form className="video-comments-form" onSubmit={handleSubmit}>
            <label className="video-comments-field">
              <span className="sr-only">Ваш комментарий</span>
              <textarea
                value={commentText}
                onChange={(event) => {
                  setCommentText(event.target.value);
                  setSubmitError("");
                }}
                placeholder="Напишите комментарий…"
                rows={3}
                maxLength={500}
                disabled={submitting}
              />
            </label>
            <div className="video-comments-form-actions">
              <button type="submit" className="primary-btn" disabled={submitting}>
                {submitting ? "Отправка…" : "Отправить"}
              </button>
              <span className="video-comments-counter muted">{commentText.length}/500</span>
            </div>
          </form>
        ) : (
          <div className="video-comments-prompt">
            <p className="muted">Чтобы оставить комментарий, сначала задайте юзернейм в личном кабинете.</p>
            <button
              type="button"
              className="secondary-btn"
              onClick={() => {
                if (onRequestSetUsername) {
                  onRequestSetUsername();
                  return;
                }
                window.dispatchEvent(new CustomEvent("bronytv:open-profile"));
              }}
            >
              Задать юзернейм
            </button>
          </div>
        )
      ) : (
        <p className="muted">Войдите в аккаунт, чтобы оставить комментарий.</p>
      )}

      {submitError ? (
        <p className="video-comments-message video-comments-message--error" role="alert">
          {submitError}
        </p>
      ) : null}

      {commentsLoading ? (
        <p className="muted">Загрузка комментариев…</p>
      ) : commentsError ? (
        <p className="video-comments-message video-comments-message--error" role="alert">
          {commentsError}
        </p>
      ) : comments.length === 0 ? (
        <p className="muted">Пока нет комментариев. Будьте первым!</p>
      ) : (
        <ul className="video-comments-list">
          {comments.map((comment) => (
            <li key={comment.id} className="video-comment-item">
              <div className="video-comment-main">
                <p className="video-comment-author">@{comment.username || "anonymous"}</p>
                <p className="video-comment-text">{comment.text}</p>
                {comment.createdAt ? (
                  <time className="video-comment-date muted" dateTime={comment.createdAt}>
                    {formatCommentDate(comment.createdAt)}
                  </time>
                ) : null}
              </div>
              {canModerate(comment) ? (
                <button
                  type="button"
                  className="video-comment-delete"
                  onClick={() => handleDelete(comment.id)}
                  disabled={deletingId === comment.id}
                  aria-label="Удалить комментарий"
                >
                  <Trash2 size={16} />
                  <span>Удалить</span>
                </button>
              ) : null}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
