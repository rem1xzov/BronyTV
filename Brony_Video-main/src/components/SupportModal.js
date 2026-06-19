import React, { useEffect, useId, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { LifeBuoy, Send, X } from "lucide-react";
import {
  createSupportTicket,
  fetchUserTicket,
  formatSupportDate,
  sendSupportMessage
} from "../support/tickets";

export default function SupportModal({ isOpen, onClose, user }) {
  const titleId = useId();
  const messagesRef = useRef(null);
  const [loading, setLoading] = useState(true);
  const [ticket, setTicket] = useState(null);
  const [error, setError] = useState("");
  const [subject, setSubject] = useState("");
  const [description, setDescription] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [messageInput, setMessageInput] = useState("");
  const [sending, setSending] = useState(false);

  useEffect(() => {
    if (!isOpen || !user?.id) {
      return undefined;
    }

    let cancelled = false;

    const loadTicket = async () => {
      setLoading(true);
      setError("");
      try {
        const existing = await fetchUserTicket(user.id);
        if (!cancelled) {
          setTicket(existing);
        }
      } catch (loadError) {
        if (!cancelled) {
          setError(loadError.message || "Не удалось загрузить обращение.");
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    };

    loadTicket();
    return () => {
      cancelled = true;
    };
  }, [isOpen, user?.id]);

  useEffect(() => {
    if (!isOpen) {
      setSubject("");
      setDescription("");
      setMessageInput("");
      setError("");
      setSubmitting(false);
      setSending(false);
      setTicket(null);
      setLoading(false);
    }
  }, [isOpen]);

  useEffect(() => {
    if (!isOpen || !ticket?.messages?.length) {
      return;
    }
    const node = messagesRef.current;
    if (node) {
      node.scrollTop = node.scrollHeight;
    }
  }, [isOpen, ticket?.messages?.length, ticket?.updatedAt]);

  useEffect(() => {
    if (!isOpen) {
      return undefined;
    }

    const handleKeyDown = (event) => {
      if (event.key === "Escape") {
        onClose();
      }
    };

    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [isOpen, onClose]);

  if (!isOpen || !user) {
    return null;
  }

  const handleBackdropClick = (event) => {
    if (event.target === event.currentTarget) {
      onClose();
    }
  };

  const handleCreateTicket = async (event) => {
    event.preventDefault();
    setError("");

    const trimmedSubject = subject.trim();
    const trimmedDescription = description.trim();
    if (!trimmedSubject) {
      setError("Укажите тему проблемы.");
      return;
    }
    if (!trimmedDescription) {
      setError("Опишите проблему.");
      return;
    }

    setSubmitting(true);
    try {
      const created = await createSupportTicket({
        userId: user.id,
        username: user.username,
        subject: trimmedSubject,
        description: trimmedDescription
      });
      setTicket(created);
      setSubject("");
      setDescription("");
    } catch (submitError) {
      setError(submitError.message || "Не удалось отправить обращение.");
    } finally {
      setSubmitting(false);
    }
  };

  const handleSendMessage = async (event) => {
    event.preventDefault();
    if (!ticket || !messageInput.trim()) {
      return;
    }

    setSending(true);
    setError("");
    try {
      const updated = await sendSupportMessage({
        ticketId: ticket.id,
        authorRole: "user",
        authorUsername: user.username || user.email || "user",
        text: messageInput
      });
      setTicket(updated);
      setMessageInput("");
    } catch (sendError) {
      setError(sendError.message || "Не удалось отправить сообщение.");
    } finally {
      setSending(false);
    }
  };

  return createPortal(
    <div className="support-modal-overlay" onClick={handleBackdropClick} role="presentation">
      <div
        className="support-modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        onClick={(event) => event.stopPropagation()}
      >
        <button type="button" className="support-modal-close" onClick={onClose} aria-label="Закрыть">
          <X size={20} />
        </button>

        <header className="support-modal-header">
          <div className="support-modal-icon" aria-hidden="true">
            <LifeBuoy size={28} />
          </div>
          <h2 id={titleId}>Поддержка</h2>
          <p className="support-modal-subtitle">
            {ticket ? ticket.subject : "Опишите проблему — мы ответим в этом чате"}
          </p>
        </header>

        <div className="support-modal-body">
          {loading ? (
            <p className="muted support-modal-loading">Загрузка…</p>
          ) : ticket ? (
            <div className="support-chat">
              <ul className="support-chat-messages" ref={messagesRef}>
                {ticket.messages.map((message) => {
                  const isStaff = message.authorRole === "admin";
                  return (
                    <li
                      key={message.id}
                      className={`support-chat-message${isStaff ? " support-chat-message--staff" : " support-chat-message--user"}`}
                    >
                      <p className="support-chat-message-meta">
                        <span>{isStaff ? "Поддержка" : `@${message.authorUsername || "вы"}`}</span>
                        <time dateTime={message.createdAt}>{formatSupportDate(message.createdAt)}</time>
                      </p>
                      <p className="support-chat-message-text">{message.text}</p>
                    </li>
                  );
                })}
              </ul>
              <form className="support-chat-composer" onSubmit={handleSendMessage}>
                <textarea
                  className="support-chat-input"
                  rows={2}
                  value={messageInput}
                  onChange={(event) => setMessageInput(event.target.value)}
                  placeholder="Напишите сообщение…"
                  aria-label="Сообщение в поддержку"
                />
                <button
                  type="submit"
                  className="primary-btn support-chat-send"
                  disabled={sending || !messageInput.trim()}
                >
                  <Send size={16} />
                  <span>{sending ? "Отправка…" : "Отправить"}</span>
                </button>
              </form>
            </div>
          ) : (
            <form className="support-create-form" onSubmit={handleCreateTicket}>
              <label className="support-field">
                <span>Тема проблемы</span>
                <input
                  type="text"
                  value={subject}
                  onChange={(event) => setSubject(event.target.value)}
                  placeholder="Кратко опишите суть"
                  maxLength={120}
                  autoComplete="off"
                />
              </label>
              <label className="support-field">
                <span>Описание проблемы</span>
                <textarea
                  rows={5}
                  value={description}
                  onChange={(event) => setDescription(event.target.value)}
                  placeholder="Расскажите подробнее, что произошло"
                  maxLength={2000}
                />
              </label>
              {error ? (
                <p className="support-message support-message--error" role="alert">
                  {error}
                </p>
              ) : null}
              <button type="submit" className="primary-btn support-submit-btn" disabled={submitting}>
                {submitting ? "Отправка…" : "Отправить обращение"}
              </button>
            </form>
          )}
          {ticket && error ? (
            <p className="support-message support-message--error" role="alert">
              {error}
            </p>
          ) : null}
        </div>
      </div>
    </div>,
    document.body
  );
}
