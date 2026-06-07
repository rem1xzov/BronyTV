import React, { useCallback, useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ArrowLeft, MessageSquare, Plus } from "lucide-react";
import { useAuth } from "../auth/AuthContext";
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
    postCount: Number(raw.postCount ?? raw.PostCount ?? 0)
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
    authorUsername: raw.authorUsername ?? raw.AuthorUsername ?? ""
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

  return date.toLocaleString("ru-RU", {
    day: "numeric",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit"
  });
}

function CreateThreadModal({ isOpen, onClose, onCreated }) {
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [error, setError] = useState("");
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!isOpen) {
      setTitle("");
      setDescription("");
      setError("");
      setSubmitting(false);
    }
  }, [isOpen]);

  if (!isOpen) {
    return null;
  }

  const handleSubmit = async (event) => {
    event.preventDefault();
    setError("");

    const trimmedTitle = title.trim();
    if (!trimmedTitle) {
      setError("Укажите заголовок темы.");
      return;
    }

    if (trimmedTitle.length > 150) {
      setError("Заголовок не может быть длиннее 150 символов.");
      return;
    }

    setSubmitting(true);
    try {
      const response = await apiFetch("/forum/threads", {
        method: "POST",
        body: JSON.stringify({
          title: trimmedTitle,
          description: description.trim() || null
        })
      });
      const raw = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(raw.message || "Не удалось создать тему.");
      }

      const thread = normalizeThread(raw);
      onCreated(thread);
      onClose();
    } catch (submitError) {
      setError(submitError.message || "Не удалось создать тему.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="forum-modal-overlay" onClick={onClose} role="presentation">
      <div className="forum-modal" onClick={(event) => event.stopPropagation()} role="dialog" aria-modal="true">
        <h2>Создать тему</h2>
        <form className="forum-create-form" onSubmit={handleSubmit}>
          <label className="forum-field">
            <span>Заголовок (до 150 символов)</span>
            <input
              type="text"
              value={title}
              maxLength={150}
              onChange={(event) => setTitle(event.target.value)}
              required
            />
          </label>
          <label className="forum-field">
            <span>Описание (необязательно)</span>
            <textarea
              value={description}
              rows={4}
              maxLength={4000}
              onChange={(event) => setDescription(event.target.value)}
            />
          </label>
          {error ? (
            <p className="forum-message forum-message--error" role="alert">
              {error}
            </p>
          ) : null}
          <div className="forum-form-actions">
            <button type="submit" className="primary-btn" disabled={submitting}>
              {submitting ? "Создание…" : "Опубликовать"}
            </button>
            <button type="button" className="secondary-btn" onClick={onClose} disabled={submitting}>
              Отмена
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
  const [thread, setThread] = useState(null);
  const [posts, setPosts] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [replyText, setReplyText] = useState("");
  const [replyError, setReplyError] = useState("");
  const [replying, setReplying] = useState(false);

  const loadThread = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const [threadsResponse, postsResponse] = await Promise.all([
        apiFetch("/forum/threads"),
        apiFetch(`/forum/threads/${threadId}/posts`)
      ]);

      if (!threadsResponse.ok || !postsResponse.ok) {
        throw new Error("Не удалось загрузить тему.");
      }

      const threadsPayload = await threadsResponse.json();
      const postsPayload = await postsResponse.json();
      const threads = (Array.isArray(threadsPayload) ? threadsPayload : [])
        .map(normalizeThread)
        .filter(Boolean);
      const found = threads.find((item) => String(item.id) === String(threadId)) ?? null;

      if (!found) {
        throw new Error("Тема не найдена.");
      }

      setThread(found);
      setPosts((Array.isArray(postsPayload) ? postsPayload : []).map(normalizePost).filter(Boolean));
    } catch (loadError) {
      setError(loadError.message || "Не удалось загрузить тему.");
    } finally {
      setLoading(false);
    }
  }, [threadId]);

  useEffect(() => {
    loadThread();
  }, [loadThread]);

  const handleReply = async (event) => {
    event.preventDefault();
    setReplyError("");

    const trimmed = replyText.trim();
    if (!trimmed) {
      setReplyError("Введите текст ответа.");
      return;
    }

    setReplying(true);
    try {
      const response = await apiFetch(`/forum/threads/${threadId}/posts`, {
        method: "POST",
        body: JSON.stringify({ content: trimmed })
      });
      const raw = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(raw.message || "Не удалось отправить ответ.");
      }

      setReplyText("");
      await loadThread();
    } catch (submitError) {
      setReplyError(submitError.message || "Не удалось отправить ответ.");
    } finally {
      setReplying(false);
    }
  };

  if (loading) {
    return <p className="muted">Загрузка темы…</p>;
  }

  if (error || !thread) {
    return (
      <div className="forum-error-state">
        <p className="forum-message forum-message--error">{error || "Тема не найдена."}</p>
        <Link className="secondary-btn" to="/forum">
          Назад к форуму
        </Link>
      </div>
    );
  }

  return (
    <section className="forum-thread-view">
      <button type="button" className="secondary-btn forum-back-btn" onClick={() => navigate("/forum")}>
        <ArrowLeft size={16} />
        <span>К списку тем</span>
      </button>

      <article className="forum-thread-hero">
        <h1>{thread.title}</h1>
        {thread.description ? <p className="forum-thread-description">{thread.description}</p> : null}
        <p className="muted forum-thread-meta">
          @{thread.authorUsername || "anonymous"} · {formatDate(thread.createdAt)}
        </p>
      </article>

      <div className="forum-posts">
        <h2>Ответы ({posts.length})</h2>
        {posts.length === 0 ? (
          <p className="muted">Пока нет ответов. Напишите первым!</p>
        ) : (
          <ul className="forum-post-list">
            {posts.map((post) => (
              <li key={post.id} className="forum-post-item">
                <p className="forum-post-author">@{post.authorUsername || "anonymous"}</p>
                <p className="forum-post-content">{post.content}</p>
                <time className="muted forum-post-date" dateTime={post.createdAt}>
                  {formatDate(post.createdAt)}
                </time>
              </li>
            ))}
          </ul>
        )}
      </div>

      {user ? (
        user.username ? (
          <form className="forum-reply-form" onSubmit={handleReply}>
            <label className="forum-field">
              <span>Ваш ответ</span>
              <textarea
                value={replyText}
                onChange={(event) => setReplyText(event.target.value)}
                rows={3}
                maxLength={4000}
                disabled={replying}
              />
            </label>
            {replyError ? (
              <p className="forum-message forum-message--error" role="alert">
                {replyError}
              </p>
            ) : null}
            <button type="submit" className="primary-btn" disabled={replying}>
              {replying ? "Отправка…" : "Отправить ответ"}
            </button>
          </form>
        ) : (
          <p className="muted">Задайте юзернейм в личном кабинете, чтобы отвечать в теме.</p>
        )
      ) : (
        <p className="muted">Войдите в аккаунт, чтобы ответить в теме.</p>
      )}
    </section>
  );
}

export default function ForumPage() {
  const { threadId } = useParams();
  const { user } = useAuth();
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
        throw new Error("Не удалось загрузить темы форума.");
      }
      const payload = await response.json();
      setThreads((Array.isArray(payload) ? payload : []).map(normalizeThread).filter(Boolean));
    } catch (loadError) {
      setError(loadError.message || "Не удалось загрузить темы форума.");
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
            <span>Форум BronyTV</span>
          </h1>
          <p className="muted">Обсуждайте серии, теории и всё о пони.</p>
        </div>
        {user ? (
          <button type="button" className="primary-btn" onClick={() => setCreateOpen(true)}>
            <Plus size={16} />
            <span>Создать тему</span>
          </button>
        ) : (
          <p className="muted forum-login-hint">Войдите, чтобы создавать темы.</p>
        )}
      </header>

      {loading ? (
        <p className="muted">Загрузка тем…</p>
      ) : error ? (
        <p className="forum-message forum-message--error" role="alert">
          {error}
        </p>
      ) : threads.length === 0 ? (
        <p className="muted">Тем пока нет. Создайте первую!</p>
      ) : (
        <ul className="forum-thread-list">
          {threads.map((thread) => (
            <li key={thread.id}>
              <Link className="forum-thread-card" to={`/forum/${thread.id}`}>
                <h2>{thread.title}</h2>
                {thread.description ? <p className="forum-thread-card-desc">{thread.description}</p> : null}
                <p className="muted forum-thread-card-meta">
                  @{thread.authorUsername || "anonymous"} · {formatDate(thread.createdAt)} · ответов:{" "}
                  {thread.postCount}
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
