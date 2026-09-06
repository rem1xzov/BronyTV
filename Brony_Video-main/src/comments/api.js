import { apiFetch } from "../auth/api";

// Единая точка доступа к комментариям (форум и новости). Поведение совпадает
// 1 в 1 — отличается только эндпоинт и имя поля «ответ на комментарий».
const ENDPOINTS = {
  forum: {
    list: (id) => `/forum/threads/${id}/posts`,
    create: (id) => `/forum/threads/${id}/posts`,
    like: (id) => `/forum/posts/${id}/like`,
    remove: (id) => `/forum/posts/${id}`,
    replyField: "replyToPostId"
  },
  news: {
    list: (id) => `/news/${id}/comments`,
    create: (id) => `/news/${id}/comments`,
    like: (id) => `/news/comments/${id}/like`,
    remove: (id) => `/news/comments/${id}`,
    replyField: "replyToCommentId"
  }
};

export function normalizeComment(raw) {
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
    createdAt: raw.createdAtUtc ?? raw.CreatedAtUtc ?? raw.createdAt ?? raw.CreatedAt,
    authorUsername: raw.authorUsername ?? raw.AuthorUsername ?? "",
    authorRole: raw.authorRole ?? raw.AuthorRole ?? "user",
    images: raw.images ?? raw.Images ?? [],
    likes: Number(raw.likes ?? raw.Likes ?? 0),
    likedByMe: Boolean(raw.likedByMe ?? raw.LikedByMe ?? false),
    replyToId: raw.replyToCommentId ?? raw.ReplyToCommentId ?? raw.replyToPostId ?? raw.ReplyToPostId ?? null,
    replyToAuthorUsername: raw.replyToAuthorUsername ?? raw.ReplyToAuthorUsername ?? "",
    replyToContent: raw.replyToContent ?? raw.ReplyToContent ?? "",
    authorStreak: Number(raw.authorStreak ?? raw.AuthorStreak ?? 0),
    authorStreakActive: Boolean(raw.authorStreakActive ?? raw.AuthorStreakActive ?? false)
  };
}

export function buildCommentTree(comments) {
  const nodes = {};
  comments.forEach((comment) => {
    nodes[comment.id] = { ...comment, children: [] };
  });

  const roots = [];
  comments.forEach((comment) => {
    const node = nodes[comment.id];
    const parentId = comment.replyToId;
    if (parentId && nodes[parentId]) {
      nodes[parentId].children.push(node);
    } else {
      roots.push(node);
    }
  });

  return roots;
}

export function fileToBase64(file) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onloadend = () => resolve(reader.result);
    reader.onerror = reject;
    reader.readAsDataURL(file);
  });
}

export async function fetchComments(entityType, entityId) {
  const response = await apiFetch(ENDPOINTS[entityType].list(entityId));
  if (!response.ok) {
    throw new Error("Не удалось загрузить комментарии.");
  }
  const payload = await response.json();
  return (Array.isArray(payload) ? payload : []).map(normalizeComment).filter(Boolean);
}

export async function createComment(entityType, entityId, { content, images, replyToId }) {
  const endpoint = ENDPOINTS[entityType];
  const body = { content, images };
  if (replyToId) {
    body[endpoint.replyField] = replyToId;
  }
  return apiFetch(endpoint.create(entityId), {
    method: "POST",
    body: JSON.stringify(body)
  });
}

export async function toggleCommentLike(entityType, commentId) {
  return apiFetch(ENDPOINTS[entityType].like(commentId), { method: "POST" });
}

export async function deleteComment(entityType, commentId) {
  return apiFetch(ENDPOINTS[entityType].remove(commentId), { method: "DELETE" });
}
