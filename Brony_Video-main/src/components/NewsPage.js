import React, { useCallback, useEffect, useState } from "react";
import { createPortal } from "react-dom";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ArrowLeft, Newspaper, Plus, Trash2 } from "lucide-react";
import { useAuth } from "../auth/AuthContext";
import { useI18n } from "../i18n";
import { isPlatformAdmin } from "../auth/adminAccess";
import { apiFetch } from "../auth/api";
import CommentsSection from "./CommentsSection";

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
  const { t } = useI18n();
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
      setError(t("news.required"));
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
          setError(t("news.readFileError"));
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
        throw new Error(raw.message || t("news.createFailed"));
      }

      const post = normalizeNewsPost(raw);
      onCreated(post);
      onClose();
    } catch (submitError) {
      setError(submitError.message || t("news.createFailed"));
    } finally {
      setSubmitting(false);
    }
  };

  return createPortal(
    <div className="news-modal-overlay" onClick={onClose} role="presentation">
      <div className="news-modal" onClick={(event) => event.stopPropagation()} role="dialog" aria-modal="true">
        <h2>{t("news.titleCreate")}</h2>
        <form className="news-create-form" onSubmit={handleSubmit}>
          <label className="news-field">
            <span>{t("news.fieldTitle")}</span>
            <input
              type="text"
              value={title}
              maxLength={200}
              onChange={(event) => setTitle(event.target.value)}
              placeholder={t("news.placeholderTitle")}
            />
          </label>
          <label className="news-field">
            <span>{t("news.fieldContent")}</span>
            <textarea
              value={content}
              rows={5}
              maxLength={10000}
              onChange={(event) => setContent(event.target.value)}
              placeholder={t("news.placeholderContent")}
            />
          </label>
          <label className="news-field">
            <span>{t("news.fieldImageUrl")}</span>
            <input
              type="text"
              value={imageUrl}
              onChange={(event) => {
                setImageUrl(event.target.value);
                setPreviewUrls([]);
              }}
              placeholder={t("news.placeholderUrl")}
            />
          </label>
          <label className="news-field">
            <span>{t("news.fieldFiles")}</span>
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
              {submitting ? t("news.publishing") : t("news.publish")}
            </button>
            <button type="button" className="secondary-btn" onClick={onClose} disabled={submitting}>
              {t("news.cancel")}
            </button>
          </div>
        </form>
      </div>
    </div>,
    document.body
  );
}

function NewsCard({ post }) {
  const { t } = useI18n();
  const images = parseImageList(post.imageUrl);
  const previewImage = images.length > 0 ? images[0] : null;
  const fullContent = post.content ?? "";
  const truncatedContent = fullContent.length > 200 ? fullContent.substring(0, 200) + "..." : fullContent;

  return (
    <li className="news-card">
      {previewImage ? (
        <img src={previewImage} alt="" className="news-card-image" loading="lazy" />
      ) : null}
      <div className="news-card-body">
        {post.title ? <h2 className="news-card-title">{post.title}</h2> : null}
        {truncatedContent ? (
          <p className="news-card-content">{truncatedContent}</p>
        ) : null}
        <div className="news-card-meta">
          <span>@{post.authorUsername || "anonymous"}</span>
          <span className="muted">· {formatDate(post.createdAt)}</span>
        </div>
        <Link className="primary-btn news-open-btn" to={`/news/${post.id}`}>
          {t("news.open")}
        </Link>
      </div>
    </li>
  );
}

function NewsDetailView({ newsId }) {
  const navigate = useNavigate();
  const { user } = useAuth();
  const { t } = useI18n();
  const [post, setPost] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const loadPost = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const response = await apiFetch(`/news/${newsId}`);
      if (!response.ok) {
        throw new Error(t("news.loadError"));
      }
      const raw = await response.json();
      setPost(normalizeNewsPost(raw));
    } catch (loadError) {
      setError(loadError.message || t("news.loadError"));
    } finally {
      setLoading(false);
    }
  }, [newsId]);

  useEffect(() => {
    loadPost();
  }, [loadPost]);

  const handleDelete = async () => {
    if (!window.confirm(t("news.deleteConfirm"))) {
      return;
    }
    try {
      const response = await apiFetch(`/news/${newsId}`, { method: "DELETE" });
      if (!response.ok) {
        throw new Error(t("news.deleteFailed"));
      }
      navigate("/news");
    } catch (deleteError) {
      alert(deleteError.message || t("news.deleteFailed"));
    }
  };

  if (loading) {
    return (
      <section className="panel news-panel">
        <p className="muted">{t("news.loading")}</p>
      </section>
    );
  }

  if (error || !post) {
    return (
      <section className="panel news-panel">
        <div className="forum-error-state">
          <p className="forum-message forum-message--error">{error || t("news.loadError")}</p>
          <Link className="secondary-btn" to="/news">
            {t("news.back")}
          </Link>
        </div>
      </section>
    );
  }

  const images = parseImageList(post.imageUrl);
  const isAdmin = user && isPlatformAdmin(user);

  return (
    <section className="panel news-panel">
      <button type="button" className="secondary-btn forum-back-btn" onClick={() => navigate("/news")}>
        <ArrowLeft size={16} />
        <span>{t("news.back")}</span>
      </button>

      <article className="news-detail">
        {post.title ? <h1 className="news-detail-title">{post.title}</h1> : null}
        <p className="muted news-detail-meta">
          @{post.authorUsername || "anonymous"} · {formatDate(post.createdAt)}
        </p>
        {post.content ? <p className="news-detail-content">{post.content}</p> : null}
        {images.length > 0 ? (
          <div className="news-detail-images">
            {images.map((src, idx) => (
              <img key={idx} src={src} alt={`Image ${idx + 1}`} className="news-detail-image" loading="lazy" />
            ))}
          </div>
        ) : null}
        {isAdmin ? (
          <button type="button" className="primary-btn news-detail-delete" onClick={handleDelete}>
            <Trash2 size={14} />
            <span>{t("news.delete")}</span>
          </button>
        ) : null}
      </article>

      <div className="news-comments">
        <h2>{t("news.comments")}</h2>
        <CommentsSection entityType="news" entityId={newsId} />
      </div>
    </section>
  );
}

export default function NewsPage() {
  const { newsId } = useParams();
  const { user } = useAuth();
  const { t } = useI18n();
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
        throw new Error(t("news.loadError"));
      }
      const payload = await response.json();
      setPosts((Array.isArray(payload) ? payload : []).map(normalizeNewsPost).filter(Boolean));
    } catch (loadError) {
      setError(loadError.message || t("news.loadError"));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (!newsId) {
      loadPosts();
    }
  }, [loadPosts, newsId]);

  if (newsId) {
    return <NewsDetailView newsId={newsId} />;
  }

  const handleCreated = (post) => {
    if (post) {
      setPosts((prev) => [post, ...prev]);
    }
  };

  const isAdmin = user && isPlatformAdmin(user);

  return (
    <section className="panel news-panel">
      <header className="news-header">
        <div>
          <h1>
            <Newspaper size={24} aria-hidden="true" />
            <span>{t("news.title")}</span>
          </h1>
          <p className="muted">{t("news.subtitle")}</p>
        </div>
        {isAdmin ? (
          <button type="button" className="primary-btn" onClick={() => setCreateOpen(true)}>
            <Plus size={16} />
            <span>{t("news.create")}</span>
          </button>
        ) : null}
      </header>

      {loading ? (
        <p className="muted">{t("news.loading")}</p>
      ) : error ? (
        <p className="news-message news-message--error" role="alert">
          {error}
        </p>
      ) : posts.length === 0 ? (
        <p className="muted">{t("news.empty")}</p>
      ) : (
        <ul className="news-list">
          {posts.map((post) => (
            <NewsCard key={post.id} post={post} />
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
