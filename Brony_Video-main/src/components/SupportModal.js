import React, { useEffect, useId, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { ArrowLeft, LifeBuoy, Plus, Send, X } from "lucide-react";
import {
  createSupportTicket,
  fetchUserTickets,
  formatSupportDate,
  getLastMessagePreview,
  getTicketStatusLabel,
  isOwnMessage,
  sendSupportMessage
} from "../support/tickets";

export default function SupportModal({ isOpen, onClose, user }) {
  const titleId = useId();
  const messagesRef = useRef(null);
  const userId = user?.id ?? user?.Id;

  const [loading, setLoading] = useState(true);
  const [tickets, setTickets] = useState([]);
  const [selectedId, setSelectedId] = useState(null);
  const [mobileView, setMobileView] = useState("list");
  const [showCreateForm, setShowCreateForm] = useState(false);
  const [error, setError] = useState("");
  const [subject, setSubject] = useState("");
  const [description, setDescription] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [messageInput, setMessageInput] = useState("");
  const [sending, setSending] = useState(false);

  const selectedTicket = tickets.find((ticket) => ticket.id === selectedId) || null;
  const isTicketClosed = selectedTicket?.isClosed || selectedTicket?.status === "closed";

  useEffect(() => {
    if (!isOpen || !userId) {
      return undefined;
    }

    let cancelled = false;

    const loadTickets = async () => {
      setLoading(true);
      setError("");
      try {
        const list = await fetchUserTickets(userId);
        if (!cancelled) {
          setTickets(list);
          setSelectedId((current) => {
            if (current && list.some((ticket) => ticket.id === current)) {
              return current;
            }
            return list[0]?.id ?? null;
          });
        }
      } catch (loadError) {
        if (!cancelled) {
          setError(loadError.message || "Не удалось загрузить обращения.");
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    };

    loadTickets();
    return () => {
      cancelled = true;
    };
  }, [isOpen, userId]);

  useEffect(() => {
    if (!isOpen) {
      setSubject("");
      setDescription("");
      setMessageInput("");
      setError("");
      setSubmitting(false);
      setSending(false);
      setTickets([]);
      setSelectedId(null);
      setMobileView("list");
      setShowCreateForm(false);
      setLoading(false);
    }
  }, [isOpen]);

  useEffect(() => {
    if (!isOpen || !selectedTicket?.messages?.length) {
      return;
    }
    const node = messagesRef.current;
    if (node) {
      node.scrollTop = node.scrollHeight;
    }
  }, [isOpen, selectedTicket?.messages?.length, selectedTicket?.updatedAt, mobileView]);

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

  const handleSelectTicket = (ticketId) => {
    setSelectedId(ticketId);
    setShowCreateForm(false);
    setMobileView("chat");
    setMessageInput("");
    setError("");
  };

  const handleBackToList = () => {
    setMobileView("list");
    setShowCreateForm(false);
  };

  const handleOpenCreateForm = () => {
    setShowCreateForm(true);
    setSelectedId(null);
    setMobileView("chat");
    setSubject("");
    setDescription("");
    setError("");
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
        userId,
        username: user.username,
        subject: trimmedSubject,
        description: trimmedDescription
      });
      setTickets((prev) => [created, ...prev.filter((ticket) => ticket.id !== created.id)]);
      setSelectedId(created.id);
      setShowCreateForm(false);
      setSubject("");
      setDescription("");
      setMobileView("chat");
    } catch (submitError) {
      setError(submitError.message || "Не удалось отправить обращение.");
    } finally {
      setSubmitting(false);
    }
  };

  const handleSendMessage = async (event) => {
    event.preventDefault();
    if (!selectedTicket || !messageInput.trim() || isTicketClosed) {
      return;
    }

    setSending(true);
    setError("");
    try {
      const updated = await sendSupportMessage({
        ticketId: selectedTicket.id,
        authorRole: "user",
        authorUsername: user.username || user.email || "user",
        senderId: userId,
        text: messageInput
      });
      setTickets((prev) => prev.map((ticket) => (ticket.id === updated.id ? updated : ticket)));
      setMessageInput("");
    } catch (sendError) {
      setError(sendError.message || "Не удалось отправить сообщение.");
    } finally {
      setSending(false);
    }
  };

  const renderMessage = (message) => {
    const isMine = isOwnMessage(message, userId);
    return (
      <li
        key={message.id}
        className={`support-chat-message${isMine ? " support-chat-message--user" : " support-chat-message--staff"}`}
      >
        <p className="support-chat-message-meta">
          <span>{isMine ? "@вы" : "@поддержка"}</span>
          <time dateTime={message.createdAt}>{formatSupportDate(message.createdAt)}</time>
        </p>
        <p className="support-chat-message-text">{message.text}</p>
      </li>
    );
  };

  const listPaneHidden = mobileView === "chat";
  const chatPaneHidden = mobileView === "list";

  return createPortal(
    <div className="support-modal-overlay" onClick={handleBackdropClick} role="presentation">
      <div
        className="support-modal support-modal--messenger"
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        onClick={(event) => event.stopPropagation()}
      >
        <button type="button" className="support-modal-close" onClick={onClose} aria-label="Закрыть">
          <X size={18} />
        </button>

        <header className="support-modal-header support-modal-header--compact">
          <div className="support-modal-icon" aria-hidden="true">
            <LifeBuoy size={22} />
          </div>
          <h2 id={titleId}>Поддержка</h2>
        </header>

        <div className="support-modal-body support-messenger-shell">
          {loading ? (
            <p className="muted support-modal-loading">Загрузка…</p>
          ) : (
            <>
              <section
                className={`support-messenger-list-pane${listPaneHidden ? " support-messenger-list-pane--hidden-mobile" : ""}`}
              >
                <div className="support-messenger-list-header">
                  <p className="muted support-messenger-list-caption">Мои обращения</p>
                  <button
                    type="button"
                    className="primary-btn support-messenger-new-btn"
                    onClick={handleOpenCreateForm}
                  >
                    <Plus size={14} />
                    <span>Новое обращение</span>
                  </button>
                </div>

                {tickets.length === 0 ? (
                  <p className="muted support-messenger-empty">Обращений пока нет. Создайте первое.</p>
                ) : (
                  <ul className="support-messenger-ticket-list">
                    {tickets.map((ticket) => {
                      const isClosed = ticket.isClosed || ticket.status === "closed";
                      return (
                        <li key={ticket.id}>
                          <button
                            type="button"
                            className={`support-messenger-ticket-card${selectedId === ticket.id && !showCreateForm ? " is-active" : ""}`}
                            onClick={() => handleSelectTicket(ticket.id)}
                          >
                            <div className="support-messenger-ticket-card-top">
                              <strong className="support-messenger-ticket-subject">{ticket.subject}</strong>
                              <span
                                className={`support-messenger-status${isClosed ? " support-messenger-status--closed" : " support-messenger-status--open"}`}
                              >
                                {getTicketStatusLabel(ticket)}
                              </span>
                            </div>
                            <span className="support-messenger-ticket-preview">{getLastMessagePreview(ticket)}</span>
                            <time className="muted support-messenger-ticket-time" dateTime={ticket.updatedAt}>
                              {formatSupportDate(ticket.updatedAt)}
                            </time>
                          </button>
                        </li>
                      );
                    })}
                  </ul>
                )}
              </section>

              <section
                className={`support-messenger-chat-pane${chatPaneHidden ? " support-messenger-chat-pane--hidden-mobile" : ""}`}
              >
                {showCreateForm ? (
                  <>
                    <header className="support-messenger-chat-header">
                      <button type="button" className="secondary-btn support-messenger-back-btn" onClick={handleBackToList}>
                        <ArrowLeft size={14} />
                        <span>К списку</span>
                      </button>
                      <div className="support-messenger-chat-heading">
                        <h3>Новое обращение</h3>
                        <p className="muted">Опишите проблему — мы ответим в этом чате</p>
                      </div>
                    </header>
                    <form className="support-create-form support-create-form--in-pane" onSubmit={handleCreateTicket}>
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
                          rows={4}
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
                        {submitting ? "Отправка…" : "Создать обращение"}
                      </button>
                    </form>
                  </>
                ) : selectedTicket ? (
                  <>
                    <header className="support-messenger-chat-header">
                      <button type="button" className="secondary-btn support-messenger-back-btn" onClick={handleBackToList}>
                        <ArrowLeft size={14} />
                        <span>К списку</span>
                      </button>
                      <div className="support-messenger-chat-heading">
                        <h3>{selectedTicket.subject}</h3>
                        <p className="muted">
                          {getTicketStatusLabel(selectedTicket)} · {formatSupportDate(selectedTicket.createdAt)}
                        </p>
                      </div>
                    </header>

                    <ul className="support-chat-messages support-messenger-messages" ref={messagesRef}>
                      {selectedTicket.messages.map(renderMessage)}
                    </ul>

                    {isTicketClosed ? (
                      <p className="muted support-messenger-closed-notice">Обращение закрыто. Создайте новое, если нужна помощь.</p>
                    ) : (
                      <form className="support-chat-composer support-messenger-composer" onSubmit={handleSendMessage}>
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
                          aria-label="Отправить"
                        >
                          <Send size={14} />
                          <span className="support-chat-send-label">{sending ? "…" : "Отправить"}</span>
                        </button>
                      </form>
                    )}
                  </>
                ) : (
                  <div className="support-messenger-chat-empty">
                    <p className="muted">Выберите обращение из списка или создайте новое.</p>
                  </div>
                )}

                {!showCreateForm && selectedTicket && error ? (
                  <p className="support-message support-message--error" role="alert">
                    {error}
                  </p>
                ) : null}
              </section>
            </>
          )}
        </div>
      </div>
    </div>,
    document.body
  );
}
