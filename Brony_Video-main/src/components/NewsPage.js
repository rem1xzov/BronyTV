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

function fileToBase64(file) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onloadend = () => resolve(reader.result);
    reader.onerror = reject;
    reader.readAsDataURL(file);
  });
}

function parseImageList(imageUrl) {
  if (!imageUrl) {
    return [];
  }

  try {
    const parsed = JSON.parse(imageUrl);
    if (Array.isArray(parsed)) {
      return parsed.filter((item) => typeof item === "string" && item.length > 0);
    }
  } catch {
    // single image URL (legacy)
    return [imageUrl];
  }

  return [imageUrl];
}

function CreateNewsModal({ isOpen, onClose, onCreated }) {
  const [title, setTitle] = useState("");
  const [content, setContent] = useState("");
  const [imageUrl, setImageUrl] = useState("");
  const [imageFiles, setImageFiles] = useState([]);
  const [previewUrls, setPreviewUrls] = useState([]);
  const [error, setError] = useState("");
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!isOpen) {
      setTitle("");
      setContent("");
      setImageUrl("");
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
    const limited = files.slice(0, 5);
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
    const trimmedContent = content.trim();

    if (!trimmedTitle && !trimmedContent && imageFiles.length === 0 && !imageUrl.trim()) {
      setError("Укажите хотя бы заголовок, текст или изображение.");
      return;
    }

    setSubmitting(true);
    try {
      let uploadImageUrl = imageUrl.trim() || null;

      if (imageFiles.length > 0) {
        try {
          const base64Array = await Promise.all(imageFiles.map((file) => fileToBase64(file)));
          uploadImageUrl = JSON.stringify(base64Array);
        } catch (readError) {
          setError("Не удалось прочитать файлы изображений.");
          setSubmitting(false);
          return;
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
                setPreviewUrls([]);
              }}
              placeholder="URL изображения"
            />
          </label>
          <label className="news-field">
            <span>Загрузить файлы (до 5)</span>
            <input type="file" accept="image/*" multiple onChange={handleImageChange} />
            {previewUrls.length > 0 ? (
              <div className="news-image-preview-row">
                {previewUrls.map((src, idx) => (
                  <img key={idx} src={src} alt={`Preview ${idx + 1}`} className="news-image-preview" />
                ))}
              </div>
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

function NewsCard({ post, isAdmin, onDelete, isExpanded, onToggleExpand }) {
  const images = parseImageList(post.imageUrl);
  const previewImage = images.length > 0 ? images[0] : null;
  const fullContent = post.content ?? "";
  const truncatedContent = fullContent.length > 200 ? fullContent.substring(0, 200) + "..." : fullContent;
  const displayContent = isExpanded ? fullContent : truncatedContent;

  return (
    <li className="news-card">
      {previewImage ? (
        <img src={previewImage} alt="" className="news-card-image" loading="lazy" />
      ) : null}
      <div className="news-card-body">
        {post.title ? <h2 className="news-card-title">{post.title}</h2> : null}
        {displayContent ? (
          <p className="news-card-content">
            {displayContent}
          </p>
        ) : null}
        <div className="news-card-meta">
          <span>@{post.authorUsername || "anonymous"}</span>
          <span className="muted">· {formatDate(post.createdAt)}</span>
        </div>
        {fullContent.length > 0 ? (
          <button type="button"
            className="news-card-read-more"
            onClick={onToggleExpand}
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              justifyContent: 'center',
              height: '36px',
              padding: '0 16px',
              borderRadius: '18px',
              background: 'linear-gradient(135deg, #ec4899, #a855f7)',
              color: 'white',
              border: 'none',
              cursor: 'pointer',
              fontSize: '0.875rem',
              fontWeight: 500,
            }}
          >
            {isExpanded ? "Свернуть" : "Читать далее"}
          </button>
        ) : null}
        {isExpanded && images.length > 1 ? (
          <div className="news-card-gallery">
            {images.slice(1).map((src, idx) => (
              <img key={idx} src={src} alt={`Image ${idx + 2}`} className="news-card-gallery-image" loading="lazy" />
            ))}
          </div>
        ) : null}
        {isAdmin ? (
          <button
            type="button"
            className="news-delete-btn"
            onClick={() => onDelete(post.id)}
            aria-label="Удалить новость"
          >
            <Trash2 size={14} />
          </button>
        ) : null}
      </div>
    </li>
  );
}

export default function NewsPage() {
  const { user } = useAuth();
  const [posts, setPosts] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [createOpen, setCreateOpen] = useState(false);
  const [expandedNews, setExpandedNews] = useState({});

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

  const toggleExpand = (newsId) => {
    setExpandedNews((prev) => ({ ...prev, [newsId]: !prev[newsId] }));
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
            <NewsCard
              key={post.id}
              post={post}
              isAdmin={isAdmin}
              onDelete={handleDelete}
              isExpanded={!!expandedNews[post.id]}
              onToggleExpand={() => toggleExpand(post.id)}
            />
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
