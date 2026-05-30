import React, { useCallback, useEffect, useMemo, useState } from "react";
import { Heart, MessageCircle, Trash2 } from "lucide-react";
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
  const parentCommentId = raw.parentCommentId ?? raw.ParentCommentId ?? null;
  const likeCount = raw.likeCount ?? raw.LikeCount ?? 0;
  const isLikedByCurrentUser = Boolean(
    raw.isLikedByCurrentUser ?? raw.IsLikedByCurrentUser ?? false
  );

  if (!id || !userId) {
    return null;
  }

  return {
    id,
    userId,
    username,
    text,
    createdAt,
    parentCommentId: parentCommentId || null,
    likeCount: Number(likeCount) || 0,
    isLikedByCurrentUser
  };
}

function buildCommentTree(flatComments) {
  const byId = new Map();

  flatComments.forEach((comment) => {
    byId.set(comment.id, { ...comment, replies: [] });
  });

  const roots = [];

  flatComments.forEach((comment) => {
    const node = byId.get(comment.id);
    if (!node) {
      return;
    }

    if (comment.parentCommentId && byId.has(comment.parentCommentId)) {
      byId.get(comment.parentCommentId).replies.push(node);
      return;
    }

    roots.push(node);
  });

  const sortNewestFirst = (a, b) => new Date(b.createdAt) - new Date(a.createdAt);
  const sortOldestFirst = (a, b) => new Date(a.createdAt) - new Date(b.createdAt);

  roots.sort(sortNewestFirst);
  roots.forEach((root) => root.replies.sort(sortOldestFirst));

  return roots;
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

function CommentThread({
  comment,
  depth,
  user,
  canPost,
  replyingToId,
  replyText,
  replySubmitting,
  likingId,
  deletingId,
  onToggleReply,
  onReplyTextChange,
  onSubmitReply,
  onCancelReply,
  onToggleLike,
  onDelete,
  canModerate
}) {
  const isReplyOpen = replyingToId === comment.id;

  return (
    <li
      className={`video-comment-item${depth > 0 ? " video-comment-item--reply" : ""}`}
      style={depth > 0 ? { marginLeft: `${Math.min(depth, 4) * 20}px` } : undefined}
    >
      <div className="video-comment-body">
        <div className="video-comment-main">
          <p className="video-comment-author">@{comment.username || "anonymous"}</p>
          <p className="video-comment-text">{comment.text}</p>
          {comment.createdAt ? (
            <time className="video-comment-date muted" dateTime={comment.createdAt}>
              {formatCommentDate(comment.createdAt)}
            </time>
          ) : null}
        </div>

        <div className="video-comment-actions">
          <button
            type="button"
            className={`video-comment-like${comment.isLikedByCurrentUser ? " is-liked" : ""}`}
            onClick={() => onToggleLike(comment)}
            disabled={!user || likingId === comment.id}
            aria-pressed={comment.isLikedByCurrentUser}
            aria-label={comment.isLikedByCurrentUser ? "Убрать лайк" : "Поставить лайк"}
          >
            <Heart size={16} fill={comment.isLikedByCurrentUser ? "currentColor" : "none"} />
            <span>{comment.likeCount}</span>
          </button>

          {canPost ? (
            <button
              type="button"
              className="video-comment-reply-btn"
              onClick={() => onToggleReply(comment.id)}
            >
              <MessageCircle size={16} />
              <span>Ответить</span>
            </button>
          ) : null}

          {canModerate(comment) ? (
            <button
              type="button"
              className="video-comment-delete"
              onClick={() => onDelete(comment.id)}
              disabled={deletingId === comment.id}
              aria-label="Удалить комментарий"
            >
              <Trash2 size={16} />
              <span>Удалить</span>
            </button>
          ) : null}
        </div>
      </div>

      {isReplyOpen ? (
        <form
          className="video-comment-reply-form"
          onSubmit={(event) => onSubmitReply(event, comment.id)}
        >
          <textarea
            value={replyText}
            onChange={(event) => onReplyTextChange(event.target.value)}
            placeholder="Ваш ответ…"
            rows={2}
            maxLength={500}
            disabled={replySubmitting}
            autoFocus
          />
          <div className="video-comment-reply-actions">
            <button type="submit" className="primary-btn small" disabled={replySubmitting}>
              {replySubmitting ? "Отправка…" : "Отправить ответ"}
            </button>
            <button
              type="button"
              className="secondary-btn small"
              onClick={onCancelReply}
              disabled={replySubmitting}
            >
              Отмена
            </button>
          </div>
        </form>
      ) : null}

      {comment.replies?.length > 0 ? (
        <ul className="video-comments-replies">
          {comment.replies.map((reply) => (
            <CommentThread
              key={reply.id}
              comment={reply}
              depth={depth + 1}
              user={user}
              canPost={canPost}
              replyingToId={replyingToId}
              replyText={replyText}
              replySubmitting={replySubmitting}
              likingId={likingId}
              deletingId={deletingId}
              onToggleReply={onToggleReply}
              onReplyTextChange={onReplyTextChange}
              onSubmitReply={onSubmitReply}
              onCancelReply={onCancelReply}
              onToggleLike={onToggleLike}
              onDelete={onDelete}
              canModerate={canModerate}
            />
          ))}
        </ul>
      ) : null}
    </li>
  );
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
  const [likingId, setLikingId] = useState(null);
  const [replyingToId, setReplyingToId] = useState(null);
  const [replyText, setReplyText] = useState("");
  const [replySubmitting, setReplySubmitting] = useState(false);

  const canPost = Boolean(user?.username);
  const commentTree = useMemo(() => buildCommentTree(comments), [comments]);

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

  const postComment = async (text, parentCommentId = null) => {
    const body = { text };
    if (parentCommentId) {
      body.parentCommentId = parentCommentId;
    }

    const response = await apiFetch(`/videos/${videoId}/comments`, {
      method: "POST",
      body: JSON.stringify(body)
    });
    const raw = await response.json().catch(() => ({}));

    if (!response.ok) {
      throw new Error(raw.message || "Не удалось отправить комментарий.");
    }
  };

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
      await postComment(trimmed);
      setCommentText("");
      await loadComments();
    } catch (error) {
      setSubmitError(error.message || "Не удалось отправить комментарий.");
    } finally {
      setSubmitting(false);
    }
  };

  const handleSubmitReply = async (event, parentCommentId) => {
    event.preventDefault();
    setSubmitError("");

    const trimmed = replyText.trim();
    if (!trimmed) {
      setSubmitError("Введите текст ответа.");
      return;
    }

    setReplySubmitting(true);

    try {
      await postComment(trimmed, parentCommentId);
      setReplyText("");
      setReplyingToId(null);
      await loadComments();
    } catch (error) {
      setSubmitError(error.message || "Не удалось отправить ответ.");
    } finally {
      setReplySubmitting(false);
    }
  };

  const handleToggleLike = async (comment) => {
    if (!user) {
      setSubmitError("Войдите в аккаунт, чтобы ставить лайки.");
      return;
    }

    setLikingId(comment.id);
    setSubmitError("");

    try {
      const response = await apiFetch(`/comments/${comment.id}/like`, { method: "POST" });
      const raw = await response.json().catch(() => ({}));

      if (!response.ok) {
        throw new Error(raw.message || "Не удалось обновить лайк.");
      }

      await loadComments();
    } catch (error) {
      setSubmitError(error.message || "Не удалось обновить лайк.");
    } finally {
      setLikingId(null);
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

      if (replyingToId === commentId) {
        setReplyingToId(null);
        setReplyText("");
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

  const handleToggleReply = (commentId) => {
    setSubmitError("");
    if (replyingToId === commentId) {
      setReplyingToId(null);
      setReplyText("");
      return;
    }

    setReplyingToId(commentId);
    setReplyText("");
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
        canPost ? (
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
      ) : commentTree.length === 0 ? (
        <p className="muted">Пока нет комментариев. Будьте первым!</p>
      ) : (
        <ul className="video-comments-list">
          {commentTree.map((comment) => (
            <CommentThread
              key={comment.id}
              comment={comment}
              depth={0}
              user={user}
              canPost={canPost}
              replyingToId={replyingToId}
              replyText={replyText}
              replySubmitting={replySubmitting}
              likingId={likingId}
              deletingId={deletingId}
              onToggleReply={handleToggleReply}
              onReplyTextChange={setReplyText}
              onSubmitReply={handleSubmitReply}
              onCancelReply={() => {
                setReplyingToId(null);
                setReplyText("");
              }}
              onToggleLike={handleToggleLike}
              onDelete={handleDelete}
              canModerate={canModerate}
            />
          ))}
        </ul>
      )}
    </section>
  );
}
