import React, { useCallback, useEffect, useState } from "react";
import { Heart, Trash2 } from "lucide-react";
import { useAuth } from "../auth/AuthContext";
import { useI18n } from "../i18n";
import StreakFlame from "./StreakFlame";
import {
  buildCommentTree,
  createComment,
  deleteComment,
  fetchComments,
  fileToBase64,
  toggleCommentLike
} from "../comments/api";

function formatCommentDate(value) {
  if (!value) {
    return "";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "";
  }

  const day = date.toLocaleString("ru-RU", { day: "numeric" });
  const month = date.toLocaleString("ru-RU", { month: "short" }).replace(".", "");
  const time = date.toLocaleString("ru-RU", { hour: "2-digit", minute: "2-digit" });
  return `${day} ${month} в ${time}`;
}

/**
 * Универсальный блок комментариев: одинаково работает для тем форума и новостей.
 * Поведение 1 в 1 с форумом (список, форма ответа, лайки, удаление, вложенные
 * ответы, огонёк стрика автора).
 */
export default function CommentsSection({ entityType, entityId, onCountChange }) {
  const { user } = useAuth();
  const { t } = useI18n();
  const [comments, setComments] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [replyText, setReplyText] = useState("");
  const [replyImages, setReplyImages] = useState([]);
  const [replyPreviewUrls, setReplyPreviewUrls] = useState([]);
  const [replyError, setReplyError] = useState("");
  const [replying, setReplying] = useState(false);
  const [replyTo, setReplyTo] = useState(null);

  const loadComments = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const list = await fetchComments(entityType, entityId);
      setComments(list);
      if (onCountChange) {
        onCountChange(list.length);
      }
    } catch (loadError) {
      setError(loadError.message || "Не удалось загрузить комментарии.");
    } finally {
      setLoading(false);
    }
  }, [entityType, entityId, onCountChange]);

  useEffect(() => {
    loadComments();
  }, [loadComments]);

  const handleReplyImageChange = (event) => {
    const files = Array.from(event.target.files ?? []);
    const limited = files.slice(0, 3);
    setReplyImages(limited);

    const previews = [];
    limited.forEach((file) => {
      const reader = new FileReader();
      reader.onloadend = () => {
        previews.push(reader.result);
        setReplyPreviewUrls([...previews]);
      };
      reader.readAsDataURL(file);
    });
  };

  const handleReply = async (event) => {
    event.preventDefault();
    setReplyError("");

    const trimmed = replyText.trim();
    const hasText = trimmed.length > 0;
    const hasImages = replyImages.length > 0;

    if (!hasText && !hasImages) {
      setReplyError(t("forum.replyEmpty"));
      return;
    }

    setReplying(true);
    try {
      let images = [];
      if (replyImages.length > 0) {
        images = await Promise.all(replyImages.map((file) => fileToBase64(file)));
      }

      const response = await createComment(entityType, entityId, {
        content: trimmed,
        images,
        replyToId: replyTo?.id ?? null
      });
      const raw = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(raw.message || t("forum.replyFailed"));
      }

      setReplyText("");
      setReplyImages([]);
      setReplyPreviewUrls([]);
      setReplyTo(null);
      await loadComments();
    } catch (submitError) {
      setReplyError(submitError.message || t("forum.replyFailed"));
    } finally {
      setReplying(false);
    }
  };

  const clearReplyTarget = () => {
    setReplyTo(null);
  };

  const handleReplyToUser = (comment) => {
    setReplyTo(comment);
  };

  const handleLikeComment = async (commentId) => {
    try {
      const response = await toggleCommentLike(entityType, commentId);
      if (response.ok) {
        const updated = await response.json();
        if (updated && updated.id) {
          setComments((prev) =>
            prev.map((comment) =>
              comment.id === commentId
                ? {
                    ...comment,
                    likes: Number(updated.likes ?? comment.likes),
                    likedByMe: Boolean(updated.likedByMe ?? !comment.likedByMe)
                  }
                : comment
            )
          );
        }
      }
    } catch (likeError) {
      // silently ignore
    }
  };

  const handleDeleteComment = async (commentId) => {
    if (!window.confirm(t("comments.deleteConfirm"))) {
      return;
    }

    try {
      const response = await deleteComment(entityType, commentId);
      if (!response.ok) {
        const raw = await response.json().catch(() => ({}));
        throw new Error(raw.message || "Не удалось удалить комментарий.");
      }
      setComments((prev) => prev.filter((comment) => comment.id !== commentId));
      if (onCountChange) {
        onCountChange(comments.length - 1);
      }
    } catch (deleteError) {
      alert(deleteError.message || "Ошибка при удалении комментария.");
    }
  };

  const CommentNode = ({ node, depth = 0 }) => {
    const currentUsername = user?.username || user?.userName;
    const currentUserRole = (user?.platformRole || user?.role || "").toLowerCase();
    const isOwner = currentUserRole === "owner" || user?.isOwner;
    const isAdmin = currentUserRole === "admin" || user?.isPlatformAdmin;
    const isAuthor = Boolean(currentUsername && currentUsername === node.authorUsername);
    const commentAuthorRole = (node.authorRole || "").toLowerCase();
    const canDelete = isOwner || isAuthor || (isAdmin && commentAuthorRole !== "owner");

    return (
      <li
        key={node.id}
        className="forum-post-item"
        style={{ marginLeft: depth > 0 ? Math.min(depth * 18, 90) : 0 }}
      >
        <div className="forum-post-head" style={{ display: "flex", alignItems: "baseline", gap: "10px", marginBottom: "8px" }}>
          <span className="forum-post-author" style={{ fontWeight: "bold", color: "#d81b60", margin: 0, fontSize: "0.95rem", display: "inline-flex", alignItems: "center", gap: "6px" }}>
            @{node.authorUsername || "anonymous"}
            <StreakFlame streak={node.authorStreak} active={node.authorStreakActive} size={14} />
          </span>
          <time className="forum-post-date" style={{ fontSize: "0.85rem", color: "#888" }}>
            {node.createdAt ? formatCommentDate(node.createdAt) : ""}
          </time>
        </div>

        <p className="forum-post-content">{node.content}</p>

        {node.images && node.images.length > 0 ? (
          <div className="forum-post-images">
            {node.images.map((src, idx) => (
              <img
                key={idx}
                src={src}
                alt={`Comment image ${idx + 1}`}
                className="forum-post-image"
                loading="lazy"
              />
            ))}
          </div>
        ) : null}

        <div className="forum-post-actions" style={{ display: "flex", alignItems: "center", gap: "8px", marginTop: "12px" }}>
          <button
            type="button"
            className="forum-post-reply-btn primary-btn"
            onClick={() => handleReplyToUser(node)}
            aria-label="Ответить пользователю"
            style={{ display: "inline-flex", alignItems: "center", justifyContent: "center", height: "36px", padding: "0 14px", borderRadius: "18px" }}
          >
            {t("forum.replyTo")}
          </button>
          <button
            type="button"
            className={`forum-post-like-btn primary-btn ${node.likedByMe ? "forum-post-like-btn--active" : ""}`}
            onClick={() => handleLikeComment(node.id)}
            aria-label="Лайк"
            style={{ display: "inline-flex", alignItems: "center", justifyContent: "center", height: "36px", padding: "0 14px", borderRadius: "18px" }}
          >
            <Heart
              size={14}
              fill={node.likedByMe ? "#00BFFF" : "none"}
              stroke={node.likedByMe ? "#00BFFF" : "currentColor"}
            />
            <span>{node.likes}</span>
          </button>
          {canDelete && (
            <button
              type="button"
              className="forum-post-delete-btn primary-btn"
              onClick={() => handleDeleteComment(node.id)}
              aria-label="Удалить пост"
              style={{ display: "inline-flex", alignItems: "center", justifyContent: "center", height: "36px", padding: "0 14px", borderRadius: "18px" }}
            >
              <Trash2 size={14} />
            </button>
          )}
        </div>

        {node.children.length > 0 ? (
          <ul className="forum-post-children">
            {node.children.map((child) => (
              <CommentNode key={child.id} node={child} depth={depth + 1} />
            ))}
          </ul>
        ) : null}
      </li>
    );
  };

  if (loading) {
    return <p className="muted">{t("comments.loading")}</p>;
  }

  if (error) {
    return <p className="forum-message forum-message--error" role="alert">{error}</p>;
  }

  return (
    <div className="comments-section">
      {comments.length === 0 ? (
        <p className="muted">{t("comments.empty")}</p>
      ) : (
        <ul className="forum-post-list">
          {buildCommentTree(comments).map((rootNode) => (
            <CommentNode key={rootNode.id} node={rootNode} depth={0} />
          ))}
        </ul>
      )}

      {user ? (
        user.username ? (
          <form className="forum-reply-form" onSubmit={handleReply}>
            {replyTo ? (
              <div className="forum-reply-target">
                <span className="forum-reply-target-label">
                  {t("forum.replyingTo")} @{replyTo.authorUsername || "anonymous"}
                </span>
                <span className="forum-reply-target-snippet">
                  {replyTo.content || ""}
                </span>
                <button
                  type="button"
                  className="forum-reply-target-cancel"
                  onClick={clearReplyTarget}
                  aria-label={t("forum.cancelReply")}
                >
                  ✕
                </button>
              </div>
            ) : null}
            <label className="forum-field">
              <span>{t("forum.replyLabel")}</span>
              <textarea
                value={replyText}
                onChange={(event) => setReplyText(event.target.value)}
                rows={3}
                maxLength={4000}
                disabled={replying}
              />
            </label>
            <label className="forum-field">
              <span>{t("forum.fieldImages")}</span>
              <div className="forum-file-upload-wrapper">
                <label htmlFor={`comment-file-upload-${entityType}-${entityId}`} className="primary-btn forum-file-upload-label">
                  {t("forum.chooseFiles")}
                </label>
                <input
                  type="file"
                  id={`comment-file-upload-${entityType}-${entityId}`}
                  accept="image/*"
                  multiple
                  onChange={handleReplyImageChange}
                  className="forum-file-input-hidden"
                  style={{ display: "none" }}
                />
              </div>
              {replyPreviewUrls.length > 0 ? (
                <div className="forum-image-preview-row">
                  {replyPreviewUrls.map((src, idx) => (
                    <img key={idx} src={src} alt={`Reply preview ${idx + 1}`} className="forum-image-preview" />
                  ))}
                </div>
              ) : null}
            </label>
            {replyError ? (
              <p className="forum-message forum-message--error" role="alert">
                {replyError}
              </p>
            ) : null}
            <button type="submit" className="primary-btn" disabled={replying}>
              {replying ? t("forum.sending") : t("forum.sendReply")}
            </button>
          </form>
        ) : (
          <p className="muted">{t("forum.noUsername")}</p>
        )
      ) : (
        <p className="muted">{t("forum.loginToReply")}</p>
      )}
    </div>
  );
}
