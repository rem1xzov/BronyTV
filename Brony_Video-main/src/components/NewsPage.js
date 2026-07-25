import React, { useCallback, useEffect, useState } from "react";
import { Newspaper, Plus, Trash2, Image as ImageIcon } from "lucide-react";
import { useAuth } from "../auth/AuthContext";
import { isPlatformAdmin } from "../auth/adminAccess";
import { apiFetch } from "../auth/api";

function normalizeNewsPost(raw) {
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
    content: raw.content ?? raw.Content ?? "",
    imageUrl: raw.imageUrl ?? raw.ImageUrl ?? raw.image_url ?? null,
    authorUsername: raw.authorUsername ?? raw.AuthorUsername ?? "",
    createdAt: raw.createdAt ?? raw.CreatedAt
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

function CreateNewsModal({ isOpen, onClose, onCreated }) {
  const [title, setTitle] = useState("");
  const [content, setContent] = useState("");
  const [imageUrl, setImageUrl] = useState("");
  const [imageFile, setImageFile] = useState(null);
  const [previewUrl, setPreviewUrl] = useState("");
  const [error, setError] = useState("");
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!isOpen) {
      setTitle("");
      setContent("");
      setImageUrl("");
      setImageFile(null);
      setPreviewUrl("");
      setError("");
      setSubmitting(false);
    }
  }, [isOpen]);

  if (!isOpen) {
    return null;
  }

  const handleImageChange = (event) => {
    const file = event.target.files?.[0];
    if (!file) {
      return;
    }

    setImageFile(file);
    const reader = new FileReader();
    reader.onloadend = () => {
      setPreviewUrl(reader.result);
    };
    reader.readAsDataURL(file);
  };

  const handleSubmit = async (event) => {
    event.preventDefault();
    setError("");

    const trimmedTitle = title.trim();
    const trimmedContent = content.trim();

    if (!trimmedTitle && !trimmedContent && !imageFile && !imageUrl.trim()) {
      setError("Укажите хотя бы заголовок, текст или изображение.");
      return;
    }

    setSubmitting(true);
    try {
      let uploadImageUrl = imageUrl.trim() || null;

      if (imageFile) {
        const formData = new FormData();
        formData.append("file", imageFile);
        const uploadResponse = await apiFetch("/news/upload-image", {
          method: "POST",
          body: formData,
          credentials: "same-origin"
        });
        if (uploadResponse.ok) {
          const uploadData = await uploadResponse.json();
          uploadImageUrl = uploadData.imageUrl;
        }
      }

      const response = await apiFetch("/news", {
        method: "POST",
        body: JSON.stringify({
          title: trimmedTitle || null,
          content: trimmedContent || null,
          imageUrl: uploadImageUrl
        })
      });
      const raw = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(raw.message || "Не удалось создать новость.");
      }

      const post = normalizeNewsPost(raw);
      onCreated(post);
      onClose();
    } catch (submitError) {
      setError(submitError.message || "Не удалось создать новость.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="news-modal-overlay" onClick={onClose} role="presentation">
      <div className="news-modal" onClick={(event) => event.stopPropagation()} role="dialog" aria-modal="true">
        <h2>Создать новость</h2>
        <form className="news-create-form" onSubmit={handleSubmit}>
          <label className="news-field">
            <span>Заголовок (необязательно)</span>
            <input
              type="text"
              value={title}
              maxLength={200}
              onChange={(event) => setTitle(event.target.value)}
              placeholder="Заголовок новости"
            />
          </label>
          <label className="news-field">
            <span>Текст (необязательно)</span>
            <textarea
              value={content}
              rows={5}
              maxLength={10000}
              onChange={(event) => setContent(event.target.value)}
              placeholder="Содержание новости"
            />
          </label>
          <label className="news-field">
            <span>Ссылка на изображение (необязательно)</span>
            <input
              type="text"
              value={imageUrl}
              onChange={(event) => {
                setImageUrl(event.target.value);
                setPreviewUrl("");
              }}
              placeholder="URL изображения"
            />
          </label>
          <label className="news-field">
            <span>Или загрузить файл</span>
            <input type="file" accept="image/*" onChange={handleImageChange} />
            {previewUrl ? (
              <img src={previewUrl} alt="Preview" className="news-image-preview" />
            ) : null}
          </label>
          {error ? (
            <p className="news-message news-message--error" role="alert">
              {error}
            </p>
          ) : null}
          <div className="news-form-actions">
            <button type="submit" className="primary-btn" disabled={submitting}>
              {submitting ? "Публикация…" : "Опубликовать"}
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

export default function NewsPage() {
  const { user } = useAuth();
  const [posts, setPosts] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [createOpen, setCreateOpen] = useState(false);

  const loadPosts = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const response = await apiFetch("/news");
      if (!response.ok) {
        throw new Error("Не удалось загрузить новости.");
      }
      const payload = await response.json();
      setPosts((Array.isArray(payload) ? payload : []).map(normalizeNewsPost).filter(Boolean));
    } catch (loadError) {
      setError(loadError.message || "Не удалось загрузить новости.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadPosts();
  }, [loadPosts]);

  const handleCreated = (post) => {
    if (post) {
      setPosts((prev) => [post, ...prev]);
    }
  };

  const handleDelete = async (id) => {
    if (!window.confirm("Удалить эту новость?")) {
      return;
    }
    try {
      const response = await apiFetch(`/news/${id}`, { method: "DELETE" });
      if (!response.ok) {
        throw new Error("Не удалось удалить новость.");
      }
      setPosts((prev) => prev.filter((post) => post.id !== id));
    } catch (deleteError) {
      setError(deleteError.message || "Не удалось удалить новость.");
    }
  };

  const isAdmin = user && isPlatformAdmin(user);

  return (
    <section className="panel news-panel">
      <header className="news-header">
        <div>
          <h1>
            <Newspaper size={24} aria-hidden="true" />
            <span>Новости</span>
          </h1>
          <p className="muted">Актуальные новости проекта BronyTV.</p>
        </div>
        {isAdmin ? (
          <button type="button" className="primary-btn" onClick={() => setCreateOpen(true)}>
            <Plus size={16} />
            <span>Создать новость</span>
          </button>
        ) : null}
      </header>

      {loading ? (
        <p className="muted">Загрузка новостей…</p>
      ) : error ? (
        <p className="news-message news-message--error" role="alert">
          {error}
        </p>
      ) : posts.length === 0 ? (
        <p className="muted">Пока нет новостей. Будьте первыми!</p>
      ) : (
        <ul className="news-list">
          {posts.map((post) => (
            <li key={post.id} className="news-card">
              {post.imageUrl ? (
                <img src={post.imageUrl} alt="" className="news-card-image" loading="lazy" />
              ) : null}
              <div className="news-card-body">
                {post.title ? <h2 className="news-card-title">{post.title}</h2> : null}
                {post.content ? <p className="news-card-content">{post.content}</p> : null}
                <div className="news-card-meta">
                  <span>@{post.authorUsername || "anonymous"}</span>
                  <span className="muted">· {formatDate(post.createdAt)}</span>
                </div>
                {isAdmin ? (
                  <button
                    type="button"
                    className="news-delete-btn"
                    onClick={() => handleDelete(post.id)}
                    aria-label="Удалить новость"
                  >
                    <Trash2 size={14} />
                  </button>
                ) : null}
              </div>
            </li>
          ))}
        </ul>
      )}

      <CreateNewsModal
        isOpen={createOpen}
        onClose={() => setCreateOpen(false)}
        onCreated={handleCreated}
      />
    </section>
  );
}
