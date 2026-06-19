import { apiFetch } from "../auth/api";

const STORAGE_KEY = "bronytv-support-tickets";

function readLocalTickets() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    const parsed = raw ? JSON.parse(raw) : [];
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

function writeLocalTickets(tickets) {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(tickets));
}

function normalizeMessage(raw) {
  if (!raw || typeof raw !== "object") {
    return null;
  }

  const id = raw.id ?? raw.Id;
  const text = raw.text ?? raw.Text ?? raw.content ?? raw.Content ?? "";
  if (!id || !text) {
    return null;
  }

  return {
    id,
    text,
    authorRole: raw.authorRole ?? raw.AuthorRole ?? "user",
    authorUsername: raw.authorUsername ?? raw.AuthorUsername ?? "",
    createdAt: raw.createdAt ?? raw.CreatedAt ?? new Date().toISOString()
  };
}

function normalizeTicket(raw) {
  if (!raw || typeof raw !== "object") {
    return null;
  }

  const id = raw.id ?? raw.Id;
  const userId = raw.userId ?? raw.UserId;
  const subject = raw.subject ?? raw.Subject ?? "";
  if (!id || !userId || !subject) {
    return null;
  }

  const messages = Array.isArray(raw.messages ?? raw.Messages)
    ? (raw.messages ?? raw.Messages).map(normalizeMessage).filter(Boolean)
    : [];

  return {
    id,
    userId,
    username: raw.username ?? raw.Username ?? "",
    subject,
    description: raw.description ?? raw.Description ?? "",
    status: raw.status ?? raw.Status ?? "open",
    createdAt: raw.createdAt ?? raw.CreatedAt ?? new Date().toISOString(),
    updatedAt: raw.updatedAt ?? raw.UpdatedAt ?? raw.createdAt ?? raw.CreatedAt ?? new Date().toISOString(),
    messages
  };
}

function createId(prefix) {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return `${prefix}-${crypto.randomUUID()}`;
  }
  return `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 9)}`;
}

async function tryApi(path, options) {
  try {
    const response = await apiFetch(path, options);
    if (response.status === 404) {
      return null;
    }
    if (!response.ok) {
      const payload = await response.json().catch(() => ({}));
      throw new Error(payload.message || "Ошибка сервера поддержки.");
    }
    if (response.status === 204) {
      return null;
    }
    return response.json();
  } catch (error) {
    if (error?.message?.includes("Failed to fetch")) {
      return null;
    }
    throw error;
  }
}

export function getLastMessagePreview(ticket) {
  const last = ticket?.messages?.[ticket.messages.length - 1];
  return last?.text || ticket?.description || "";
}

export function formatSupportDate(value) {
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
    hour: "2-digit",
    minute: "2-digit"
  });
}

export async function fetchUserTicket(userId) {
  const payload = await tryApi(`/support/tickets/me`);
  const normalized = normalizeTicket(payload);
  if (normalized) {
    return normalized;
  }

  return readLocalTickets().find((ticket) => ticket.userId === userId) || null;
}

export async function fetchAllTickets() {
  const payload = await tryApi("/support/tickets");
  if (Array.isArray(payload)) {
    return payload.map(normalizeTicket).filter(Boolean);
  }

  return readLocalTickets().sort(
    (a, b) => new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime()
  );
}

export async function createSupportTicket({ userId, username, subject, description }) {
  const payload = await tryApi("/support/tickets", {
    method: "POST",
    body: JSON.stringify({ subject, description })
  });
  const normalized = normalizeTicket(payload);
  if (normalized) {
    return normalized;
  }

  const now = new Date().toISOString();
  const initialMessage = {
    id: createId("msg"),
    text: description.trim(),
    authorRole: "user",
    authorUsername: username || "user",
    createdAt: now
  };
  const ticket = {
    id: createId("ticket"),
    userId,
    username: username || "",
    subject: subject.trim(),
    description: description.trim(),
    status: "open",
    createdAt: now,
    updatedAt: now,
    messages: [initialMessage]
  };

  const tickets = readLocalTickets().filter((item) => item.userId !== userId);
  tickets.unshift(ticket);
  writeLocalTickets(tickets);
  return ticket;
}

export async function sendSupportMessage({ ticketId, authorRole, authorUsername, text }) {
  const trimmed = text.trim();
  if (!trimmed) {
    throw new Error("Сообщение не может быть пустым.");
  }

  const payload = await tryApi(`/support/tickets/${ticketId}/messages`, {
    method: "POST",
    body: JSON.stringify({ text: trimmed })
  });
  const normalized = normalizeTicket(payload);
  if (normalized) {
    return normalized;
  }

  const tickets = readLocalTickets();
  const index = tickets.findIndex((ticket) => ticket.id === ticketId);
  if (index === -1) {
    throw new Error("Обращение не найдено.");
  }

  const now = new Date().toISOString();
  const message = {
    id: createId("msg"),
    text: trimmed,
    authorRole,
    authorUsername,
    createdAt: now
  };

  tickets[index] = {
    ...tickets[index],
    updatedAt: now,
    messages: [...(tickets[index].messages || []), message]
  };
  writeLocalTickets(tickets);
  return tickets[index];
}

export function filterTickets(tickets, query) {
  const normalized = query.trim().toLowerCase();
  if (!normalized) {
    return tickets;
  }

  return tickets.filter((ticket) => {
    const haystack = [
      ticket.subject,
      ticket.username,
      getLastMessagePreview(ticket)
    ]
      .join(" ")
      .toLowerCase();
    return haystack.includes(normalized);
  });
}
