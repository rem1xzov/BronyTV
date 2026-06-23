import React, { useCallback, useEffect, useRef, useState } from "react";
import { ArrowLeft, MessageSquare, Search, Trash2 } from "lucide-react";
import {
  closeSupportTicket,
  fetchAllTickets,
  filterTickets,
  formatSupportDate,
  getLastMessagePreview,
  sendSupportMessage
} from "../support/tickets";

export default function AdminSupportPanel() {
  const messagesRef = useRef(null);
  const [tickets, setTickets] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [search, setSearch] = useState("");
  const [selectedId, setSelectedId] = useState(null);
  const [mobileView, setMobileView] = useState("list");
  const [reply, setReply] = useState("");
  const [sending, setSending] = useState(false);
  const [closing, setClosing] = useState(false);

  const loadTickets = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const list = await fetchAllTickets();
      setTickets(list);
      setSelectedId((current) => {
        if (current && list.some((ticket) => ticket.id === current)) {
          return current;
        }
        return list[0]?.id ?? null;
      });
    } catch (loadError) {
      setError(loadError.message || "Не удалось загрузить обращения.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadTickets();
  }, [loadTickets]);

  const filteredTickets = filterTickets(tickets, search);
  const selectedTicket = tickets.find((ticket) => ticket.id === selectedId) || null;

  useEffect(() => {
    const node = messagesRef.current;
    if (node) {
      node.scrollTop = node.scrollHeight;
    }
  }, [selectedTicket?.messages?.length, selectedTicket?.updatedAt]);

  const handleSelectTicket = (ticketId) => {
    setSelectedId(ticketId);
    setMobileView("chat");
    setReply("");
    setError("");
  };

  const handleBackToList = () => {
    setMobileView("list");
  };

  const handleSendReply = async (event) => {
    event.preventDefault();
    if (!selectedTicket || !reply.trim()) {
      return;
    }

    setSending(true);
    setError("");
    try {
      const updated = await sendSupportMessage({
        ticketId: selectedTicket.id,
        authorRole: "admin",
        authorUsername: "support",
        text: reply
      });
      setTickets((prev) => prev.map((ticket) => (ticket.id === updated.id ? updated : ticket)));
      setReply("");
    } catch (sendError) {
      setError(sendError.message || "Не удалось отправить ответ.");
    } finally {
      setSending(false);
    }
  };

  const handleCloseTicket = async () => {
    if (!selectedTicket || closing) {
      return;
    }

    const ticketId = selectedTicket.id;
    const confirmed = window.confirm("Закрыть это обращение? Оно исчезнет из списка активных.");
    if (!confirmed) {
      return;
    }

    setClosing(true);
    setError("");
    try {
      await closeSupportTicket(ticketId);
      setTickets((prev) => {
        const remaining = prev.filter((ticket) => ticket.id !== ticketId);
        setSelectedId((current) => (current === ticketId ? remaining[0]?.id ?? null : current));
        return remaining;
      });
      setMobileView("list");
    } catch (closeError) {
      setError(closeError.message || "Не удалось закрыть обращение.");
    } finally {
      setClosing(false);
    }
  };

  return (
    <article className="admin-card admin-card--support">
      <div className="admin-support-shell">
        <section
          className={`admin-support-list-pane${mobileView === "chat" ? " admin-support-list-pane--hidden-mobile" : ""}`}
        >
          <header className="admin-support-list-header">
            <h2>Обращения в поддержку</h2>
            <p className="muted">Активные тикеты пользователей</p>
          </header>

          <label className="admin-support-search">
            <Search size={14} aria-hidden="true" />
            <input
              type="search"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Поиск по юзернейму или теме"
              aria-label="Поиск обращений"
            />
          </label>

          {loading ? (
            <p className="muted">Загрузка обращений…</p>
          ) : filteredTickets.length === 0 ? (
            <p className="muted">Активных обращений нет.</p>
          ) : (
            <ul className="admin-support-ticket-list">
              {filteredTickets.map((ticket) => (
                <li key={ticket.id}>
                  <button
                    type="button"
                    className={`admin-support-ticket-card${selectedId === ticket.id ? " is-active" : ""}`}
                    onClick={() => handleSelectTicket(ticket.id)}
                  >
                    <strong className="admin-support-ticket-subject">{ticket.subject}</strong>
                    <span className="admin-support-ticket-user">@{ticket.username || "anonymous"}</span>
                    <span className="admin-support-ticket-preview">{getLastMessagePreview(ticket)}</span>
                    <time className="muted admin-support-ticket-time" dateTime={ticket.updatedAt}>
                      {formatSupportDate(ticket.updatedAt)}
                    </time>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </section>

        <section
          className={`admin-support-chat-pane${mobileView === "list" ? " admin-support-chat-pane--hidden-mobile" : ""}`}
        >
          {selectedTicket ? (
            <>
              <header className="admin-support-chat-header">
                <button
                  type="button"
                  className="secondary-btn admin-support-back-btn"
                  onClick={handleBackToList}
                >
                  <ArrowLeft size={14} />
                  <span>К списку</span>
                </button>
                <div className="admin-support-chat-heading">
                  <h3>{selectedTicket.subject}</h3>
                  <p className="muted">
                    @{selectedTicket.username || "anonymous"} · {formatSupportDate(selectedTicket.createdAt)}
                  </p>
                </div>
                <button
                  type="button"
                  className="secondary-btn admin-support-close-btn"
                  onClick={handleCloseTicket}
                  disabled={closing}
                >
                  <Trash2 size={14} />
                  <span>{closing ? "Закрытие…" : "Закрыть обращение"}</span>
                </button>
              </header>

              <ul className="admin-support-chat-messages" ref={messagesRef}>
                {selectedTicket.messages.map((message) => {
                  const isStaff = message.authorRole === "admin";
                  return (
                    <li
                      key={message.id}
                      className={`admin-support-chat-message${isStaff ? " admin-support-chat-message--staff" : " admin-support-chat-message--user"}`}
                    >
                      <p className="admin-support-chat-message-meta">
                        <span>{isStaff ? "Вы (поддержка)" : `@${message.authorUsername || "user"}`}</span>
                        <time dateTime={message.createdAt}>{formatSupportDate(message.createdAt)}</time>
                      </p>
                      <p className="admin-support-chat-message-text">{message.text}</p>
                    </li>
                  );
                })}
              </ul>

              <form className="admin-support-reply-form" onSubmit={handleSendReply}>
                <textarea
                  className="admin-support-reply-input"
                  rows={2}
                  value={reply}
                  onChange={(event) => setReply(event.target.value)}
                  placeholder="Ответ пользователю…"
                  aria-label="Ответ пользователю"
                />
                <button type="submit" className="primary-btn admin-support-reply-btn" disabled={sending || !reply.trim()}>
                  <MessageSquare size={14} />
                  <span>{sending ? "…" : "Ответить"}</span>
                </button>
              </form>
            </>
          ) : (
            <div className="admin-support-chat-empty">
              <p className="muted">Выберите обращение из списка слева.</p>
            </div>
          )}

          {error ? (
            <p className="admin-message admin-message--error" role="alert">
              {error}
            </p>
          ) : null}
        </section>
      </div>
    </article>
  );
}
