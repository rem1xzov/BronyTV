import React, { useCallback, useEffect, useState } from "react";
import { createPortal } from "react-dom";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ArrowLeft, MessageSquare, Plus, Trash2 } from "lucide-react";
import { useAuth } from "../auth/AuthContext";
import { useI18n } from "../i18n";
import { isPlatformAdmin } from "../auth/adminAccess";
import { apiFetch } from "../auth/api";
import { fileToBase64 } from "../comments/api";
import CommentsSection from "./CommentsSection";

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

  return createPortal(
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
    </div>,
    document.body
  );
}

function ForumThreadView({ threadId }) {
  const navigate = useNavigate();
  const { user } = useAuth();
  const { t } = useI18n();
  const [thread, setThread] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [commentCount, setCommentCount] = useState(0);

  const loadThread = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const response = await apiFetch("/forum/threads");
      if (!response.ok) {
        throw new Error(t("forum.loadThreadError"));
      }

      const payload = await response.json();
      const threads = (Array.isArray(payload) ? payload : [])
        .map(normalizeThread)
        .filter(Boolean);
      const found = threads.find((item) => String(item.id) === String(threadId)) ?? null;

      if (!found) {
        throw new Error(t("forum.errorTitleNotFound"));
      }

      setThread(found);
    } catch (loadError) {
      setError(loadError.message || t("forum.loadThreadError"));
    } finally {
      setLoading(false);
    }
  }, [threadId]);

  useEffect(() => {
    loadThread();
  }, [loadThread]);

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
        <h2>{t("forum.answers", { count: commentCount })}</h2>
        <CommentsSection entityType="forum" entityId={threadId} onCountChange={setCommentCount} />
      </div>
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
