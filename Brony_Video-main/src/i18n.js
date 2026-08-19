import React, { createContext, useCallback, useContext, useMemo, useState } from "react";

// =============================================================================
// BronyTV — простой i18n-контекст на React (RU | EN).
// Словари вынесены вниз файла, чтобы держать «холст» перевода в одном месте.
// =============================================================================

const I18nContext = createContext(null);

const STORAGE_KEY = "bronytv-language";

function readStoredLanguage() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (raw === "ru" || raw === "en") {
      return raw;
    }
  } catch {
    // ignore storage failures
  }
  return "ru";
}

export function I18nProvider({ children }) {
  const [language, setLanguageState] = useState(readStoredLanguage);

  const setLanguage = useCallback((next) => {
    const lang = next === "en" ? "en" : "ru";
    setLanguageState(lang);
    try {
      localStorage.setItem(STORAGE_KEY, lang);
    } catch {
      // ignore storage failures
    }
  }, []);

  const t = useCallback(
    (key, params) => {
      const table = TRANSLATIONS[language] || TRANSLATIONS.ru;
      let value = table[key] ?? TRANSLATIONS.ru[key] ?? key;
      if (params && typeof value === "string") {
        value = value.replace(/\{(\w+)\}/g, (match, name) =>
          Object.prototype.hasOwnProperty.call(params, name) ? String(params[name]) : match
        );
      }
      return value;
    },
    [language]
  );

  const value = useMemo(() => ({ language, setLanguage, t }), [language, setLanguage, t]);

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}

export function useI18n() {
  const context = useContext(I18nContext);
  if (!context) {
    throw new Error("useI18n must be used within an I18nProvider.");
  }
  return context;
}

// =============================================================================
// СЛОВАРИ
// =============================================================================

const ru = {
  // === Навигация (Sidebar) ===
  "nav.home": "Главная",
  "nav.forum": "Форум",
  "nav.news": "Новости",
  "nav.bots": "ИИ Боты",
  "nav.light": "Тьма",
  "nav.dark": "Свет",
  "nav.season": "С{number}",

  // === VPN ===
  "vpn.label": "VPN от BronyTV",
  "vpn.modalTitle": "BronyVPN",
  "vpn.text": "Защищённый BronyVPN находится в стадии разработки. Доступ скоро появится!",
  "vpn.close": "Понятно",
  "vpn.signin": "Войти",
  "vpn.loginPrompt": "Войдите в аккаунт, чтобы получить доступ к BronyVPN.",
  "vpn.loading": "Загрузка статуса…",
  "vpn.retry": "Повторить",
  "vpn.comingSoon": "BronyVPN пока недоступен. Мы подготовим его и откроем доступ позже.",
  "vpn.active": "Активен",
  "vpn.plan": "Тариф",
  "vpn.planDefault": "BronyVPN",
  "vpn.daysLeft": "Осталось дней",
  "vpn.expires": "Действует до",
  "vpn.connectionLink": "Ссылка подключения (VLESS)",
  "vpn.copy": "Копировать",
  "vpn.clients": "Клиенты",
  "vpn.panel": "Панель",
  "vpn.renew": "Продлить",
  "vpn.noSubscription": "У вас пока нет активной подписки BronyVPN.",
  "vpn.trialStart": "Активировать trial ({days} дней)",
  "vpn.trialUsed": "Trial уже использован",
  "vpn.promoLabel": "Промо-код",
  "vpn.promoPlaceholder": "Введите промо-код",
  "vpn.activate": "Активировать",
  "vpn.promoSuccess": "Промо-код активирован!",
  "vpn.referralTitle": "Реферальная ссылка",
  "vpn.referralText": "Поделитесь ссылкой — друг получит бонус при регистрации, а вы — награду.",

  // === Главная страница ===
  "home.tagline":
    "BronyTV — это уютный стриминг-сервис для поклонников My Little Pony: Friendship Is Magic с удобной навигацией по сезонам, подборкой лучших эпизодов по рейтингу и быстрым доступом к просмотру. На главной собран топ-10 самых высоко оцененных видео по данным IMDb, а внутри каждого сезона можно выставить свою оценку от 1 до 10. Здесь легко найти любимые серии и быстро перейти к просмотру без лишних действий.",
  "home.openSeasons": "Открыть сезоны",
  "home.openForum": "Открыть форум",
  "home.openNews": "Открыть новости",
  "home.topTitle": "Топ-10 видео MLP по рейтингу IMDb",
  "home.seasonEpisode": "Сезон {season}, серия {episode} • {source}: {rating}/10",
  "home.rate": "Оценить",
  "home.delete": "Удалить",







  // === Форум ===
  "forum.title": "Форум BronyTV",
  "forum.subtitle": "Обсуждайте серии, теории и всё о пони.",
  "forum.createThread": "Создать тему",
  "forum.loginHint": "Войдите, чтобы создавать темы.",
  "forum.loadingThreads": "Загрузка тем…",
  "forum.loadingThread": "Загрузка темы…",
  "forum.emptyThreads": "Тем пока нет. Создайте первую!",
  "forum.loadError": "Не удалось загрузить темы форума.",
  "forum.responses": "ответов: {count}",
  "forum.backToList": "К списку тем",
  "forum.backToForum": "Назад к форуму",
  "forum.titleCreate": "Создать тему",
  "forum.fieldTitle": "Заголовок (до 150 символов)",
  "forum.fieldDescription": "Описание (необязательно)",
  "forum.fieldImages": "Прикрепить изображения (до 3)",
  "forum.titleRequired": "Укажите заголовок темы.",
  "forum.titleTooLong": "Заголовок не может быть длиннее 150 символов.",
  "forum.createFailed": "Не удалось создать тему.",
  "forum.creating": "Создание…",
  "forum.publish": "Опубликовать",
  "forum.cancel": "Отмена",
  "forum.errorTitleNotFound": "Тема не найдена.",
  "forum.deleteThreadConfirm": "Удалить эту тему?",
  "forum.deletePostConfirm": "Удалить этот пост?",
  "forum.deleteThread": "Удалить тему",
  "forum.replyTo": "Ответить",
  "forum.replyingTo": "Ответ пользователю",
  "forum.cancelReply": "Отменить ответ",
  "forum.like": "Лайк",
  "forum.deletePost": "Удалить пост",
  "forum.replyLabel": "Ваш ответ",
  "forum.chooseFiles": "Выбрать файлы",
  "forum.replyEmpty": "Введите текст ответа или прикрепите изображение.",
  "forum.replyFailed": "Не удалось отправить ответ.",
  "forum.sending": "Отправка…",
  "forum.sendReply": "Отправить ответ",
  "forum.noUsername": "Задайте юзернейм в личном кабинете, чтобы отвечать в теме.",
  "forum.loginToReply": "Войдите в аккаунт, чтобы ответить в теме.",
  "forum.answers": "Ответы ({count})",
  "forum.emptyPosts": "Пока нет ответов. Напишите первым!",
  "forum.loadThreadError": "Не удалось загрузить тему.",

  // === Новости ===
  "news.title": "Новости",
  "news.subtitle": "Актуальные новости проекта BronyTV.",
  "news.create": "Создать новость",
  "news.loading": "Загрузка новостей…",
  "news.empty": "Пока нет новостей. Будьте первыми!",
  "news.loadError": "Не удалось загрузить новости.",
  "news.titleCreate": "Создать новость",
  "news.fieldTitle": "Заголовок (необязательно)",
  "news.fieldContent": "Текст (необязательно)",
  "news.fieldImageUrl": "Ссылка на изображение (необязательно)",
  "news.fieldFiles": "Загрузить файлы (до 5)",
  "news.placeholderTitle": "Заголовок новости",
  "news.placeholderContent": "Содержание новости",
  "news.placeholderUrl": "URL изображения",
  "news.required": "Укажите хотя бы заголовок, текст или изображение.",
  "news.readFileError": "Не удалось прочитать файлы изображений.",
  "news.createFailed": "Не удалось создать новость.",
  "news.publishing": "Публикация…",
  "news.publish": "Опубликовать",
  "news.cancel": "Отмена",
  "news.readMore": "Читать далее",
  "news.collapse": "Свернуть",
  "news.deleteConfirm": "Удалить эту новость?",
  "news.deleteFailed": "Не удалось удалить новость.",
  "news.delete": "Удалить новость"
};

const en = {
  "nav.home": "Home",
  "nav.forum": "Forum",
  "nav.news": "News",
  "nav.bots": "AI Bots",
  "nav.light": "Dark",
  "nav.dark": "Light",
  "nav.season": "S{number}",

  "vpn.label": "BronyTV VPN",
  "vpn.modalTitle": "BronyVPN",
  "vpn.text": "Protected BronyVPN is under development. Access will be available soon!",
  "vpn.close": "Got it",
  "vpn.signin": "Sign in",
  "vpn.loginPrompt": "Log in to get access to BronyVPN.",
  "vpn.loading": "Loading status…",
  "vpn.retry": "Retry",
  "vpn.comingSoon": "BronyVPN is not available yet. We'll prepare it and open access later.",
  "vpn.active": "Active",
  "vpn.plan": "Plan",
  "vpn.planDefault": "BronyVPN",
  "vpn.daysLeft": "Days left",
  "vpn.expires": "Expires",
  "vpn.connectionLink": "Connection link (VLESS)",
  "vpn.copy": "Copy",
  "vpn.clients": "Clients",
  "vpn.panel": "Panel",
  "vpn.renew": "Renew",
  "vpn.noSubscription": "You don't have an active BronyVPN subscription yet.",
  "vpn.trialStart": "Start trial ({days} days)",
  "vpn.trialUsed": "Trial already used",
  "vpn.promoLabel": "Promo code",
  "vpn.promoPlaceholder": "Enter promo code",
  "vpn.activate": "Activate",
  "vpn.promoSuccess": "Promo code activated!",
  "vpn.referralTitle": "Referral link",
  "vpn.referralText": "Share the link — a friend gets a bonus on signup and you get a reward.",

  "home.tagline":
    "BronyTV is a cozy streaming service for fans of My Little Pony: Friendship Is Magic with convenient season navigation, a curated selection of top-rated episodes, and quick access to viewing. The homepage features the top-10 highest-rated videos per IMDb, and inside each season you can leave your own rating from 1 to 10. It's easy to find your favorite episodes and jump right into playback.",
  "home.openSeasons": "Open seasons",
  "home.openForum": "Open forum",
  "home.openNews": "Open news",
  "home.topTitle": "Top-10 MLP videos by IMDb rating",
  "home.seasonEpisode": "Season {season}, episode {episode} • {source}: {rating}/10",
  "home.rate": "Rate",
  "home.delete": "Remove",
  // === Forum ===
  "forum.title": "BronyTV Forum",
  "forum.subtitle": "Discuss episodes, theories and everything about ponies.",
  "forum.createThread": "Create thread",
  "forum.loginHint": "Log in to create threads.",
  "forum.loadingThreads": "Loading threads…",
  "forum.loadingThread": "Loading thread…",
  "forum.emptyThreads": "No threads yet. Create the first one!",
  "forum.loadError": "Failed to load forum threads.",
  "forum.responses": "replies: {count}",
  "forum.backToList": "Back to thread list",
  "forum.backToForum": "Back to forum",
  "forum.titleCreate": "Create thread",
  "forum.fieldTitle": "Title (up to 150 characters)",
  "forum.fieldDescription": "Description (optional)",
  "forum.fieldImages": "Attach images (up to 3)",
  "forum.titleRequired": "Please provide a thread title.",
  "forum.titleTooLong": "Title cannot be longer than 150 characters.",
  "forum.createFailed": "Failed to create thread.",
  "forum.creating": "Creating…",
  "forum.publish": "Post",
  "forum.cancel": "Cancel",
  "forum.errorTitleNotFound": "Thread not found.",
  "forum.deleteThreadConfirm": "Delete this thread?",
  "forum.deletePostConfirm": "Delete this post?",
  "forum.deleteThread": "Delete thread",
  "forum.replyTo": "Reply",
  "forum.replyingTo": "Replying to",
  "forum.cancelReply": "Cancel reply",
  "forum.like": "Like",
  "forum.deletePost": "Delete post",
  "forum.replyLabel": "Your reply",
  "forum.chooseFiles": "Choose files",
  "forum.replyEmpty": "Enter reply text or attach an image.",
  "forum.replyFailed": "Failed to send reply.",
  "forum.sending": "Sending…",
  "forum.sendReply": "Send reply",
  "forum.noUsername": "Set a username in your profile to reply in threads.",
  "forum.loginToReply": "Log in to reply in this thread.",
  "forum.answers": "Replies ({count})",
  "forum.emptyPosts": "No replies yet. Be the first to write!",
  "forum.loadThreadError": "Failed to load thread.",

  // === News ===
  "news.title": "News",
  "news.subtitle": "Latest BronyTV project news.",
  "news.create": "Create news",
  "news.loading": "Loading news…",
  "news.empty": "No news yet. Be the first!",
  "news.loadError": "Failed to load news.",
  "news.titleCreate": "Create news",
  "news.fieldTitle": "Title (optional)",
  "news.fieldContent": "Text (optional)",
  "news.fieldImageUrl": "Image link (optional)",
  "news.fieldFiles": "Upload files (up to 5)",
  "news.placeholderTitle": "News title",
  "news.placeholderContent": "News content",
  "news.placeholderUrl": "Image URL",
  "news.required": "Provide at least a title, text or image.",
  "news.readFileError": "Failed to read image files.",
  "news.createFailed": "Failed to create news.",
  "news.publishing": "Publishing…",
  "news.publish": "Publish",
  "news.cancel": "Cancel",
  "news.readMore": "Read more",
  "news.collapse": "Collapse",
  "news.deleteConfirm": "Delete this news?",
  "news.deleteFailed": "Failed to delete news.",
  "news.delete": "Delete news"
};

const TRANSLATIONS = { ru, en };

