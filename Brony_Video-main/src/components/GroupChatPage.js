import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";

import {
  ArrowLeft,
  Bot,
  Check,
  ChevronRight,
  LockKeyhole,
  LogIn,
  Pencil,
  Plus,
  Send,
  Trash2,
  UserPlus,
  Users,
  X
} from "lucide-react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { BOT_CATALOG, BotAvatar } from "./AiChatPage";

// Порог, после которого в менеджере состава показываем ненавязчивое предупреждение
// про время ответа. Легко поменять в одном месте.
const GROUP_CHAT_WARN_THRESHOLD = 6;

const GROUP_CHAT_CONFIG = {
  user: { baseUrl: "/api/group-chats" },
  admin: { baseUrl: "/api/admin/group-chats" }
};

let groupMsgSeq = 0;
const nextGroupId = () => `g-${Date.now()}-${groupMsgSeq++}`;

const findBot = (id) => BOT_CATALOG.find((b) => b.id === id) || null;

// Читает SSE-стрим группового чата: события bot_start/bot_end, текст ответов,
// Id сохранённого сообщения пользователя и сигнал «лимит исчерпан».
const consumeGroupStream = async (
  res,
  { onUserMessageId, onBotStart, onText, onBotEnd, onLimit }
) => {
  const reader = res.body.getReader();
  const decoder = new TextDecoder();
  let acc = "";
  let finished = false;

  while (!finished) {
    const { done, value } = await reader.read();
    if (done) break;
    acc += decoder.decode(value, { stream: true });

    let newlineIdx;
    while ((newlineIdx = acc.indexOf("\n")) !== -1) {
      const line = acc.slice(0, newlineIdx).trim();
      acc = acc.slice(newlineIdx + 1);
      if (!line.startsWith("data:")) continue;
      const data = line.slice(5).trim();
      if (data === "[DONE]") {
        finished = true;
        break;
      }
      let parsed;
      try {
        parsed = JSON.parse(data);
      } catch {
        continue;
      }
      if (parsed && typeof parsed.error === "string") {
        throw new Error(parsed.error);
      }
      if (parsed && typeof parsed.userMessageId === "number" && onUserMessageId) {
        onUserMessageId(parsed.userMessageId);
      }
      if (parsed && parsed.limit === true && onLimit) {
        onLimit();
        continue;
      }
      if (parsed && parsed.event === "bot_start" && onBotStart) {
        onBotStart(parsed.characterId);
        continue;
      }
      if (parsed && parsed.event === "bot_end" && onBotEnd) {
        onBotEnd(parsed.characterId);
        continue;
      }
      if (parsed && typeof parsed.text === "string" && parsed.text.length > 0 && onText) {
        onText(parsed.characterId, parsed.text);
      }
    }
  }
};

function GroupChatPage({ mode = "user" }) {
  const cfg = GROUP_CHAT_CONFIG[mode] || GROUP_CHAT_CONFIG.user;
  const isAdminMode = mode === "admin";
  const navigate = useNavigate();
  const { user, loading, refreshUser } = useAuth();

  const [groups, setGroups] = useState([]);
  const [activeGroupId, setActiveGroupId] = useState(null);
  const [activeGroup, setActiveGroup] = useState(null); // { id, name, participants, messages }
  const [input, setInput] = useState("");
  const [streaming, setStreaming] = useState(false);
  const [error, setError] = useState("");

  const [showManager, setShowManager] = useState(false);
  const [managerMode, setManagerMode] = useState("create"); // "create" | "edit"
  const [groupName, setGroupName] = useState("");
  const [selectedIds, setSelectedIds] = useState([]);

  const [confirmDelete, setConfirmDelete] = useState(false);
  const [chatView, setChatView] = useState(false); // mobile: list vs chat

  const scrollRef = useRef(null);
  const streamRef = useRef(null);
  const textareaRef = useRef(null);

  const messages = useMemo(() => activeGroup?.messages || [], [activeGroup]);

  useEffect(() => {
    const el = scrollRef.current;
    if (el) el.scrollTop = el.scrollHeight;
  }, [messages, streaming]);

  useEffect(() => {
    return () => {
      if (streamRef.current) streamRef.current.abort();
    };
  }, []);

  const fetchGroups = useCallback(async () => {
    try {
      const res = await fetch(cfg.baseUrl, { credentials: "include" });
      if (!res.ok) return;
      const payload = await res.json().catch(() => []);
      setGroups(Array.isArray(payload) ? payload : []);
    } catch {
      /* не критично */
    }
  }, [cfg.baseUrl]);

  useEffect(() => {
    if (!user) {
      setGroups([]);
      setActiveGroup(null);
      setActiveGroupId(null);
      return;
    }
    fetchGroups();
  }, [fetchGroups, user]);

  const openGroup = useCallback(async (id) => {
    setActiveGroupId(id);
    setError("");
    if (window.matchMedia("(max-width: 960px)").matches) {
      setChatView(true);
    }
    try {
      const res = await fetch(`${cfg.baseUrl}/${id}`, { credentials: "include" });
      if (!res.ok) {
        const payload = await res.json().catch(() => ({}));
        throw new Error(payload.message || `Сервер ответил: ${res.status}`);
      }
      const detail = await res.json();
      setActiveGroup({
        id: detail.id,
        name: detail.name,
        participants: detail.participants || [],
        messages: (detail.messages || []).map((m) => ({
          id: m.id,
          senderType: m.senderType,
          senderCharacterId: m.senderCharacterId,
          content: m.content,
          streaming: false
        }))
      });
    } catch (err) {
      setError(err.message || "Не удалось открыть чат.");
    }
  }, [cfg.baseUrl]);

  const toggleCharacter = useCallback((characterId) => {
    setSelectedIds((prev) =>
      prev.includes(characterId)
        ? prev.filter((id) => id !== characterId)
        : [...prev, characterId]
    );
  }, []);

  const openCreateManager = useCallback(() => {
    setManagerMode("create");
    setGroupName("");
    setSelectedIds([]);
    setShowManager(true);
  }, []);

  const openEditManager = useCallback(() => {
    if (!activeGroup) return;
    setManagerMode("edit");
    setGroupName(activeGroup.name);
    setSelectedIds(activeGroup.participants || []);
    setShowManager(true);
  }, [activeGroup]);

  const closeManager = useCallback(() => setShowManager(false), []);

  const saveManager = useCallback(async () => {
    if (selectedIds.length === 0) {
      setError("Выберите хотя бы одного персонажа.");
      return;
    }
    setError("");

    try {
      if (managerMode === "create") {
        const res = await fetch(cfg.baseUrl, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          credentials: "include",
          body: JSON.stringify({ name: groupName.trim(), characterIds: selectedIds })
        });
        const payload = await res.json().catch(() => ({}));
        if (!res.ok) throw new Error(payload.message || `Сервер ответил: ${res.status}`);
        setShowManager(false);
        await fetchGroups();
        openGroup(payload.id);
      } else if (activeGroup) {
        const res = await fetch(`${cfg.baseUrl}/${activeGroup.id}/participants`, {
          method: "PUT",
          headers: { "Content-Type": "application/json" },
          credentials: "include",
          body: JSON.stringify({ characterIds: selectedIds })
        });
        const payload = await res.json().catch(() => ({}));
        if (!res.ok) throw new Error(payload.message || `Сервер ответил: ${res.status}`);
        setShowManager(false);
        await fetchGroups();
        openGroup(activeGroup.id);
      }
    } catch (err) {
      setError(err.message || "Не удалось сохранить чат.");
    }
  }, [selectedIds, groupName, managerMode, activeGroup, cfg.baseUrl, fetchGroups, openGroup]);

  const doDeleteGroup = useCallback(async () => {
    if (!activeGroup) return;
    setConfirmDelete(false);
    try {
      const res = await fetch(`${cfg.baseUrl}/${activeGroup.id}`, {
        method: "DELETE",
        credentials: "include"
      });
      const payload = await res.json().catch(() => ({}));
      if (!res.ok) throw new Error(payload.message || `Сервер ответил: ${res.status}`);
      setActiveGroup(null);
      setActiveGroupId(null);
      await fetchGroups();
    } catch (err) {
      setError(err.message || "Не удалось удалить чат.");
    }
  }, [activeGroup, cfg.baseUrl, fetchGroups]);

  const handleTextareaInput = (e) => {
    setInput(e.target.value);
    if (textareaRef.current) {
      textareaRef.current.style.height = "auto";
      textareaRef.current.style.height = `${Math.min(textareaRef.current.scrollHeight, 240)}px`;
    }
  };

  const handleSend = useCallback(async () => {
    const text = input.trim();
    if (!text || !activeGroup || streaming) return;

    const userMsgId = nextGroupId();
    const userMsg = { id: userMsgId, senderType: "user", content: text };
    setActiveGroup((prev) => prev && {
      ...prev,
      messages: [...prev.messages, userMsg]
    });
    setInput("");
    if (textareaRef.current) textareaRef.current.style.height = "auto";
    setError("");
    setStreaming(true);

    const controller = new AbortController();
    streamRef.current = controller;

    const onBotStart = (characterId) => {
      setActiveGroup((prev) => prev && {
        ...prev,
        messages: [
          ...prev.messages,
          { id: nextGroupId(), senderType: "bot", senderCharacterId: characterId, content: "", streaming: true }
        ]
      });
    };

    const onText = (characterId, delta) => {
      setActiveGroup((prev) => {
        if (!prev) return prev;
        const msgs = [...prev.messages];
        for (let i = msgs.length - 1; i >= 0; i--) {
          if (msgs[i].senderType === "bot" && msgs[i].streaming && msgs[i].senderCharacterId === characterId) {
            msgs[i] = { ...msgs[i], content: msgs[i].content + delta };
            break;
          }
        }
        return { ...prev, messages: msgs };
      });
    };

    const onBotEnd = (characterId) => {
      setActiveGroup((prev) => {
        if (!prev) return prev;
        const msgs = prev.messages.map((m) =>
          m.senderType === "bot" && m.streaming && m.senderCharacterId === characterId
            ? { ...m, streaming: false }
            : m
        );
        return { ...prev, messages: msgs };
      });
    };

    const onLimit = () => {
      setActiveGroup((prev) => prev && {
        ...prev,
        messages: [...prev.messages, { id: nextGroupId(), senderType: "limit" }]
      });
    };

    const onUserMessageId = () => {
      // В групповом чате редактирование не требуется — Id игнорируем.
    };

    try {
      const res = await fetch(`${cfg.baseUrl}/${activeGroup.id}/messages/stream`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        credentials: "include",
        signal: controller.signal,
        body: JSON.stringify({ message: text })
      });

      if (res.status === 401 || res.status === 403) {
        await refreshUser();
        throw new Error("Сессия истекла. Войдите в аккаунт снова.");
      }
      if (!res.ok) {
        const payload = await res.json().catch(() => ({}));
        throw new Error(payload.message || `Сервер ответил: ${res.status}`);
      }

      await consumeGroupStream(res, { onUserMessageId, onBotStart, onText, onBotEnd, onLimit });

      streamRef.current = null;
      setStreaming(false);
    } catch (err) {
      streamRef.current = null;
      setStreaming(false);
      if (err.name !== "AbortError") {
        setError(err.message || "Не удалось получить ответ. Попробуйте ещё раз.");
        setActiveGroup((prev) => prev && {
          ...prev,
          messages: prev.messages.filter((m) => !(m.streaming && m.senderType === "bot"))
        });
      }
    }
  }, [input, activeGroup, streaming, cfg.baseUrl, refreshUser]);

  const handleKeyDown = useCallback(
    (e) => {
      if (e.key === "Enter" && !e.shiftKey) {
        e.preventDefault();
        handleSend();
      }
    },
    [handleSend]
  );

  const goBack = useCallback(() => {
    navigate(isAdminMode ? "/admin" : "/bots");
  }, [navigate, isAdminMode]);

  const isDesktopView = () => window.matchMedia("(min-width: 961px)").matches;
  const isMobileView = () => !isDesktopView();

  const showChatPane = (isDesktopView() || chatView) && activeGroup;
  const showListPane = isDesktopView() || !chatView;
  const mobileDialogOpen = isMobileView() && showChatPane;

  const goBackToList = useCallback(() => setChatView(false), []);

  useEffect(() => {
    const body = document.body;
    if (mobileDialogOpen) {
      body.classList.add("no-scroll");
    } else {
      body.classList.remove("no-scroll");
    }
    return () => body.classList.remove("no-scroll");
  }, [mobileDialogOpen]);

  if (loading) {
    return (
      <section className="ai-chat-page ai-auth-gate panel" aria-busy="true">
        <div className="ai-auth-gate-icon"><Bot size={34} /></div>
        <h2>Проверяем сессию…</h2>
        <p className="muted">Подождите немного.</p>
      </section>
    );
  }

  if (!user || !user.isEmailConfirmed) {
    const openAuth = (authMode) =>
      window.dispatchEvent(new CustomEvent("bronytv:open-auth", { detail: { mode: authMode } }));

    return (
      <section className="ai-chat-page ai-auth-gate panel">
        <div className="ai-auth-gate-icon"><LockKeyhole size={34} /></div>
        <div>
          <h2>Войдите, чтобы общаться с ИИ-ботами</h2>
          <p className="muted">Групповые чаты доступны только пользователям с подтверждённым email.</p>
        </div>
        <div className="ai-auth-gate-actions">
          <button type="button" className="primary-btn" onClick={() => openAuth("signin")}>
            <LogIn size={17} /> Войти
          </button>
          <button type="button" className="secondary-btn" onClick={() => openAuth("signup")}>
            <UserPlus size={17} /> Зарегистрироваться
          </button>
        </div>
      </section>
    );
  }

  const selectedCount = selectedIds.length;
  const warnManyBots = selectedCount >= GROUP_CHAT_WARN_THRESHOLD;

  return (
    <section className={`ai-chat-page panel${isMobileView() && showChatPane ? " ai-chat-page--chat" : ""}`}>
      <div className="ai-chat-header gc-header">
        <div className="ai-chat-title">
          <button type="button" className="ai-back-btn" onClick={goBack} aria-label="Назад">
            <ArrowLeft size={18} />
          </button>
          <span className="ai-chat-title-icon"><Users size={22} /></span>
          <div>
            <h2>Групповые чаты</h2>
            <p className="muted">Общайся сразу с несколькими пони — каждый бот ответит на твоё сообщение.</p>
          </div>
        </div>
        <button type="button" className="primary-btn gc-create-btn" onClick={openCreateManager}>
          <Plus size={17} /> Создать чат
        </button>
      </div>

      <div className={`ai-messenger ${isMobileView() ? "ai-messenger--mobile" : ""}`}>
        {showListPane && (
        <div className="ai-bot-list-pane">
          <div className="ai-bot-list-head">
            <span className="ai-bot-list-title">Ваши чаты</span>
          </div>
          <div className="ai-bot-list">
            {groups.length === 0 && <p className="ai-group-empty">Пока нет групповых чатов.</p>}
            {groups.map((g) => {
              const isActive = g.id === activeGroupId;
              return (
                <button
                  key={g.id}
                  type="button"
                  className={`ai-bot-card${isActive ? " is-active" : ""}`}
                  onClick={() => openGroup(g.id)}
                >
                  <span className="ai-group-avatar"><Users size={20} /></span>
                  <span className="ai-bot-card-info">
                    <span className="ai-bot-card-name">{g.name}</span>
                    <span className="ai-bot-card-race">{g.participantCount} персонажей</span>
                  </span>
                  {isActive && <ChevronRight size={16} className="ai-bot-card-arrow" />}
                </button>
              );
            })}
          </div>
        </div>
        )}

        {showChatPane ? (
          <div className="ai-chat-pane">
            <div className="ai-chat-head">
              <div className="ai-chat-head-main">
                {isMobileView() && (
                  <button type="button" className="ai-back-btn" onClick={goBackToList} aria-label="Назад к списку">
                    <ArrowLeft size={18} />
                  </button>
                )}
                <span className="ai-group-avatar"><Users size={18} /></span>
                <div className="ai-chat-head-info">
                  <span className="ai-chat-head-name">{activeGroup.name}</span>
                  <span className="ai-chat-head-status">{activeGroup.participants.length} ботов в чате</span>
                </div>
              </div>
              <div className="ai-chat-head-actions">
                <button type="button" className="ai-chat-head-action" onClick={openEditManager} aria-label="Состав чата" title="Состав чата">
                  <Pencil size={18} />
                </button>
                <button type="button" className="ai-chat-head-action" onClick={() => setConfirmDelete(true)} aria-label="Удалить чат" title="Удалить чат">
                  <Trash2 size={18} />
                </button>
              </div>
            </div>

            {error && <div className="ai-chat-error">{error}</div>}

            <div className="ai-messages gc-messages" ref={scrollRef}>
              {messages.length === 0 && (
                <div className="ai-chat-empty">
                  <span className="ai-group-avatar ai-group-avatar--large"><Users size={34} /></span>
                  <p>Напиши сообщение — и все пони ответят по очереди.</p>
                </div>
              )}

              {messages.map((m) => {
                if (m.senderType === "limit") {
                  return (
                    <div key={m.id} className="ai-msg ai-msg--bot gc-msg">
                      <div className="ai-msg-limit-banner">
                        <div className="ai-msg-limit-icon"><LockKeyhole size={20} /></div>
                        <div className="ai-msg-limit-body">
                          <div className="ai-msg-limit-title">Лимит общения исчерпан</div>
                          <div className="ai-msg-limit-text">Боты больше не отвечают до сброса лимита.</div>
                        </div>
                      </div>
                    </div>
                  );
                }

                if (m.senderType === "user") {
                  return (
                    <div key={m.id} className="ai-msg ai-msg--user gc-msg">
                      <div className="ai-msg-user-body">
                        <div className="ai-bubble"><span className="ai-bubble-text">{m.content}</span></div>
                      </div>
                    </div>
                  );
                }

                const bot = findBot(m.senderCharacterId);
                return (
                  <div key={m.id} className="ai-msg ai-msg--bot gc-msg">
                    <BotAvatar bot={bot} size={32} />
                    <div className="gc-msg-bot-body">
                      <span className="gc-msg-bot-name">{bot?.name || m.senderCharacterId}</span>
                      <div className="ai-bubble">
                        {m.streaming && !m.content ? (
                          <span className="ai-typing"><span /><span /><span /></span>
                        ) : (
                          <span className="ai-bubble-text">{m.content}</span>
                        )}
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>

            <div className="ai-composer">
              <textarea
                className="ai-composer-input"
                ref={textareaRef}
                value={input}
                onChange={handleTextareaInput}
                onKeyDown={handleKeyDown}
                placeholder="Сообщение..."
                disabled={streaming}
                rows={1}
                maxLength={2000}
              />
              <button
                type="button"
                className="ai-composer-send"
                onClick={handleSend}
                disabled={streaming || !input.trim()}
                aria-label="Отправить"
              >
                <Send size={18} />
              </button>
            </div>
          </div>
        ) : isDesktopView() && !activeGroup ? (
          <div className="ai-chat-pane ai-chat-placeholder">
            <div className="ai-chat-placeholder-inner">
              <span className="ai-chat-placeholder-icon"><Users size={56} /></span>
              <h3>Выбери или создай групповой чат</h3>
              <p className="muted">Добавь несколько пони в один чат и общайся со всеми сразу.</p>
            </div>
          </div>
        ) : null}
      </div>

      {showManager && (
        <div className="ai-premium-overlay" onClick={closeManager}>
          <div className="ai-premium-modal ai-group-manager" role="dialog" aria-modal="true" onClick={(e) => e.stopPropagation()}>
            <button type="button" className="ai-premium-close" onClick={closeManager} aria-label="Закрыть" title="Закрыть">
              <X size={18} />
            </button>
            <h3>{managerMode === "create" ? "Новый групповой чат" : "Состав чата"}</h3>

            <label className="news-field">
              <span>Название (необязательно)</span>
              <input
                type="text"
                value={groupName}
                onChange={(e) => setGroupName(e.target.value)}
                placeholder="Групповой чат"
                maxLength={200}
              />
            </label>

            <div className="ai-group-picker-label">Персонажи</div>
            <div className="ai-group-picker">
              {BOT_CATALOG.map((bot) => {
                const checked = selectedIds.includes(bot.id);
                return (
                  <button
                    key={bot.id}
                    type="button"
                    className={`ai-group-pick${checked ? " is-checked" : ""}`}
                    onClick={() => toggleCharacter(bot.id)}
                  >
                    <BotAvatar bot={bot} size={36} />
                    <span className="ai-group-pick-name">{bot.name}</span>
                    {checked && <Check size={16} className="ai-group-pick-check" />}
                  </button>
                );
              })}
            </div>

            {warnManyBots && (
              <p className="ai-group-warn">
                Чем больше ботов в чате, тем дольше они отвечают на каждое сообщение.
              </p>
            )}

            <div className="ai-confirm-actions">
              <button type="button" className="ai-confirm-btn ai-confirm-btn--cancel" onClick={closeManager}>
                <X size={16} /> Отмена
              </button>
              <button
                type="button"
                className="ai-confirm-btn ai-confirm-btn--danger"
                onClick={saveManager}
                disabled={selectedCount === 0}
              >
                <Check size={16} /> {managerMode === "create" ? "Создать" : "Сохранить"}
              </button>
            </div>
          </div>
        </div>
      )}

      {confirmDelete && activeGroup && (
        <div className="ai-confirm-overlay" onClick={() => setConfirmDelete(false)}>
          <div className="ai-confirm-modal" role="dialog" aria-modal="true" onClick={(e) => e.stopPropagation()}>
            <button type="button" className="ai-confirm-close" onClick={() => setConfirmDelete(false)} aria-label="Закрыть" title="Закрыть">
              <X size={18} />
            </button>
            <div className="ai-confirm-icon"><Trash2 size={22} /></div>
            <h3>Удалить групповой чат?</h3>
            <p className="muted">Переписка и состав чата будут удалены безвозвратно.</p>
            <div className="ai-confirm-actions">
              <button type="button" className="ai-confirm-btn ai-confirm-btn--cancel" onClick={() => setConfirmDelete(false)}>
                <X size={16} /> Отмена
              </button>
              <button type="button" className="ai-confirm-btn ai-confirm-btn--danger" onClick={doDeleteGroup}>
                <Check size={16} /> Удалить
              </button>
            </div>
          </div>
        </div>
      )}
    </section>
  );
}

export default GroupChatPage;
