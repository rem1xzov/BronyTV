import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";

import {
  ArrowLeft,
  BookOpen,
  Bot,
    Check,
  ChevronRight,
  LockKeyhole,
    LogIn,
  PanelLeftClose,
  PanelLeftOpen,
  Plus,
  Send,
  Star,
  Trash2,
  UserPlus,
  X
} from "lucide-react";
import { useAuth } from "../auth/AuthContext";

// Метаданные персонажей-ботов. id совпадает с characterId в микросервисе AiBronyTV,
// avatar — имя файла в public/assets/avatars.
const BOT_CATALOG = [
  {
    id: "narrator",
    name: "Рассказчик",
    race: "Game Master",
    tagline: "Опиши своего персонажа и отправься в приключение по Эквестрии.",
    colour: "#8e5af0"
  },
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
  },
  {
    id: "applebloom",
    name: "Эппл Блум",
    race: "Земная пони",
    tagline: "Младшая сестра Эпплджек и заядлая открывательница меток.",
    avatar: "applebloom.jpe",
    colour: "#f0782b"
  },
  {
    id: "sweetiebelle",
    name: "Свити Белль",
    race: "Единорог",
    tagline: "Беззаботная сестра Рарити, всегда готовая спеть.",
    avatar: "sweetiebelle .jpe",
    colour: "#f7d3e0"
  },
  {
    id: "scootaloo",
    name: "Скуталу",
    race: "Пегас",
    tagline: "Трескун и обожательница Рэйнбоу Дэш.",
    avatar: "scootaloo.jpe",
    colour: "#e8796a"
  },
  {
    id: "derpy",
    name: "Дерпи Хувз",
    race: "Пегас",
    tagline: "Косоглазая, но добрейшая почтальонка Понивилля.",
    avatar: "Derpyhooves.jpe",
    colour: "#9aa7b8"
  },
  {
    id: "discord",
    name: "Дискорд",
    race: "Дух хаоса",
    tagline: "Властелин хаоса, обожающий ломать правила реальности.",
    avatar: "discord.jpe",
    colour: "#b18cd9"
  },
  {
    id: "cozyglow",
    name: "Кози Глоу",
    race: "Пегас",
    tagline: "Милая и прилежная ученица, в душе — гениальная интриганка.",
    avatar: "cozyglow.jpe",
    colour: "#f5a8b8"
  },
  {
    id: "octavia",
    name: "Октавия",
    race: "Земная пони",
    tagline: "Искушённая виолончелистка с консервативным вкусом.",
    avatar: "octavia.jpe",
    colour: "#7a7f8f"
  },
  {
    id: "djpon3",
    name: "Ди-Джей Пон-3",
    race: "Единорог",
    tagline: "Легендарная техно-дэнс диджейка Кантерлота.",
    avatar: "djpon3.jpe",
    colour: "#8e7cf0"
  },
  {
    id: "shiningarmor",
    name: "Шайнинг Армор",
    race: "Единорог",
    tagline: "Капитан гвардии Кристальной Империи и брат Твайлайт.",
    avatar: "shiningarmor.jpe",
    colour: "#5aa7d6"
  }
];

// Конфигурация режимов: публичный (user) и админский (admin). Админский режим использует
// отдельные эндпоинты и отдельные ключи localStorage, чтобы история не пересекалась с публичной.
const MODE_CONFIG = {
  user: {
    sessionKey: "bronytv-ai-session",
    messagesKey: "bronytv-ai-messages",
    metaKey: "bronytv-ai-session-meta",
    streamUrl: "/api/chat/stream",
    historyUrl: "/api/chat/history",
    premiumStatusUrl: "/api/bots/premium-status",
    activateUrl: "/api/bots/activate"
  },
  admin: {
    sessionKey: "bronytv-ai-admin-session",
    messagesKey: "bronytv-ai-admin-messages",
    metaKey: "bronytv-ai-admin-session-meta",
    streamUrl: "/api/admin/chat/stream",
    historyUrl: "/api/admin/chat/history",
    premiumStatusUrl: null,
    activateUrl: null
  }
};

const buildAssetUrl = (avatar) => {
  const base = process.env.PUBLIC_URL || "";
  return `${base}/assets/avatars/${encodeURIComponent(avatar)}`;
};

const readSession = (sessionKey) => {
  try {
    return localStorage.getItem(sessionKey) || "";
  } catch {
    return "";
  }
};

const ensureSession = (sessionKey) => {
  let sid = readSession(sessionKey);
  if (!sid) {
    sid = `web-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
    try {
      localStorage.setItem(sessionKey, sid);
    } catch {
      /* ignore */
    }
  }
  return sid;
};

const loadStoredMessages = (messagesKey, characterId) => {
  try {
    const raw = localStorage.getItem(messagesKey);
    if (!raw) return [];
    const byChar = JSON.parse(raw);
    return (byChar[characterId] || []).slice(-60);
  } catch {
    return [];
  }
};

const storeMessages = (messagesKey, characterId, messages) => {
  try {
    const raw = localStorage.getItem(messagesKey);
    const byChar = raw ? JSON.parse(raw) : {};
    byChar[characterId] = messages.slice(-60);
    localStorage.setItem(messagesKey, JSON.stringify(byChar));
  } catch {
    /* ignore storage failures */
  }
};

let msgSeq = 0;
const nextId = () => `m-${Date.now()}-${msgSeq++}`;

const loadSessionMeta = (metaKey) => {
  try {
    const raw = localStorage.getItem(metaKey);
    return raw ? JSON.parse(raw) : {};
  } catch {
    return {};
  }
};

const saveSessionMeta = (metaKey, meta) => {
  try {
    localStorage.setItem(metaKey, JSON.stringify(meta));
  } catch {
    /* ignore */
  }
};

const formatPremiumDate = (iso) => {
  try {
    const d = new Date(iso);
    if (Number.isNaN(d.getTime())) return "";
    return d.toLocaleDateString("ru-RU", {
      day: "numeric",
      month: "long",
      year: "numeric"
    });
  } catch {
    return "";
  }
};

function BotAvatar({ bot, size = 56 }) {
  const fallback = (bot?.name || "Бот").slice(0, 1).toUpperCase();
  const baseStyle = {
    width: size,
    height: size,
    "--bot-accent": bot?.colour || "var(--accent)",
    fontSize: size * 0.38
  };
  if (bot?.id === "narrator") {
    return (
      <div className="ai-bot-avatar ai-bot-avatar--icon" style={baseStyle}>
        <BookOpen size={size * 0.5} />
      </div>
    );
  }
  return (
    <div className="ai-bot-avatar" style={baseStyle}>
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

function AiChatPage({ mode = "user" }) {
  const cfg = MODE_CONFIG[mode] || MODE_CONFIG.user;
  const isAdminMode = mode === "admin";
  const { user, loading, refreshUser } = useAuth();
  const [bots] = useState(BOT_CATALOG);
  const [activeBotId, setActiveBotId] = useState(null);
  const [messages, setMessages] = useState([]);
  const [input, setInput] = useState("");
  const [streaming, setStreaming] = useState(false);
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);
  const [chatView, setChatView] = useState(false); // mobile: list vs chat
    const [error, setError] = useState("");
  const [confirmClear, setConfirmClear] = useState(false);
  const [premiumKey, setPremiumKey] = useState("");
  const [premiumMsg, setPremiumMsg] = useState("");
  const [premiumError, setPremiumError] = useState("");
    const [premiumLoading, setPremiumLoading] = useState(false);
    const [showPremiumModal, setShowPremiumModal] = useState(false);
  const [premiumStatus, setPremiumStatus] = useState(null);
  const [showPremiumInfoModal, setShowPremiumInfoModal] = useState(false);
  const [characterName, setCharacterName] = useState("");
  const [characterAge, setCharacterAge] = useState("");
  const [characterGender, setCharacterGender] = useState("");
  const [characterRace, setCharacterRace] = useState("");
  const [characterCutieMark, setCharacterCutieMark] = useState("");
    const [characterDescription, setCharacterDescription] = useState("");
    const [characterPlot, setCharacterPlot] = useState("");
  const scrollRef = useRef(null);
  const streamRef = useRef(null);
  const textareaRef = useRef(null);

    const activeBot = bots.find((b) => b.id === activeBotId) || null;
  const isNarratorSetup = activeBot?.id === 'narrator' && messages.length === 0;

  useEffect(() => {
    if (activeBotId) {
      setMessages(loadStoredMessages(cfg.messagesKey, activeBotId));
    } else {
      setMessages([]);
    }
  }, [activeBotId, cfg.messagesKey]);

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

    const doClearHistory = useCallback(async () => {
    if (!activeBotId) return;
    const sessionId = ensureSession(cfg.sessionKey);
    setConfirmClear(false);
    // Сначала реально удаляем историю на бэкенде, чтобы бот забыл прошлый разговор.
    try {
      const res = await fetch(
        `${cfg.historyUrl}?sessionId=${encodeURIComponent(sessionId)}&characterId=${encodeURIComponent(activeBotId)}`,
        { method: "DELETE", credentials: "include" }
      );
      if (!res.ok) {
        const payload = await res.json().catch(() => ({}));
        throw new Error(payload.message || `Сервер ответил: ${res.status}`);
      }
    } catch (err) {
      setError(err.message || "Не удалось очистить историю на сервере.");
      return;
    }
    // Только при успешном ответе сервера чистим локальный state.
    try {
      const raw = localStorage.getItem(cfg.messagesKey);
      const byChar = raw ? JSON.parse(raw) : {};
      delete byChar[activeBotId];
      localStorage.setItem(cfg.messagesKey, JSON.stringify(byChar));
    } catch {
      /* ignore storage failures */
    }
    setMessages([]);
    setError("");
  }, [activeBotId, cfg.messagesKey]);

  const confirmClearHistory = useCallback(() => {
    if (!activeBotId) return;
    setConfirmClear(true);
  }, [activeBotId]);

    const cancelClearHistory = useCallback(() => setConfirmClear(false), []);

  const fetchPremiumStatus = useCallback(async () => {
    try {
      const sessionId = ensureSession(cfg.sessionKey);
      const res = await fetch(`${cfg.premiumStatusUrl}?sessionId=${encodeURIComponent(sessionId)}`, {
        credentials: "include"
      });
      if (!res.ok) return;
      const payload = await res.json().catch(() => ({}));
      setPremiumStatus(payload);
    } catch {
      /* не критично — кнопка останется на плюсе */
    }
  }, []);

    // Перезапрашиваем статус премиума при смене аккаунта, чтобы статус от прошлого
  // пользователя не «перетекал» на нового в одной вкладке браузера.
  useEffect(() => {
    if (!user || isAdminMode) {
      setPremiumStatus(null);
      return;
    }
    fetchPremiumStatus();
  }, [fetchPremiumStatus, user, isAdminMode]);

  const handleActivate = useCallback(async () => {
    const key = premiumKey.trim();
    if (!key || premiumLoading) return;
    setPremiumError("");
    setPremiumMsg("");
    setPremiumLoading(true);

    try {
      const sessionId = ensureSession(cfg.sessionKey);
      const res = await fetch(cfg.activateUrl, {
                method: "POST",
        headers: { "Content-Type": "application/json" },
        credentials: "include",
        body: JSON.stringify({ key, sessionId })
      });

      const payload = await res.json().catch(() => ({}));
      if (!res.ok) {
        throw new Error(payload.message || `Сервер ответил: ${res.status}`);
      }
            setPremiumMsg(payload.message || "Премиум активирован! Безлимит всего за 50 рублей в месяц.");
      setPremiumKey("");
      setShowPremiumModal(false);
      fetchPremiumStatus();
    } catch (err) {
      setPremiumError(err.message || "Не удалось активировать ключ.");
        } finally {
      setPremiumLoading(false);
    }
  }, [premiumKey, premiumLoading, fetchPremiumStatus]);

    const handleTextareaInput = (e) => {
    setInput(e.target.value);
    if (textareaRef.current) {
      textareaRef.current.style.height = "auto";
      textareaRef.current.style.height = `${Math.min(textareaRef.current.scrollHeight, 240)}px`; // ~10 строк
    }
  };

    const handleSend = useCallback(async (overrideText) => {
    const text = (overrideText ?? input).trim();
    if (!text || !activeBotId || streaming) return;
    if (!user || !user.isEmailConfirmed) {
      window.dispatchEvent(new CustomEvent("bronytv:open-auth", { detail: { mode: "signin" } }));
      return;
    }

    const sessionId = ensureSession(cfg.sessionKey);
    const userMsg = { id: nextId(), role: "user", text, limit: false };
        const assistantMsg = { id: nextId(), role: "assistant", text: "", limit: false, streaming: true };

    const nextMessages = [...messages, userMsg, assistantMsg];
    setMessages(nextMessages);
    setInput("");
    if (textareaRef.current) textareaRef.current.style.height = "auto";
    setError("");
    setStreaming(true);

    // Немного метаданных сессии для статистики (не критично).
    const meta = loadSessionMeta(cfg.metaKey);
    meta[activeBotId] = { lastUsed: Date.now(), updatedAt: Date.now() };
    saveSessionMeta(cfg.metaKey, meta);

    const controller = new AbortController();
    streamRef.current = controller;

    try {
      const res = await fetch(cfg.streamUrl, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        credentials: "include",
        signal: controller.signal,
        body: JSON.stringify({ sessionId, characterId: activeBotId, message: text })
      });

      if (res.status === 401 || res.status === 403) {
        await refreshUser();
        throw new Error("Сессия истекла. Войдите в аккаунт снова.");
      }
      if (res.status === 429) {
        throw new Error("Слишком много запросов. Подождите минуту и попробуйте снова.");
      }
      if (!res.ok) {
        const payload = await res.json().catch(() => ({}));
        throw new Error(payload.message || `Сервер ответил: ${res.status}`);
      }

      const reader = res.body.getReader();
      const decoder = new TextDecoder();
      const alive = { value: true };

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
          let parsed;
          try {
            parsed = JSON.parse(data);
          } catch {
            continue;
          }
          if (parsed && typeof parsed.error === "string") {
            throw new Error(parsed.error);
          }
          if (parsed && typeof parsed.text === "string") {
            if (parsed.limit === true) {
              finalizeAssistant(parsed.text, true);
            } else {
              pushChunk(parsed.text);
            }
          }
        }
        if (finished) break;
      }

      streamRef.current = null;
      // Снимаем streaming-флаг с последнего ассистентского сообщения в любом случае.
      finalizeAssistant("", false);
      setStreaming(false);
      setMessages((prev) => {
        storeMessages(cfg.messagesKey, activeBotId, prev);
        return prev;
      });
    } catch (err) {
      streamRef.current = null;
      setStreaming(false);
      if (err.name !== "AbortError") {
        setError(err.message || "Не удалось получить ответ. Попробуйте ещё раз.");
        setMessages((prev) => prev.filter((m) => m.id !== assistantMsg.id));
      }
    }
  }, [activeBotId, input, messages, refreshUser, streaming, user]);

    const handleKeyDown = useCallback(
    (e) => {
      if (e.key === "Enter" && !e.shiftKey) {
        e.preventDefault();
        handleSend();
      }
    },
    [handleSend]
  );

  const handleStartAdventure = useCallback(() => {
    if (!characterName.trim()) return;
    const sheet =
      `**Анкета персонажа:** Имя: ${characterName.trim()} Возраст: ${characterAge.trim()} ` +
      `Пол: ${characterGender.trim()} Раса: ${characterRace.trim()} Кьютимарка: ${characterCutieMark.trim()} ` +
      `Описание: ${characterDescription.trim()}  **Сюжет:** ${characterPlot.trim()} `;
    handleSend(sheet);
  }, [
    characterName,
    characterAge,
    characterGender,
    characterRace,
    characterCutieMark,
    characterDescription,
    characterPlot,
    handleSend
  ]);

  const orderedBots = useMemo(
    () => [
      ...bots.filter((b) => b.id === "narrator"),
      ...bots.filter((b) => b.id !== "narrator")
    ],
    [bots]
  );

  const isDesktopView = () => window.matchMedia("(min-width: 961px)").matches;
  const isMobileView = () => !isDesktopView();

  const showChatPane = (isDesktopView() || chatView) && activeBotId;
  const showListPane = isDesktopView() || !chatView;

  const isCollapsed = sidebarCollapsed && isDesktopView();

  const mobileDialogOpen = isMobileView() && showChatPane;

  // Жёсткая фиксация: пока открыт полноэкранный мобильный диалог — блокируем прокрутку body,
  // чтобы страница не ездила за чатом, а скроллились только сообщения внутри чата.
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
        <div className="ai-auth-gate-icon">
          <Bot size={34} />
        </div>
        <h2>Проверяем сессию…</h2>
        <p className="muted">Подождите немного.</p>
      </section>
    );
  }

  if (!user || !user.isEmailConfirmed) {
    const openAuth = (mode) =>
      window.dispatchEvent(new CustomEvent("bronytv:open-auth", { detail: { mode } }));

    return (
      <section className="ai-chat-page ai-auth-gate panel">
        <div className="ai-auth-gate-icon">
          <LockKeyhole size={34} />
        </div>
        <div>
          <h2>Войдите, чтобы общаться с ИИ-ботами</h2>
          <p className="muted">
            Доступ к персонажам открыт только пользователям с подтверждённым email.
          </p>
        </div>
        <div className="ai-auth-gate-actions">
          <button type="button" className="primary-btn" onClick={() => openAuth("signin")}>
            <LogIn size={17} />
            Войти
          </button>
          <button type="button" className="secondary-btn" onClick={() => openAuth("signup")}>
            <UserPlus size={17} />
            Зарегистрироваться
          </button>
        </div>
        <p className="ai-auth-gate-note">
          При регистрации мы отправим на вашу почту одноразовый 6-значный код.
        </p>
      </section>
    );
  }

  return (
    <section className={`ai-chat-page panel${isMobileView() && showChatPane ? " ai-chat-page--chat" : ""}`}>
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
        className={`ai-messenger ${isCollapsed ? "ai-messenger--collapsed" : ""} ${
          isMobileView() ? "ai-messenger--mobile" : ""
        }`}
      >
        {showListPane && (
          <div className={`ai-bot-list-pane${isCollapsed ? " is-collapsed" : ""}`}>
            <div className="ai-bot-list-head">
              <span className="ai-bot-list-title">Персонажи</span>
            </div>

                        <div className="ai-bot-list">
              {orderedBots.map((bot) => {
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
              <div className="ai-chat-head-main">
                {isMobileView() && (
                  <button
                    type="button"
                    className="ai-back-btn"
                    onClick={goBackToList}
                    aria-label="Назад к списку"
                  >
                    <ArrowLeft size={18} />
                  </button>
                )}
                <BotAvatar bot={activeBot} size={44} />
                <div className="ai-chat-head-info">
                                    <span className="ai-chat-head-name">{activeBot.name}</span>
                  <span className="ai-chat-head-status">онлайн · менеджер настроения</span>
                </div>
              </div>
              <div className="ai-chat-head-actions">
                {isDesktopView() && (
                  <button
                    type="button"
                    className="ai-chat-head-action"
                    onClick={toggleCollapse}
                    aria-label={sidebarCollapsed ? "Открыть список персонажей" : "Скрыть список персонажей"}
                    title={sidebarCollapsed ? "Открыть список персонажей" : "Скрыть список персонажей"}
                  >
                    {sidebarCollapsed ? <PanelLeftOpen size={18} /> : <PanelLeftClose size={18} />}
                  </button>
                )}
                {!isAdminMode && (
                  <button
                    type="button"
                    className="ai-chat-head-action"
                    onClick={() => (premiumStatus?.isActive ? setShowPremiumInfoModal(true) : setShowPremiumModal(true))}
                    aria-label={premiumStatus?.isActive ? "Статус Premium" : "Активировать Premium"}
                    title={premiumStatus?.isActive ? "Статус Premium" : "Активировать Premium"}
                  >
                    {premiumStatus?.isActive ? <Check size={20} /> : <Plus size={20} />}
                  </button>
                )}
                <button
                  type="button"
                  className="ai-chat-head-action"
                  onClick={confirmClearHistory}
                  aria-label="Очистить историю"
                  title="Очистить историю"
                >
                  <Trash2 size={18} />
                </button>
              </div>
            </div>

            {error && <div className="ai-chat-error">{error}</div>}

                        <div className="ai-messages" ref={scrollRef}>
                            {isNarratorSetup ? (
                <div className="ai-sheet-form">
                  <div className="ai-sheet-form-head">
                    <div className="ai-sheet-form-title">
                      <BookOpen size={20} />
                      <span>Создай своего персонажа</span>
                    </div>
                    <p>Заполни анкету — и Рассказчик отправит тебя в приключение по Эквестрии.</p>
                  </div>

                  <label className="news-field">
                    <span>Имя *</span>
                    <input
                      type="text"
                      value={characterName}
                      onChange={(e) => setCharacterName(e.target.value)}
                      placeholder="Например, Лунный Вихрь"
                      maxLength={80}
                    />
                  </label>

                  <div className="ai-sheet-form-row">
                    <label className="news-field">
                      <span>Возраст</span>
                      <input
                        type="text"
                        value={characterAge}
                        onChange={(e) => setCharacterAge(e.target.value)}
                        placeholder="Например, 18"
                        maxLength={40}
                      />
                    </label>
                    <label className="news-field">
                      <span>Пол</span>
                      <input
                        type="text"
                        value={characterGender}
                        onChange={(e) => setCharacterGender(e.target.value)}
                        placeholder="Например, жеребец"
                        maxLength={40}
                      />
                    </label>
                  </div>

                  <div className="ai-sheet-form-row">
                    <label className="news-field">
                      <span>Раса</span>
                      <input
                        type="text"
                        value={characterRace}
                        onChange={(e) => setCharacterRace(e.target.value)}
                        placeholder="Пони, пегас, единорог..."
                        maxLength={60}
                      />
                    </label>
                    <label className="news-field">
                      <span>Кьютимарка</span>
                      <input
                        type="text"
                        value={characterCutieMark}
                        onChange={(e) => setCharacterCutieMark(e.target.value)}
                        placeholder="Например, серебряная звезда"
                        maxLength={60}
                      />
                    </label>
                  </div>

                  <label className="news-field">
                    <span>Описание персонажа</span>
                    <textarea
                      value={characterDescription}
                      onChange={(e) => setCharacterDescription(e.target.value)}
                      placeholder="Характер, внешность, цели..."
                      rows={3}
                      maxLength={1000}
                    />
                  </label>

                  <label className="news-field">
                    <span>Завязка сюжета</span>
                    <textarea
                      value={characterPlot}
                      onChange={(e) => setCharacterPlot(e.target.value)}
                      placeholder="С чего начнётся приключение? Можно оставить пустым."
                      rows={5}
                      maxLength={1500}
                    />
                  </label>

                  <button
                    type="button"
                                        className="primary-btn ai-sheet-form-submit"
                    onClick={() => handleStartAdventure()}
                    disabled={!characterName.trim() || streaming}
                  >
                    <BookOpen size={17} />
                    Начать приключение
                  </button>

                  {!characterName.trim() && (
                    <p className="ai-sheet-form-hint">Введите имя персонажа, чтобы начать.</p>
                  )}
                </div>
              ) : messages.length === 0 ? (
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

                                                {!isNarratorSetup && (
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
                  onClick={() => handleSend()}
                  disabled={streaming || !input.trim()}
                  aria-label="Отправить"
                >
                  <Send size={18} />
                </button>
              </div>
            )}
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

      {confirmClear && (
        <div className="ai-confirm-overlay" onClick={cancelClearHistory}>
          <div className="ai-confirm-modal" role="dialog" aria-modal="true" onClick={(e) => e.stopPropagation()}>
            <button
              type="button"
              className="ai-confirm-close"
              onClick={cancelClearHistory}
              aria-label="Закрыть"
              title="Закрыть"
            >
              <X size={18} />
            </button>
            <div className="ai-confirm-icon">
              <Trash2 size={22} />
            </div>
            <h3>Вы точно хотите удалить чат?</h3>
            <p className="muted">История переписки с этим персонажем будет очищена безвозвратно.</p>
            <div className="ai-confirm-actions">
              <button
                type="button"
                className="ai-confirm-btn ai-confirm-btn--cancel"
                                onClick={cancelClearHistory}
              >
                <X size={16} />
                Отмена
              </button>
                            <button
                type="button"
                className="ai-confirm-btn ai-confirm-btn--danger"
                onClick={doClearHistory}
              >
                <Check size={16} />
                Удалить
              </button>
            </div>
          </div>
        </div>
      )}

      {!isAdminMode && showPremiumModal && (
        <div className="ai-premium-overlay" onClick={() => setShowPremiumModal(false)}>
          <div className="ai-premium-modal" role="dialog" aria-modal="true" onClick={(e) => e.stopPropagation()}>
            <button
              type="button"
              className="ai-premium-close"
              onClick={() => setShowPremiumModal(false)}
              aria-label="Закрыть"
              title="Закрыть"
            >
              <X size={18} />
            </button>
            <div className="ai-premium-modal-icon">
              <Star size={22} />
            </div>
            <h3>Активация Premium</h3>
                        <p className="ai-premium-modal-text">
              Активируй премиум-ключ с Boosty и получи <strong>безлимит всего за 50 рублей в месяц</strong>.
            </p>
            <div className="ai-premium-modal-input-row">
              <input
                type="text"
                className="ai-premium-modal-input"
                value={premiumKey}
                onChange={(e) => setPremiumKey(e.target.value)}
                placeholder="Введите премиум-ключ"
                disabled={premiumLoading}
                maxLength={64}
                onKeyDown={(e) => {
                  if (e.key === "Enter") {
                    e.preventDefault();
                    handleActivate();
                  }
                }}
              />
              <button
                type="button"
                className="primary-btn ai-premium-modal-btn"
                onClick={handleActivate}
                disabled={premiumLoading || !premiumKey.trim()}
              >
                {premiumLoading ? "..." : "Активировать"}
              </button>
            </div>

            {premiumError && <div className="ai-premium-modal-msg ai-premium-modal-msg--err">{premiumError}</div>}
            {premiumMsg && <div className="ai-premium-modal-msg ai-premium-modal-msg--ok">{premiumMsg}</div>}

                        <p className="ai-premium-modal-hint">
              Ключ можно получить на{" "}
              <a href="https://boosty.to/bronytvru" target="_blank" rel="noopener noreferrer">
                Boosty
              </a>
              . Донат или подписка снимут ограничения и откроют новые возможности.
            </p>
          </div>
        </div>
      )}

      {!isAdminMode && showPremiumInfoModal && (
        <div className="ai-premium-overlay" onClick={() => setShowPremiumInfoModal(false)}>
          <div className="ai-premium-modal" role="dialog" aria-modal="true" onClick={(e) => e.stopPropagation()}>
            <button
              type="button"
              className="ai-premium-close"
              onClick={() => setShowPremiumInfoModal(false)}
              aria-label="Закрыть"
              title="Закрыть"
            >
              <X size={18} />
            </button>
            <div className="ai-premium-modal-icon">
              <Check size={22} />
            </div>
            <h3>У вас активен премиум</h3>
            <p className="ai-premium-modal-text">
              Осталось{" "}
              <strong>
                {premiumStatus?.daysLeft ?? 0}{" "}
                {(() => {
                  const d = premiumStatus?.daysLeft ?? 0;
                  const mod10 = d % 10;
                  const mod100 = d % 100;
                  if (mod10 === 1 && mod100 !== 11) return "день";
                  if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return "дня";
                  return "дней";
                })()}
              </strong>{" "}
              (до {formatPremiumDate(premiumStatus?.expiresAt) || "—"}).
            </p>
            <div className="ai-premium-modal-hint">
              <p>Премиум уже активен, вводить ключ не нужно. Новый ключ просто продлит срок действия.</p>
            </div>
          </div>
        </div>
      )}
    </section>
  );
}

export default AiChatPage;
