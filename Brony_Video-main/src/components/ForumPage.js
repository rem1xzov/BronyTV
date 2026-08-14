import React, { useCallback, useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ArrowLeft, MessageSquare, Plus, Heart, Trash2 } from "lucide-react";
import { useAuth } from "../auth/AuthContext";
import { useI18n } from "../i18n";
import { isPlatformAdmin } from "../auth/adminAccess";
import { apiFetch } from "../auth/api";

function normalizeThread(raw) {
  if (!raw || typeof raw !== "object") {
    return null;
  }

  const id = raw.id ?? raw.Id;
  if (!id) {
    return null;
  }

  return {
    id,
    title: raw.title ?? raw.Title ?? "",
    description: raw.description ?? raw.Description ?? "",
    createdAt: raw.createdAt ?? raw.CreatedAt,
    authorUsername: raw.authorUsername ?? raw.AuthorUsername ?? "",
    postCount: Number(raw.postCount ?? raw.PostCount ?? 0),
    images: raw.images ?? raw.Images ?? []
  };
}

function normalizePost(raw) {
  if (!raw || typeof raw !== "object") {
    return null;
  }

  const id = raw.id ?? raw.Id;
  if (!id) {
    return null;
  }

        return {
    id,
    content: raw.content ?? raw.Content ?? "",
    createdAt: raw.createdAt ?? raw.CreatedAt,
    authorUsername: raw.authorUsername ?? raw.AuthorUsername ?? "",
    authorRole: raw.authorRole ?? raw.AuthorRole ?? "user",
    images: raw.images ?? raw.Images ?? [],
    likes: Number(raw.likes ?? raw.Likes ?? 0),
    likedByMe: Boolean(raw.likedByMe ?? raw.LikedByMe ?? false),
    replyToPostId: raw.replyToPostId ?? raw.ReplyToPostId ?? null,
    replyToAuthorUsername: raw.replyToAuthorUsername ?? raw.ReplyToAuthorUsername ?? "",
    replyToContent: raw.replyToContent ?? raw.ReplyToContent ?? ""
  };
}

function formatDate(value) {
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

function buildPostTree(posts) {
  const nodes = {};
  posts.forEach((post) => {
    nodes[post.id] = { ...post, children: [] };
  });

  const roots = [];
  posts.forEach((post) => {
    const node = nodes[post.id];
    const parentId = post.replyToPostId;
    if (parentId && nodes[parentId]) {
      nodes[parentId].children.push(node);
    } else {
      roots.push(node);
    }
  });

  return roots;
}

function fileToBase64(file) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onloadend = () => resolve(reader.result);
    reader.onerror = reject;
    reader.readAsDataURL(file);
  });
}

function CreateThreadModal({ isOpen, onClose, onCreated }) {
  const { t } = useI18n();
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [imageFiles, setImageFiles] = useState([]);
  const [previewUrls, setPreviewUrls] = useState([]);
  const [error, setError] = useState("");
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!isOpen) {
      setTitle("");
      setDescription("");
      setImageFiles([]);
      setPreviewUrls([]);
      setError("");
      setSubmitting(false);
    }
  }, [isOpen]);

  if (!isOpen) {
    return null;
  }

  const handleImageChange = (event) => {
    const files = Array.from(event.target.files ?? []);
    const limited = files.slice(0, 3);
    setImageFiles(limited);

    const previews = [];
    limited.forEach((file) => {
      const reader = new FileReader();
      reader.onloadend = () => {
        previews.push(reader.result);
        setPreviewUrls([...previews]);
      };
      reader.readAsDataURL(file);
    });
  };

  const handleSubmit = async (event) => {
    event.preventDefault();
    setError("");

        const trimmedTitle = title.trim();
    if (!trimmedTitle) {
      setError(t("forum.titleRequired"));
      return;
    }

    if (trimmedTitle.length > 150) {
      setError(t("forum.titleTooLong"));
      return;
    }

    setSubmitting(true);
    try {
      let images = [];
      if (imageFiles.length > 0) {
        images = await Promise.all(imageFiles.map((file) => fileToBase64(file)));
      }

      const response = await apiFetch("/forum/threads", {
        method: "POST",
        body: JSON.stringify({
          title: trimmedTitle,
          description: description.trim() || null,
          images
        })
      });
            const raw = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(raw.message || t("forum.createFailed"));
      }

      const thread = normalizeThread(raw);
      onCreated(thread);
      onClose();
    } catch (submitError) {
      setError(submitError.message || t("forum.createFailed"));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="forum-modal-overlay" onClick={onClose} role="presentation">
            <div className="forum-modal" onClick={(event) => event.stopPropagation()} role="dialog" aria-modal="true">
        <h2>{t("forum.titleCreate")}</h2>
        <form className="forum-create-form" onSubmit={handleSubmit}>
          <label className="forum-field">
            <span>{t("forum.fieldTitle")}</span>
            <input
              type="text"
              value={title}
              maxLength={150}
              onChange={(event) => setTitle(event.target.value)}
              required
            />
          </label>
          <label className="forum-field">
            <span>{t("forum.fieldDescription")}</span>
            <textarea
              value={description}
              rows={4}
              maxLength={4000}
              onChange={(event) => setDescription(event.target.value)}
            />
          </label>
          <label className="forum-field">
            <span>{t("forum.fieldImages")}</span>
            <input type="file" accept="image/*" multiple onChange={handleImageChange} />
            {previewUrls.length > 0 ? (
              <div className="forum-image-preview-row">
                {previewUrls.map((src, idx) => (
                  <img key={idx} src={src} alt={`Preview ${idx + 1}`} className="forum-image-preview" />
                ))}
              </div>
            ) : null}
          </label>
          {error ? (
            <p className="forum-message forum-message--error" role="alert">
              {error}
            </p>
          ) : null}
          <div className="forum-form-actions">
            <button type="submit" className="primary-btn" disabled={submitting}>
              {submitting ? t("forum.creating") : t("forum.publish")}
            </button>
            <button type="button" className="secondary-btn" onClick={onClose} disabled={submitting}>
              {t("forum.cancel")}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

function ForumThreadView({ threadId }) {
  const navigate = useNavigate();
  const { user } = useAuth();
  const { t } = useI18n();
  const [thread, setThread] = useState(null);
  const [posts, setPosts] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [replyText, setReplyText] = useState("");
  const [replyImages, setReplyImages] = useState([]);
  const [replyPreviewUrls, setReplyPreviewUrls] = useState([]);
    const [replyError, setReplyError] = useState("");
  const [replying, setReplying] = useState(false);
  const [replyToPost, setReplyToPost] = useState(null);

  const loadThread = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const [threadsResponse, postsResponse] = await Promise.all([
        apiFetch("/forum/threads"),
        apiFetch(`/forum/threads/${threadId}/posts`)
      ]);

            if (!threadsResponse.ok || !postsResponse.ok) {
        throw new Error(t("forum.loadThreadError"));
      }

      const threadsPayload = await threadsResponse.json();
      const postsPayload = await postsResponse.json();
      const threads = (Array.isArray(threadsPayload) ? threadsPayload : [])
        .map(normalizeThread)
        .filter(Boolean);
      const found = threads.find((item) => String(item.id) === String(threadId)) ?? null;

            if (!found) {
        throw new Error(t("forum.errorTitleNotFound"));
      }

      setThread(found);
      setPosts((Array.isArray(postsPayload) ? postsPayload : []).map(normalizePost).filter(Boolean));
    } catch (loadError) {
      setError(loadError.message || t("forum.loadThreadError"));
    } finally {
      setLoading(false);
    }
  }, [threadId]);

  useEffect(() => {
    loadThread();
  }, [loadThread]);

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

            const response = await apiFetch(`/forum/threads/${threadId}/posts`, {
        method: "POST",
        body: JSON.stringify({ content: trimmed, images, replyToPostId: replyToPost?.id ?? null })
      });
            const raw = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(raw.message || t("forum.replyFailed"));
      }

      setReplyText("");
      setReplyImages([]);
      setReplyPreviewUrls([]);
      setReplyToPost(null);
      await loadThread();
    } catch (submitError) {
      setReplyError(submitError.message || t("forum.replyFailed"));
    } finally {
      setReplying(false);
    }
  };

    const clearReplyTarget = () => {
    setReplyToPost(null);
  };

  const handleReplyToUser = (post) => {
    setReplyToPost(post);
  };

  const handleLikePost = async (postId) => {
    try {
      const response = await apiFetch(`/forum/posts/${postId}/like`, { method: "POST" });
      if (response.ok) {
        const updated = await response.json();
        if (updated && updated.id) {
          setPosts((prev) =>
            prev.map((p) =>
              p.id === postId
                ? {
                    ...p,
                    likes: Number(updated.likes ?? p.likes),
                    likedByMe: Boolean(updated.likedByMe ?? !p.likedByMe)
                  }
                : p
            )
          );
        }
      }
    } catch (likeError) {
      // silently ignore
    }
  };

    const handleDeletePost = async (postId) => {
    if (!window.confirm(t("forum.deletePostConfirm"))) {
      return;
    }

    try {
      const response = await apiFetch(`/forum/posts/${postId}`, { method: "DELETE" });
      if (!response.ok) {
        const raw = await response.json().catch(() => ({}));
        throw new Error(raw.message || "Не удалось удалить пост.");
      }
      setPosts((prev) => prev.filter((p) => p.id !== postId));
    } catch (deleteError) {
      alert(deleteError.message || "Ошибка при удалении поста.");
    }
  };

    const handleDeleteThread = async () => {
    if (!window.confirm(t("forum.deleteThreadConfirm"))) {
      return;
    }

    try {
      const response = await apiFetch(`/forum/threads/${threadId}`, { method: "DELETE" });
      if (!response.ok) {
        const raw = await response.json().catch(() => ({}));
        throw new Error(raw.message || "Не удалось удалить тему.");
      }
      navigate("/forum");
    } catch (deleteError) {
      alert(deleteError.message || "Ошибка при удалении темы.");
    }
  };

        const ForumPostNode = ({ node, depth = 0 }) => {
    const currentUsername = user?.username || user?.userName;
    const currentUserRole = (user?.platformRole || user?.role || "").toLowerCase();
    const isOwner = currentUserRole === "owner" || user?.isOwner;
    const isAdmin = currentUserRole === "admin" || user?.isPlatformAdmin;
    const isAuthor = Boolean(currentUsername && currentUsername === node.authorUsername);
    const postAuthorRole = (node.authorRole || "").toLowerCase();
    const canDelete = isOwner || isAuthor || (isAdmin && postAuthorRole !== "owner");

    return (
      <li
        key={node.id}
        className="forum-post-item"
        style={{ marginLeft: depth > 0 ? Math.min(depth * 18, 90) : 0 }}
      >
                <div className="forum-post-head" style={{ display: 'flex', alignItems: 'baseline', gap: '10px', marginBottom: '8px' }}>
          <span className="forum-post-author" style={{ fontWeight: 'bold', color: '#d81b60', margin: 0, fontSize: '0.95rem' }}>
            @{node.authorUsername || "anonymous"}
          </span>
          <time className="forum-post-date" style={{ fontSize: '0.85rem', color: '#888' }}>
            {node.createdAt ? formatDate(node.createdAt) : ""}
          </time>
        </div>

        <p className="forum-post-content">{node.content}</p>

        {node.images && node.images.length > 0 ? (
          <div className="forum-post-images">
            {node.images.map((src, idx) => (
              <img
                key={idx}
                src={src}
                alt={`Post image ${idx + 1}`}
                className="forum-post-image"
                loading="lazy"
                style={{
                  maxWidth: "100%",
                  height: "auto",
                  maxHeight: "400px",
                  objectFit: "contain",
                  borderRadius: "8px",
                  display: "block",
                  marginTop: "8px"
                }}
              />
            ))}
          </div>
        ) : null}

        <div className="forum-post-actions" style={{ display: 'flex', alignItems: 'center', gap: '8px', marginTop: '12px' }}>
          <button
            type="button"
            className="forum-post-reply-btn primary-btn"
            onClick={() => handleReplyToUser(node)}
            aria-label="Ответить пользователю"
            style={{ display: 'inline-flex', alignItems: 'center', justifyContent: 'center', height: '36px', padding: '0 14px', borderRadius: '18px' }}
          >
            {t("forum.replyTo")}
          </button>
          <button
            type="button"
            className={`forum-post-like-btn primary-btn ${node.likedByMe ? "forum-post-like-btn--active" : ""}`}
            onClick={() => handleLikePost(node.id)}
            aria-label="Лайк"
            style={{ display: 'inline-flex', alignItems: 'center', justifyContent: 'center', height: '36px', padding: '0 14px', borderRadius: '18px' }}
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
              onClick={() => handleDeletePost(node.id)}
              aria-label="Удалить пост"
              style={{ display: 'inline-flex', alignItems: 'center', justifyContent: 'center', height: '36px', padding: '0 14px', borderRadius: '18px' }}
            >
              <Trash2 size={14} />
            </button>
          )}
        </div>

        {node.children.length > 0 ? (
          <ul className="forum-post-children">
            {node.children.map((child) => (
              <ForumPostNode key={child.id} node={child} depth={depth + 1} />
            ))}
          </ul>
        ) : null}
      </li>
    );
  };

    if (loading) {
    return <p className="muted">{t("forum.loadingThread")}</p>;
  }

  if (error || !thread) {
    return (
      <div className="forum-error-state">
        <p className="forum-message forum-message--error">{error || t("forum.errorTitleNotFound")}</p>
        <Link className="secondary-btn" to="/forum">
          {t("forum.backToForum")}
        </Link>
      </div>
    );
  }

  const canDeleteThread = user && (
    user.isOwner ||
    user.username === thread.authorUsername ||
    user.isPlatformAdmin
  );

  return (
    <section className="forum-thread-view">
            <button type="button" className="secondary-btn forum-back-btn" onClick={() => navigate("/forum")}>
        <ArrowLeft size={16} />
        <span>{t("forum.backToList")}</span>
      </button>

      <article className="forum-thread-hero">
        <h1>{thread.title}</h1>
        {thread.description ? <p className="forum-thread-description">{thread.description}</p> : null}
        {thread.images && thread.images.length > 0 ? (
          <div className="forum-thread-images">
            {thread.images.map((src, idx) => (
              <img key={idx} src={src} alt={`Thread image ${idx + 1}`} className="forum-thread-image" loading="lazy" />
            ))}
          </div>
        ) : null}
        <p className="muted forum-thread-meta">
          @{thread.authorUsername || "anonymous"} · {formatDate(thread.createdAt)}
        </p>
        {canDeleteThread && (
          <button
            type="button"
            className="forum-thread-delete-btn primary-btn"
            onClick={handleDeleteThread}
            aria-label="Удалить тему"
            style={{ display: 'inline-flex', alignItems: 'center', justifyContent: 'center', height: '36px', padding: '0 14px', borderRadius: '18px', marginTop: '8px' }}
          >
                        <Trash2 size={14} />
            <span style={{ marginLeft: '6px' }}>{t("forum.deleteThread")}</span>
          </button>
        )}
      </article>

            <div className="forum-posts">
        <h2>{t("forum.answers", { count: posts.length })}</h2>
        {posts.length === 0 ? (
          <p className="muted">{t("forum.emptyPosts")}</p>
                ) : (
          <ul className="forum-post-list">
            {buildPostTree(posts).map((rootNode) => (
              <ForumPostNode key={rootNode.id} node={rootNode} depth={0} />
            ))}
          </ul>
        )}
      </div>

            {user ? (
        user.username ? (
                    <form className="forum-reply-form" onSubmit={handleReply}>
            {replyToPost ? (
              <div className="forum-reply-target">
                <span className="forum-reply-target-label">
                  {t("forum.replyingTo")} @{replyToPost.authorUsername || "anonymous"}
                </span>
                <span className="forum-reply-target-snippet">
                  {replyToPost.content || ""}
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
                <label htmlFor="forum-file-upload" className="primary-btn forum-file-upload-label">
                  {t("forum.chooseFiles")}
                </label>
                <input
                  type="file"
                  id="forum-file-upload"
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
    </section>
  );
}

export default function ForumPage() {
  const { threadId } = useParams();
  const { user } = useAuth();
  const { t } = useI18n();
  const [threads, setThreads] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [createOpen, setCreateOpen] = useState(false);

  const loadThreads = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
            const response = await apiFetch("/forum/threads");
      if (!response.ok) {
        throw new Error(t("forum.loadError"));
      }
      const payload = await response.json();
      setThreads((Array.isArray(payload) ? payload : []).map(normalizeThread).filter(Boolean));
    } catch (loadError) {
      setError(loadError.message || t("forum.loadError"));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (!threadId) {
      loadThreads();
    }
  }, [loadThreads, threadId]);

  if (threadId) {
    return (
      <section className="panel forum-panel">
        <ForumThreadView threadId={threadId} />
      </section>
    );
  }

  return (
    <section className="panel forum-panel">
            <header className="forum-header">
        <div>
          <h1>
            <MessageSquare size={24} aria-hidden="true" />
            <span>{t("forum.title")}</span>
          </h1>
          <p className="muted">{t("forum.subtitle")}</p>
        </div>
        {user ? (
          <button type="button" className="primary-btn" onClick={() => setCreateOpen(true)}>
            <Plus size={16} />
            <span>{t("forum.createThread")}</span>
          </button>
        ) : (
          <p className="muted forum-login-hint">{t("forum.loginHint")}</p>
        )}
      </header>

      {loading ? (
        <p className="muted">{t("forum.loadingThreads")}</p>
      ) : error ? (
        <p className="forum-message forum-message--error" role="alert">
          {error}
        </p>
      ) : threads.length === 0 ? (
        <p className="muted">{t("forum.emptyThreads")}</p>
      ) : (
        <ul className="forum-thread-list">
          {threads.map((thread) => (
            <li key={thread.id}>
              <Link className="forum-thread-card" to={`/forum/${thread.id}`}>
                <h2>{thread.title}</h2>
                {thread.description ? <p className="forum-thread-card-desc">{thread.description}</p> : null}
                                <p className="muted forum-thread-card-meta">
                  @{thread.authorUsername || "anonymous"} · {formatDate(thread.createdAt)} ·{" "}
                  {t("forum.responses", { count: thread.postCount })}
                </p>
              </Link>
            </li>
          ))}
        </ul>
      )}

      <CreateThreadModal
        isOpen={createOpen}
        onClose={() => setCreateOpen(false)}
        onCreated={() => loadThreads()}
      />
    </section>
  );
}
