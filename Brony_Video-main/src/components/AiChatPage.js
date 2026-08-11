import React, { useCallback, useEffect, useRef, useState } from "react";
import { ArrowLeft, Bot, ChevronRight, PanelLeftClose, PanelLeftOpen, Send, Star } from "lucide-react";

// Метаданные персонажей-ботов. id совпадает с characterId в микросервисе AiBronyTV,
// avatar — имя файла в public/assets/avatars.
const BOT_CATALOG = [
  {
    id: "rainbow",
    name: "Рэйнбоу Дэш",
    race: "Пегас",
    tagline: "Самая быстрая и дерзкая пегаска Понивилля.",
    avatar: "rainbow dash.jpe",
    colour: "#16a6e8"
  },
  {
    id: "twilight",
    name: "Твайлайт Спаркл",
    race: "Аликорн",
    tagline: "Принцесса дружбы и учёный-книжный червь.",
    avatar: "twilight sparkle.jpe",
    colour: "#6a55d8"
  },
  {
    id: "trixie",
    name: "Трикси",
    race: "Единорог",
    tagline: "Великая и Могущественная иллюзионистка.",
    avatar: "trixie.jpe",
    colour: "#2f8fd6"
  },
  {
    id: "pinki",
    name: "Пинки Пай",
    race: "Земная пони",
    tagline: "Неутомимая королева вечеринок и кексов.",
    avatar: "pinki.jpe",
    colour: "#f2509a"
  },
  {
    id: "fluttershy",
    name: "Флаттершай",
    race: "Пегас",
    tagline: "Добрая и робкая ценительница животных.",
    avatar: "fluttershy.jpe",
    colour: "#f7c96a"
  },
  {
    id: "rarity",
    name: "Рарити",
    race: "Единорог",
    tagline: "Изысканный модельер из бутика «Карусель».",
    avatar: "rarity.jpe",
    colour: "#e8b3d4"
  },
  {
    id: "applejack",
    name: "Эпплджек",
    race: "Земная пони",
    tagline: "Надёжная и честная пони с фермы «Сладкое Яблочко».",
    avatar: "applejack.jpe",
    colour: "#f0a23b"
  },
  {
    id: "starlight",
    name: "Старлайт Глиммер",
    race: "Единорог",
    tagline: "Бывшая злодейка, а теперь ученица Искорки.",
    avatar: "Starlight.jpe",
    colour: "#e25a9a"
  },
  {
    id: "sunset",
    name: "Сансет Шиммер",
    race: "Единорог",
    tagline: "Крутая рок-звезда из мира людей.",
    avatar: "Sunset.jpe",
    colour: "#ee7f34"
  },
  {
    id: "celestia",
    name: "Принцесса Селестия",
    race: "Аликорн",
    tagline: "Мудрая правительница Эквестрии, поднимающая солнце.",
    avatar: "celestia.jpe",
    colour: "#f3c6d6"
  },
  {
    id: "luna",
    name: "Принцесса Луна",
    race: "Аликорн",
    tagline: "Повелительница снов и ночи, хранительница сновидений.",
    avatar: "luna.jpe",
    colour: "#4a3f9e"
  },
  {
    id: "cadance",
    name: "Принцесса Каденс",
    race: "Аликорн",
    tagline: "Аликорн любви, правительница Кристальной Империи.",
    avatar: "cadence.jpe",
    colour: "#f3a8dd"
  }
];

const SESSION_KEY = "bronytv-ai-session";
const MESSAGES_KEY = "bronytv-ai-messages";

const buildAssetUrl = (avatar) => {
  const base = process.env.PUBLIC_URL || "";
  return `${base}/assets/avatars/${encodeURIComponent(avatar)}`;
};

const readSession = () => {
  try {
    return localStorage.getItem(SESSION_KEY) || "";
  } catch {
    return "";
  }
};

const ensureSession = () => {
  let sid = readSession();
  if (!sid) {
    sid = `web-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
    try {
      localStorage.setItem(SESSION_KEY, sid);
    } catch {
      /* ignore */
    }
  }
  return sid;
};

const loadStoredMessages = (characterId) => {
  try {
    const raw = localStorage.getItem(MESSAGES_KEY);
    if (!raw) return [];
    const byChar = JSON.parse(raw);
    return (byChar[characterId] || []).slice(-60);
  } catch {
    return [];
  }
};

const storeMessages = (characterId, messages) => {
  try {
    const raw = localStorage.getItem(MESSAGES_KEY);
    const byChar = raw ? JSON.parse(raw) : {};
    byChar[characterId] = messages.slice(-60);
    localStorage.setItem(MESSAGES_KEY, JSON.stringify(byChar));
  } catch {
    /* ignore storage failures */
  }
};

let msgSeq = 0;
const nextId = () => `m-${Date.now()}-${msgSeq++}`;

const SESSION_META_KEY = "bronytv-ai-session-meta";

const loadSessionMeta = () => {
  try {
    const raw = localStorage.getItem(SESSION_META_KEY);
    return raw ? JSON.parse(raw) : {};
  } catch {
    return {};
  }
};

const saveSessionMeta = (meta) => {
  try {
    localStorage.setItem(SESSION_META_KEY, JSON.stringify(meta));
  } catch {
    /* ignore */
  }
};

function BotAvatar({ bot, size = 56 }) {
  const fallback = (bot?.name || "Бот").slice(0, 1).toUpperCase();
  return (
    <div
      className="ai-bot-avatar"
      style={{
        width: size,
        height: size,
        "--bot-accent": bot?.colour || "var(--accent)",
        fontSize: size * 0.38
      }}
    >
      {bot?.avatar ? (
        <img src={buildAssetUrl(bot.avatar)} alt="" draggable={false} />
      ) : (
        <span>{fallback}</span>
      )}
    </div>
  );
}

function LimitBanner({ message }) {
  return (
    <div className="ai-msg ai-msg--bot">
      <div className="ai-msg-limit-banner">
        <div className="ai-msg-limit-icon">
          <Star size={20} />
        </div>
        <div className="ai-msg-limit-body">
          <div className="ai-msg-limit-title">Сегодняшний лимит общения исчерпан</div>
          <div className="ai-msg-limit-text">{message}</div>
        </div>
      </div>
    </div>
  );
}

function AiChatPage() {
  const [bots] = useState(BOT_CATALOG);
  const [activeBotId, setActiveBotId] = useState(null);
  const [messages, setMessages] = useState([]);
  const [input, setInput] = useState("");
  const [streaming, setStreaming] = useState(false);
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);
  const [chatView, setChatView] = useState(false); // mobile: list vs chat
  const [error, setError] = useState("");
  const scrollRef = useRef(null);
  const streamRef = useRef(null);

  const activeBot = bots.find((b) => b.id === activeBotId) || null;

  useEffect(() => {
    if (activeBotId) {
      setMessages(loadStoredMessages(activeBotId));
    } else {
      setMessages([]);
    }
  }, [activeBotId]);

  useEffect(() => {
    const el = scrollRef.current;
    if (el) el.scrollTop = el.scrollHeight;
  }, [messages, streaming]);

  useEffect(() => {
    return () => {
      if (streamRef.current) streamRef.current.abort();
    };
  }, []);

  const selectBot = useCallback(
    (botId) => {
      setActiveBotId(botId);
      setError("");
      setChatView(true);
      if (window.matchMedia("(max-width: 960px)").matches) {
        setSidebarCollapsed(true);
      }
    },
    []
  );

  const goBackToList = useCallback(() => {
    setChatView(false);
    setActiveBotId(null);
  }, []);

  const toggleCollapse = useCallback(() => setSidebarCollapsed((v) => !v), []);

  const handleSend = useCallback(async () => {
    const text = input.trim();
    if (!text || !activeBotId || streaming) return;

    const sessionId = ensureSession();
    const userMsg = { id: nextId(), role: "user", text, limit: false };
    const assistantMsg = { id: nextId(), role: "assistant", text: "", limit: false, streaming: true };

    const nextMessages = [...messages, userMsg, assistantMsg];
    setMessages(nextMessages);
    setInput("");
    setError("");

    // Немного метаданных сессии для статистики (не критично).
    const meta = loadSessionMeta();
    meta[activeBotId] = { lastUsed: Date.now(), updatedAt: Date.now() };
    saveSessionMeta(meta);

    try {
      const res = await fetch("/api/chat/stream", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        credentials: "include",
        body: JSON.stringify({ sessionId, characterId: activeBotId, message: text })
      });

      if (!res.ok) {
        throw new Error(`Сервер ответил: ${res.status}`);
      }

      const reader = res.body.getReader();
      const decoder = new TextDecoder();
      const alive = { value: true };

      const controller = new AbortController();
      streamRef.current = controller;

      let acc = "";
      let finished = false;

      const pushChunk = (delta) => {
        setMessages((prev) => {
          const idx = prev.findIndex((m) => m.id === assistantMsg.id);
          if (idx === -1) return prev;
          const copy = [...prev];
          copy[idx] = { ...copy[idx], text: copy[idx].text + delta };
          return copy;
        });
      };

      const finalizeAssistant = (limitMsg, limit) => {
        setMessages((prev) => {
          const idx = prev.findIndex((m) => m.id === assistantMsg.id);
          if (idx === -1) return prev;
          const copy = [...prev];
          if (limit) {
            copy[idx] = { ...copy[idx], text: limitMsg, limit: true, streaming: false };
          } else {
            copy[idx] = { ...copy[idx], streaming: false };
          }
          return copy;
        });
      };

      while (alive.value) {
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
          try {
            const parsed = JSON.parse(data);
            if (parsed && typeof parsed.text === "string") {
              if (parsed.limit === true) {
                finalizeAssistant(parsed.text, true);
              } else {
                pushChunk(parsed.text);
              }
            }
          } catch {
            /* ignore malformed frames */
          }
        }
        if (finished) break;
      }

      streamRef.current = null;
      // Снимаем streaming-флаг с последнего ассистентского сообщения в любом случае.
      finalizeAssistant("", false);
      setStreaming(false);
      setMessages((prev) => {
        storeMessages(activeBotId, prev);
        return prev;
      });
    } catch (err) {
      streamRef.current = null;
      setStreaming(false);
      if (err.name !== "AbortError") {
        setError("Не удалось получить ответ. Попробуйте ещё раз.");
        setMessages((prev) => prev.filter((m) => m.id !== assistantMsg.id));
      }
    }
  }, [activeBotId, input, messages, streaming]);

  const handleKeyDown = useCallback(
    (e) => {
      if (e.key === "Enter" && !e.shiftKey) {
        e.preventDefault();
        handleSend();
      }
    },
    [handleSend]
  );

  const isDesktopView = () => window.matchMedia("(min-width: 961px)").matches;
  const isMobileView = () => !isDesktopView();

  const showChatPane = (isDesktopView() || chatView) && activeBotId;
  const showListPane = isDesktopView() || !chatView;

  return (
    <section className="panel ai-chat-panel">
      <div className="ai-chat-header">
        <div className="ai-chat-title">
          <span className="ai-chat-title-icon">
            <Bot size={22} />
          </span>
          <div>
            <h2>ИИ Боты</h2>
            <p className="muted">Поболтай с любимыми пони. У каждого персонажа свой характер и настроение.</p>
          </div>
        </div>
      </div>

      <div
        className={`ai-messenger ${sidebarCollapsed && isDesktopView() ? "ai-messenger--collapsed" : ""} ${
          isMobileView() ? "ai-messenger--mobile" : ""
        }`}
      >
        {showListPane && (
          <div className="ai-bot-list-pane">
            <div className="ai-bot-list-head">
              <span className="ai-bot-list-title">Персонажи</span>
              {isDesktopView() && (
                <button
                  type="button"
                  className="ai-collapse-btn"
                  onClick={toggleCollapse}
                  aria-label={sidebarCollapsed ? "Развернуть список" : "Свернуть список"}
                  title={sidebarCollapsed ? "Развернуть список" : "Свернуть список"}
                >
                  {sidebarCollapsed ? <PanelLeftOpen size={18} /> : <PanelLeftClose size={18} />}
                </button>
              )}
            </div>

            <div className="ai-bot-list">
              {bots.map((bot) => {
                const isActive = bot.id === activeBotId;
                return (
                  <button
                    key={bot.id}
                    type="button"
                    className={`ai-bot-card${isActive ? " is-active" : ""}`}
                    onClick={() => selectBot(bot.id)}
                  >
                    <BotAvatar bot={bot} size={44} />
                    <span className="ai-bot-card-info">
                      <span className="ai-bot-card-name">{bot.name}</span>
                      <span className="ai-bot-card-race">{bot.race}</span>
                      <span className="ai-bot-card-tagline">{bot.tagline}</span>
                    </span>
                    {isActive && <ChevronRight size={16} className="ai-bot-card-arrow" />}
                  </button>
                );
              })}
            </div>
          </div>
        )}

        {showChatPane && activeBot && (
          <div className="ai-chat-pane">
            <div className="ai-chat-head">
              {isMobileView() && (
                <button
                  type="button"
                  className="ai-back-btn"
                  onClick={goBackToList}
                  aria-label="Назад к списку"
                >
                  <ArrowLeft size={18} />
                  <span>Список</span>
                </button>
              )}
              <BotAvatar bot={activeBot} size={44} />
              <div className="ai-chat-head-info">
                <span className="ai-chat-head-name">{activeBot.name}</span>
                <span className="ai-chat-head-status">онлайн · менеджер настроения</span>
              </div>
            </div>

            {error && <div className="ai-chat-error">{error}</div>}

            <div className="ai-messages" ref={scrollRef}>
              {messages.length === 0 ? (
                <div className="ai-chat-empty">
                  <BotAvatar bot={activeBot} size={72} />
                  <p>
                    Привет! Я <strong>{activeBot.name}</strong>.
                    <br />
                    Расскажи, как дела, или задай любой вопрос.
                  </p>
                </div>
              ) : (
                messages.map((m) => {
                  if (m.limit) {
                    return <LimitBanner key={m.id} message={m.text} />;
                  }
                  return (
                    <div key={m.id} className={`ai-msg ai-msg--${m.role}`}>
                      {m.role === "assistant" && <BotAvatar bot={activeBot} size={32} />}
                      <div className="ai-bubble">
                        {m.role === "assistant" && m.streaming && !m.text ? (
                          <span className="ai-typing">
                            <span />
                            <span />
                            <span />
                          </span>
                        ) : (
                          <span className="ai-bubble-text">{m.text}</span>
                        )}
                      </div>
                    </div>
                  );
                })
              )}
            </div>

            <div className="ai-composer">
              <textarea
                className="ai-composer-input"
                value={input}
                onChange={(e) => setInput(e.target.value)}
                onKeyDown={handleKeyDown}
                placeholder={
                  streaming ? "Рэйнбоу думает…" : `Напиши ${activeBot.name}… (Enter — отправить)`
                }
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
        )}

        {isDesktopView() && !activeBotId && (
          <div className="ai-chat-pane ai-chat-placeholder">
            <div className="ai-chat-placeholder-inner">
              <span className="ai-chat-placeholder-icon">
                <Bot size={56} />
              </span>
              <h3>Выбери персонажа</h3>
              <p className="muted">
                Нажми на любую пони слева, чтобы начать общение. Каждый бот живёт в своём характере.
              </p>
            </div>
          </div>
        )}

        {!showListPane && !showChatPane && (
          <div className="ai-chat-pane ai-chat-placeholder">
            <div className="ai-chat-placeholder-inner">
              <span className="ai-chat-placeholder-icon">
                <Bot size={56} />
              </span>
              <h3>Выбери персонажа</h3>
            </div>
          </div>
        )}
      </div>
    </section>
  );
}

export default AiChatPage;
