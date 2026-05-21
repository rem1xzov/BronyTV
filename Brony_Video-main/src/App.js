import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { createPortal } from "react-dom";
import {
  Check,
  ChevronRight,
  Download,
  Home,
  Maximize,
  Minimize,
  MoreVertical,
  Moon,
  Pause,
  Play,
  PlayCircle,
  SkipForward,
  Star,
  Sun,
  Tv
} from "lucide-react";
import { Link, Route, Routes, useLocation, useNavigate, useParams } from "react-router-dom";

const SEASON_INFO = [
  {
    number: 1,
    title: "My Little Pony: Friendship Is Magic — Season 1",
    description: "Начало истории о Понивилле, Элементах Гармонии и становлении дружбы Mane 6."
  },
  {
    number: 2,
    title: "My Little Pony: Friendship Is Magic — Season 2",
    description: "Сезон масштабных конфликтов: возвращение Дискорда, свадьба в Кантерлоте и новые испытания."
  },
  {
    number: 3,
    title: "My Little Pony: Friendship Is Magic — Season 3",
    description: "Короткий, но ключевой сезон: Кристальная Империя и важный шаг Твайлайт к принцессе."
  },
  {
    number: 4,
    title: "My Little Pony: Friendship Is Magic — Season 4",
    description: "Поиск тайны волшебного сундука, новые квесты и развитие каждой из главных героинь."
  },
  {
    number: 5,
    title: "My Little Pony: Friendship Is Magic — Season 5",
    description: "Карта дружбы отправляет героев в миссии, а финал сражает мощью и эмоциями."
  },
  {
    number: 6,
    title: "My Little Pony: Friendship Is Magic — Season 6",
    description: "Сезон о взрослении персонажей, новых семьях и расширении мира Эквестрии."
  },
  {
    number: 7,
    title: "My Little Pony: Friendship Is Magic — Season 7",
    description: "Фокус на прошлом и семьях героев: легенды, родственники и личные открытия."
  },
  {
    number: 8,
    title: "My Little Pony: Friendship Is Magic — Season 8",
    description: "Запуск Школы Дружбы и знакомство с новым поколением учеников из разных рас."
  },
  {
    number: 9,
    title: "My Little Pony: Friendship Is Magic — Season 9",
    description: "Финальная глава сериала: союз злодеев, эпический финал и завершение истории Mane 6."
  }
];

const TOP_MLP_VIDEOS = [
  { id: "tt6240452", title: "The Perfect Pear", season: 7, episode: 13, imdbRating: "9.5", source: "IMDb" },
  { id: "tt10084500", title: "The Last Problem", season: 9, episode: 26, imdbRating: "9.3", source: "IMDb" },
  { id: "tt2303845", title: "A Canterlot Wedding - Part 2", season: 2, episode: 26, imdbRating: "9.2", source: "IMDb" },
  { id: "tt3088332", title: "Twilight's Kingdom - Part 2", season: 4, episode: 26, imdbRating: "9.2", source: "IMDb" },
  { id: "tt4534312", title: "Slice of Life", season: 5, episode: 9, imdbRating: "9.2", source: "IMDb" },
  { id: "tt4534334", title: "Crusaders of the Lost Mark", season: 5, episode: 18, imdbRating: "9.2", source: "IMDb" },
  { id: "tt4534316", title: "Amending Fences", season: 5, episode: 12, imdbRating: "9.1", source: "IMDb" },
  { id: "tt8074576", title: "Sounds of Silence", season: 8, episode: 23, imdbRating: "9.1", source: "IMDb" },
  { id: "tt10084492", title: "The Ending of the End - Part 1", season: 9, episode: 24, imdbRating: "9.0", source: "IMDb" },
  { id: "tt10084494", title: "The Ending of the End - Part 2", season: 9, episode: 25, imdbRating: "9.0", source: "IMDb" }
];

const BASE_GENRES = ["Приключения", "Комедия", "Фэнтези", "Драма", "Музыкальный", "Семейный"];

const buildEpisodes = (seasonNumber) =>
  Array.from({ length: 26 }, (_, idx) => {
    const id = idx + 1;
    const topMatch = TOP_MLP_VIDEOS.find((item) => item.season === seasonNumber && item.episode === id);
    return {
      id,
      title: topMatch?.title || `Сезон ${seasonNumber} — серия ${id}`,
      genre: BASE_GENRES[(idx + seasonNumber) % BASE_GENRES.length],
      duration: "22 мин",
      description: topMatch
        ? `Один из самых высоко оцененных эпизодов фанатами (${topMatch.imdbRating}/10 на IMDb).`
        : `Эпизод ${id} сезона ${seasonNumber} из оригинального сериала.`,
      imdbRating: topMatch?.imdbRating || null,
      imdbId: topMatch?.id || `s${seasonNumber}e${id}`
    };
  });

const buildSeasonData = () =>
  SEASON_INFO.reduce((acc, seasonInfo) => {
    const seasonNumber = seasonInfo.number;
    acc[seasonNumber] = {
      title: seasonInfo.title,
      shortTitle: `С${seasonNumber}`,
      description: seasonInfo.description,
      episodes: buildEpisodes(seasonNumber)
    };
    return acc;
  }, {});

const CONSTANTS = {
  APP_NAME: "BronyTV",
  TOTAL_SEASONS: 9,
  SEASONS: buildSeasonData()
};

const RATING_VALUES = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

const INTRO_SONG_DURATION_SECONDS = 35;

/** When the MLP theme song starts (seconds) per season; song length is always 35s. */
const SEASON_INTRO_CONFIGS = {
  1: { startTime: 90 },
  2: { startTime: 50 },
  3: { startTime: 45 },
  4: { startTime: 75 },
  5: { startTime: 100 },
  6: { startTime: 80 },
  7: { startTime: 70 },
  8: { startTime: 130 },
  9: { startTime: 150 }
};

const DEFAULT_INTRO_CONFIG = { startTime: 60 };

const getSeasonIntroConfig = (seasonId) => {
  const seasonNumber = Number(seasonId);
  const startTime =
    SEASON_INTRO_CONFIGS[seasonNumber]?.startTime ?? DEFAULT_INTRO_CONFIG.startTime;
  const endTime = startTime + INTRO_SONG_DURATION_SECONDS;
  return {
    startTime,
    endTime,
    skipToTime: endTime
  };
};

const NEXT_EPISODE_REMAINING_SECONDS = 90;

const PLAYBACK_SPEED_OPTIONS = [
  { value: 0.5, label: "0.5x" },
  { value: 1, label: "1x (Normal)" },
  { value: 1.25, label: "1.25x" },
  { value: 1.5, label: "1.5x" },
  { value: 1.75, label: "1.75x" },
  { value: 2, label: "2x" }
];

const STORAGE_KEYS = {
  SEASON_RATINGS: "bronytv-season-ratings",
  VIDEO_RATINGS: "bronytv-video-ratings",
  THEME: "bronytv-theme",
  VIDEO_PROGRESS: "bronytv-video-progress"
};

const getPublicAssetUrl = (relativePath) => {
  const base = process.env.PUBLIC_URL || "";
  return `${base}/${relativePath.replace(/^\/+/, "")}`;
};

const API_BASE_URL = (process.env.REACT_APP_API_BASE_URL ?? "").replace(/\/$/, "");

/** Кодирует каждый сегмент пути (кириллица, пробелы) для корректной подстановки в URL. */
const encodeResourcePath = (path) => {
  if (!path) {
    return "";
  }
  const trimmed = path.trim();
  if (/^https?:\/\//i.test(trimmed)) {
    try {
      const u = new URL(trimmed);
      const encodedPath =
        "/" +
        u.pathname
          .split("/")
          .filter(Boolean)
          .map((segment) => encodeURIComponent(segment))
          .join("/");
      return `${u.origin}${encodedPath}${u.search}${u.hash}`;
    } catch {
      return trimmed;
    }
  }
  const [pathPart, ...queryParts] = trimmed.split("?");
  const query = queryParts.length > 0 ? `?${queryParts.join("?")}` : "";
  const normalized = pathPart.startsWith("/") ? pathPart : `/${pathPart}`;
  const segments = normalized.split("/").filter(Boolean);
  if (segments.length === 0) {
    return query || "/";
  }
  return `/${segments.map((segment) => encodeURIComponent(segment)).join("/")}${query}`;
};

const apiUrl = (path) => {
  const normalized = path.startsWith("/") ? path : `/${path}`;
  if (!API_BASE_URL) {
    return normalized;
  }
  return `${API_BASE_URL}${normalized}`;
};

const toAbsoluteApiUrl = (path) => {
  if (!path) {
    return "";
  }
  if (/^https?:\/\//i.test(path)) {
    return path;
  }
  const encoded = encodeResourcePath(path.startsWith("/") ? path : `/${path}`);
  return apiUrl(encoded);
};

/** Видео и статика — с корня сайта (/videos), не через префикс API. */
const getMediaUrl = (path) => {
  if (!path) {
    return "";
  }
  if (/^https?:\/\//i.test(path)) {
    return encodeResourcePath(path);
  }
  const normalized = path.startsWith("/") ? path : `/${path}`;
  return encodeResourcePath(normalized);
};

const SEASON_PREVIEW_FALLBACK = getPublicAssetUrl("season-preview-fallback.svg");

const normalizeContentPath = (path) => {
  if (!path || typeof path !== "string") {
    return "";
  }
  let normalized = path.trim();
  if (!normalized || normalized === "placeholder") {
    return "";
  }
  normalized = normalized.replace(/default_season/gi, "default-season");
  if (normalized.startsWith("api/")) {
    normalized = `/${normalized.slice(4)}`;
  }
  if (normalized.startsWith("content/") || normalized.startsWith("videos/")) {
    normalized = `/${normalized}`;
  }
  if (!normalized.startsWith("/") && !/^https?:\/\//i.test(normalized)) {
    normalized = `/${normalized}`;
  }
  return normalized;
};

const resolveContentUrl = (path) => {
  const normalized = normalizeContentPath(path);
  if (!normalized) {
    return "";
  }
  if (/^https?:\/\//i.test(normalized)) {
    return encodeResourcePath(normalized);
  }
  if (normalized.startsWith("/content/") || normalized.startsWith("/videos/")) {
    return getMediaUrl(normalized);
  }
  return toAbsoluteApiUrl(normalized);
};

const resolveSeasonPreviewCandidates = (seasonNumber, posterPath) => {
  const candidates = [];
  const fromApi = normalizeContentPath(posterPath);
  if (fromApi) {
    candidates.push(fromApi);
  }
  candidates.push("/content/previews/default-season.jpg");
  candidates.push(SEASON_PREVIEW_FALLBACK);
  return [...new Set(candidates.map(resolveContentUrl).filter(Boolean))];
};

function useResolvedImageUrl(candidates) {
  const [url, setUrl] = useState("");
  const key = candidates.join("|");

  useEffect(() => {
    let cancelled = false;
    const list = candidates.filter(Boolean);
    if (!list.length) {
      setUrl("");
      return undefined;
    }

    const tryNext = (index) => {
      if (cancelled) {
        return;
      }
      if (index >= list.length) {
        setUrl("");
        return;
      }
      const img = new Image();
      img.onload = () => {
        if (!cancelled) {
          setUrl(list[index]);
        }
      };
      img.onerror = () => tryNext(index + 1);
      img.src = list[index];
    };

    setUrl("");
    tryNext(0);
    return () => {
      cancelled = true;
    };
  }, [key]);

  return url;
}

const readStorageObject = (key) => {
  try {
    const raw = localStorage.getItem(key);
    if (!raw) {
      return {};
    }
    const parsed = JSON.parse(raw);
    return typeof parsed === "object" && parsed ? parsed : {};
  } catch (error) {
    return {};
  }
};

const getPageFromPath = (path) => {
  if (path.startsWith("/player")) {
    return "player";
  }
  if (path.startsWith("/season")) {
    return "season";
  }
  return "home";
};

function RatingButton({
  value,
  onRate,
  label = "Оценить",
  popoverId,
  openPopoverId,
  onOpenPopoverId,
  variant = "episode"
}) {
  const widgetRef = useRef(null);
  const popupRef = useRef(null);
  const [localOpen, setLocalOpen] = useState(false);
  const [portalCoords, setPortalCoords] = useState(null);
  const managed = Boolean(popoverId && onOpenPopoverId);
  const isOpen = managed ? openPopoverId === popoverId : localOpen;
  const usePortal = variant === "header";

  const setOpen = (next) => {
    if (managed) {
      onOpenPopoverId(next ? popoverId : null);
      return;
    }
    setLocalOpen(next);
  };

  const updatePortalCoords = useCallback(() => {
    if (!widgetRef.current) {
      return;
    }
    const rect = widgetRef.current.getBoundingClientRect();
    setPortalCoords({
      top: rect.bottom + 8,
      left: rect.left
    });
  }, []);

  useEffect(() => {
    if (!isOpen || !usePortal) {
      setPortalCoords(null);
      return undefined;
    }
    updatePortalCoords();
    window.addEventListener("resize", updatePortalCoords);
    window.addEventListener("scroll", updatePortalCoords, true);
    return () => {
      window.removeEventListener("resize", updatePortalCoords);
      window.removeEventListener("scroll", updatePortalCoords, true);
    };
  }, [isOpen, updatePortalCoords, usePortal]);

  useEffect(() => {
    if (!isOpen) {
      return undefined;
    }
    const handlePointerDown = (event) => {
      if (widgetRef.current?.contains(event.target)) {
        return;
      }
      if (popupRef.current?.contains(event.target)) {
        return;
      }
      setOpen(false);
    };
    document.addEventListener("mousedown", handlePointerDown);
    document.addEventListener("touchstart", handlePointerDown);
    return () => {
      document.removeEventListener("mousedown", handlePointerDown);
      document.removeEventListener("touchstart", handlePointerDown);
    };
  }, [isOpen, managed, onOpenPopoverId, popoverId]);

  const popupNode = (
    <div
      ref={popupRef}
      className={`rating-popup rating-popup--${variant}${usePortal ? " rating-popup--portal" : ""}`}
      role="menu"
      style={
        usePortal && portalCoords
          ? { top: `${portalCoords.top}px`, left: `${portalCoords.left}px` }
          : undefined
      }
    >
      {RATING_VALUES.map((score) => (
        <button
          key={score}
          type="button"
          className={`rating-number ${value === score ? "active" : ""}`}
          onClick={() => {
            onRate(score);
            setOpen(false);
          }}
        >
          {score}
        </button>
      ))}
    </div>
  );

  return (
    <div
      className={`rating-widget rating-widget--${variant}${isOpen ? " is-open" : ""}`}
      ref={widgetRef}
    >
      <button
        type="button"
        className="rate-btn"
        aria-expanded={isOpen}
        onClick={() => setOpen(!isOpen)}
      >
        <Star size={14} />
        <span>{value ? `${value}/10` : label}</span>
      </button>
      {isOpen && usePortal && portalCoords
        ? createPortal(popupNode, document.body)
        : isOpen
          ? popupNode
          : null}
    </div>
  );
}

function EpisodePlaceholderIcon({ episodeNumber }) {
  return (
    <div className="episode-thumb episode-thumb--placeholder" aria-hidden="true">
      <PlayCircle size={28} strokeWidth={1.75} />
      <span className="episode-thumb-label">E{episodeNumber}</span>
    </div>
  );
}

function Sidebar({ currentSeason, currentPage, theme, onToggleTheme }) {
  return (
    <aside className="sidebar">
      <button type="button" className="nav-pill theme-switch" onClick={onToggleTheme}>
        {theme === "dark" ? <Sun size={16} /> : <Moon size={16} />}
        <span>{theme === "dark" ? "Свет" : "Тьма"}</span>
      </button>
      <Link to="/" className={`nav-pill ${currentPage === "home" ? "active" : ""}`}>
        <Home size={16} />
        <span>Главная</span>
      </Link>
      {Array.from({ length: CONSTANTS.TOTAL_SEASONS }, (_, index) => index + 1).map((season) => (
        <Link
          key={season}
          to={`/season/${season}`}
          className={`nav-pill ${currentSeason === season && currentPage === "season" ? "active" : ""}`}
        >
          <Tv size={16} />
          <span>С{season}</span>
        </Link>
      ))}
    </aside>
  );
}

function HomePage({ videoRatings, onRateVideo, onClearVideoRating }) {
  const [openRatingId, setOpenRatingId] = useState(null);

  return (
    <div className="home-layout">
      <section className="panel hero-card">
        <div className="hero-content">
          <h1>{CONSTANTS.APP_NAME}</h1>
          <p className="description">
            BronyTV — это уютный стриминг-сервис для поклонников My Little Pony: Friendship Is Magic с удобной
            навигацией по сезонам, подборкой лучших эпизодов по рейтингу и быстрым доступом к просмотру. На главной
            собран топ-10 самых высоко оцененных видео по данным IMDb, а внутри каждого сезона можно выставить свою
            оценку от 1 до 10. Здесь легко найти любимые серии и быстро перейти к просмотру без лишних действий.
          </p>
          <div className="button-row">
            <Link className="primary-btn" to="/season/1">
              Открыть сезоны
            </Link>
          </div>
        </div>
      </section>

      <section className="panel quick-list rating-center">
        <div className="quick-list-head centered">
          <h2>Топ-10 видео MLP по рейтингу IMDb</h2>
        </div>
        {TOP_MLP_VIDEOS.map((item) => {
          const userRate = videoRatings[item.id];
          return (
            <div className="compact-episode" key={item.id}>
              <div className="episode-main">
                <Link to={`/player/${item.season}/${item.episode}`}>
                  <h3>{item.title}</h3>
                </Link>
                <p className="muted">
                  Сезон {item.season}, серия {item.episode} • {item.source}: {item.imdbRating}/10
                </p>
              </div>
              <div className="compact-actions">
                <span className="rating-pill">
                  <Star size={14} />
                  {item.imdbRating}
                </span>
                <RatingButton
                  value={userRate}
                  label="Оценить"
                  popoverId={`top-${item.id}`}
                  openPopoverId={openRatingId}
                  onOpenPopoverId={setOpenRatingId}
                  onRate={(score) => onRateVideo(item.id, score)}
                />
                {userRate ? (
                  <button type="button" className="secondary-btn small" onClick={() => onClearVideoRating(item.id)}>
                    Удалить
                  </button>
                ) : null}
              </div>
            </div>
          );
        })}
      </section>
    </div>
  );
}

function SeasonPage({
  setCurrentSeason,
  seasonRatings,
  videoRatings,
  onRateSeason,
  onRateVideo,
  onClearSeasonRating,
  onClearVideoRating,
  apiSeasons,
  apiVideosBySeason,
  onEnsureSeasonVideos
}) {
  const { seasonId } = useParams();
  const navigate = useNavigate();
  const season = Number(seasonId || 1);
  const safeSeason = season >= 1 && season <= CONSTANTS.TOTAL_SEASONS ? season : 1;
  const [openRatingId, setOpenRatingId] = useState(null);
  const remoteSeasonData = apiSeasons[safeSeason];
  const seasonPreviewCandidates = useMemo(
    () => resolveSeasonPreviewCandidates(safeSeason, remoteSeasonData?.posterPath),
    [remoteSeasonData?.posterPath, safeSeason]
  );
  const seasonPreviewUrl = useResolvedImageUrl(seasonPreviewCandidates);

  useEffect(() => {
    setCurrentSeason(safeSeason);
    if (season !== safeSeason) {
      navigate(`/season/${safeSeason}`, { replace: true });
    }
  }, [navigate, safeSeason, season, setCurrentSeason]);

  useEffect(() => {
    onEnsureSeasonVideos(safeSeason);
  }, [onEnsureSeasonVideos, safeSeason]);

  const localSeasonData = CONSTANTS.SEASONS[safeSeason];
  const seasonData = {
    ...localSeasonData,
    ...(remoteSeasonData
      ? {
          title: remoteSeasonData.title || localSeasonData?.title,
          description: remoteSeasonData.description || localSeasonData?.description
        }
      : {})
  };
  const remoteVideos = apiVideosBySeason[safeSeason] || [];
  const episodes = (localSeasonData?.episodes || []).map((episode) => {
    const remote = remoteVideos.find((video) => video.episodeNumber === episode.id);
    return remote
      ? {
          ...episode,
          title: remote.title || episode.title,
          description: remote.description || episode.description,
          filePath: remote.filePath || "",
          previewImageUrl: remote.previewImageUrl || ""
        }
      : episode;
  });

  return (
    <section className="panel season-page">
      <div className={`season-banner ${seasonPreviewUrl ? "has-season-preview" : ""}`}>
        <div
          className="season-banner-bg"
          style={seasonPreviewUrl ? { "--season-preview-url": `url("${seasonPreviewUrl}")` } : undefined}
          aria-hidden="true"
        />
        <div className="season-banner-content">
          <h2>{seasonData?.title || `Сезон ${safeSeason}`}</h2>
          <p className="muted">{seasonData?.description}</p>
          <div className="button-row season-banner-actions">
            <div className="season-rating-anchor">
              <RatingButton
                variant="header"
                value={seasonRatings[String(safeSeason)]}
                label="Оценить сезон"
                popoverId={`season-${safeSeason}`}
                openPopoverId={openRatingId}
                onOpenPopoverId={setOpenRatingId}
                onRate={(score) => onRateSeason(safeSeason, score)}
              />
            </div>
            {seasonRatings[String(safeSeason)] ? (
              <button type="button" className="secondary-btn" onClick={() => onClearSeasonRating(safeSeason)}>
                Удалить оценку сезона
              </button>
            ) : null}
          </div>
        </div>
      </div>
      <div className="episode-list episode-grid scrollable">
        {episodes.map((episode) => (
          <div className="episode-card" key={`s${safeSeason}-e${episode.id}`}>
            <EpisodePlaceholderIcon episodeNumber={episode.id} />
            <div className="episode-main">
              <Link to={`/player/${safeSeason}/${episode.id}`} state={{ episode }}>
                <h3>{episode.title}</h3>
              </Link>
              <p className="muted meta-row">
                {episode.genre} | {episode.duration}
              </p>
              <p className="muted">{episode.description}</p>
            </div>
            <div className="episode-actions">
              <Link className="primary-btn small" to={`/player/${safeSeason}/${episode.id}`} state={{ episode }}>
                <PlayCircle size={16} />
                <span>Play</span>
              </Link>
              <RatingButton
                value={videoRatings[episode.imdbId]}
                label="Оценить"
                popoverId={`s${safeSeason}-e${episode.id}`}
                openPopoverId={openRatingId}
                onOpenPopoverId={setOpenRatingId}
                onRate={(score) => onRateVideo(episode.imdbId, score)}
              />
              {videoRatings[episode.imdbId] ? (
                <button
                  type="button"
                  className="secondary-btn small"
                  onClick={() => onClearVideoRating(episode.imdbId)}
                >
                  Удалить оценку
                </button>
              ) : null}
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}

function PlayerPage({ setCurrentSeason, apiVideosBySeason, onEnsureSeasonVideos }) {
  const { seasonId, episodeId } = useParams();
  const location = useLocation();
  const navigate = useNavigate();
  const season = Number(seasonId || 1);
  const episode = Number(episodeId || 1);
  const safeSeason = season >= 1 && season <= CONSTANTS.TOTAL_SEASONS ? season : 1;
  const localEpisodes = CONSTANTS.SEASONS[safeSeason]?.episodes || [];
  const remoteVideos = apiVideosBySeason[safeSeason] || [];
  const episodes = localEpisodes.map((item) => {
    const remote = remoteVideos.find((video) => video.episodeNumber === item.id);
    return remote
      ? {
          ...item,
          title: remote.title || item.title,
          description: remote.description || item.description,
          filePath: remote.filePath || "",
          previewImageUrl: remote.previewImageUrl || ""
        }
      : item;
  });
  const routeEpisode = location.state?.episode;
  const selectedEpisode = episodes.find((item) => item.id === episode) || routeEpisode || episodes[0];
  const nextEpisodes = episodes.filter((item) => item.id > (selectedEpisode?.id || 0)).slice(0, 5);
  const nextEpisode = episodes.find((item) => item.id === (selectedEpisode?.id || 0) + 1) || null;
  const playerRef = useRef(null);
  const playerShellRef = useRef(null);
  const settingsAnchorRef = useRef(null);
  const settingsDropdownRef = useRef(null);
  const progressStorageKey = `s${safeSeason}e${selectedEpisode?.id || 1}`;
  const [resumeLabel, setResumeLabel] = useState("");
  const lastSavedSecondRef = useRef(-1);
  const [videoError, setVideoError] = useState(false);
  const [videoEnded, setVideoEnded] = useState(false);
  const [nearEpisodeEnd, setNearEpisodeEnd] = useState(false);
  const [showSkipIntro, setShowSkipIntro] = useState(false);
  const [introSkipUsed, setIntroSkipUsed] = useState(false);
  const [isPlaying, setIsPlaying] = useState(false);
  const [playbackUi, setPlaybackUi] = useState({ current: 0, duration: 0 });
  const [isShellFullscreen, setIsShellFullscreen] = useState(false);
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [speedSubmenuOpen, setSpeedSubmenuOpen] = useState(false);
  const [playbackSpeed, setPlaybackSpeed] = useState(1);
  const seasonIntroConfig = useMemo(() => getSeasonIntroConfig(safeSeason), [safeSeason]);

  const videoSrc = selectedEpisode?.filePath ? getMediaUrl(selectedEpisode.filePath) : "";
  const downloadFileName = useMemo(() => {
    const rawPath = selectedEpisode?.filePath || videoSrc;
    if (!rawPath) {
      return `bronytv-s${safeSeason}e${selectedEpisode?.id || 1}.mp4`;
    }
    const segment = rawPath.split("/").pop()?.split("?")[0] || rawPath;
    return segment.includes(".") ? segment : `${segment}.mp4`;
  }, [selectedEpisode?.filePath, selectedEpisode?.id, safeSeason, videoSrc]);
  const showNextEpisodeOverlay = Boolean(nextEpisode && videoSrc && (videoEnded || nearEpisodeEnd));

  useEffect(() => {
    setCurrentSeason(safeSeason);
    onEnsureSeasonVideos(safeSeason);
  }, [onEnsureSeasonVideos, safeSeason, setCurrentSeason]);

  useEffect(() => {
    setVideoError(false);
    setVideoEnded(false);
    setShowSkipIntro(false);
    setIntroSkipUsed(false);
    setNearEpisodeEnd(false);
    setIsPlaying(false);
    setPlaybackUi({ current: 0, duration: 0 });
    setIsShellFullscreen(false);
    setSettingsOpen(false);
    setSpeedSubmenuOpen(false);
    setPlaybackSpeed(1);
    lastSavedSecondRef.current = -1;
  }, [videoSrc, safeSeason, episode]);

  useEffect(() => {
    if (!settingsOpen) {
      return undefined;
    }
    const handlePointerDown = (event) => {
      if (settingsAnchorRef.current?.contains(event.target)) {
        return;
      }
      if (settingsDropdownRef.current?.contains(event.target)) {
        return;
      }
      setSettingsOpen(false);
      setSpeedSubmenuOpen(false);
    };
    document.addEventListener("mousedown", handlePointerDown);
    document.addEventListener("touchstart", handlePointerDown);
    return () => {
      document.removeEventListener("mousedown", handlePointerDown);
      document.removeEventListener("touchstart", handlePointerDown);
    };
  }, [settingsOpen]);

  useEffect(() => {
    const handleFullscreenChange = () => {
      const shell = playerShellRef.current;
      const active =
        document.fullscreenElement === shell ||
        document.webkitFullscreenElement === shell;
      setIsShellFullscreen(active);
    };
    document.addEventListener("fullscreenchange", handleFullscreenChange);
    document.addEventListener("webkitfullscreenchange", handleFullscreenChange);
    return () => {
      document.removeEventListener("fullscreenchange", handleFullscreenChange);
      document.removeEventListener("webkitfullscreenchange", handleFullscreenChange);
    };
  }, []);

  const goToNextEpisode = useCallback(() => {
    if (!nextEpisode) {
      return;
    }
    navigate(`/player/${safeSeason}/${nextEpisode.id}`, { state: { episode: nextEpisode } });
  }, [navigate, nextEpisode, safeSeason]);

  const skipIntro = useCallback(() => {
    const player = playerRef.current;
    if (!player || typeof player.currentTime !== "number") {
      return;
    }
    const targetTime = seasonIntroConfig.skipToTime;
    if (player.duration && !Number.isNaN(player.duration)) {
      player.currentTime = Math.min(targetTime, player.duration - 0.25);
    } else {
      player.currentTime = targetTime;
    }
    setIntroSkipUsed(true);
    setShowSkipIntro(false);
  }, [seasonIntroConfig.skipToTime]);

  const formatTime = (totalSeconds) => {
    const safe = Math.max(0, Math.floor(totalSeconds || 0));
    const minutes = String(Math.floor(safe / 60)).padStart(2, "0");
    const seconds = String(safe % 60).padStart(2, "0");
    return `${minutes}:${seconds}`;
  };

  const saveVideoProgress = useCallback(
    (timeSeconds, durationSeconds = 0) => {
      try {
        const current = readStorageObject(STORAGE_KEYS.VIDEO_PROGRESS);
        current[progressStorageKey] = {
          time: Math.max(0, Number(timeSeconds) || 0),
          duration: Math.max(0, Number(durationSeconds) || 0),
          updatedAt: Date.now()
        };
        localStorage.setItem(STORAGE_KEYS.VIDEO_PROGRESS, JSON.stringify(current));
      } catch (error) {
        // Ignore storage failures to avoid blocking playback.
      }
    },
    [progressStorageKey]
  );

  const togglePlayPause = useCallback(() => {
    const player = playerRef.current;
    if (!player) {
      return;
    }
    if (player.paused) {
      player.play().catch(() => {});
    } else {
      player.pause();
    }
  }, []);

  const applyPlaybackSpeed = useCallback((speed) => {
    const player = playerRef.current;
    if (player) {
      player.playbackRate = speed;
    }
    setPlaybackSpeed(speed);
  }, []);

  const handlePlaybackSpeedSelect = useCallback(
    (speed) => {
      applyPlaybackSpeed(speed);
    },
    [applyPlaybackSpeed]
  );

  const handleSeek = useCallback((event) => {
    const player = playerRef.current;
    const nextTime = Number(event.target.value);
    if (!player || Number.isNaN(nextTime)) {
      return;
    }
    player.currentTime = nextTime;
    setPlaybackUi((prev) => ({ ...prev, current: nextTime }));
  }, []);

  const handleVideoLoadedMetadata = useCallback(
    (event) => {
      const player = event.currentTarget;
      player.playbackRate = playbackSpeed;
      setPlaybackUi({
        current: player.currentTime || 0,
        duration: player.duration || 0
      });
      const progressMap = readStorageObject(STORAGE_KEYS.VIDEO_PROGRESS);
      const saved = progressMap[progressStorageKey];
      if (!saved || typeof saved.time !== "number") {
        setResumeLabel("");
        return;
      }

      const targetTime = Math.max(0, saved.time);
      if (player.duration && targetTime > 0 && targetTime < player.duration - 1) {
        player.currentTime = targetTime;
      }
      setResumeLabel(`Продолжить с ${formatTime(targetTime)}`);
    },
    [playbackSpeed, progressStorageKey]
  );

  const handleVideoTimeUpdate = useCallback(
    (event) => {
      const currentTime = event.currentTarget.currentTime || 0;
      const inIntroWindow =
        !introSkipUsed &&
        currentTime >= seasonIntroConfig.startTime &&
        currentTime < seasonIntroConfig.endTime;
      setShowSkipIntro(inIntroWindow);
      setPlaybackUi({
        current: currentTime,
        duration: event.currentTarget.duration || 0
      });

      const duration = event.currentTarget.duration;
      if (duration && !Number.isNaN(duration) && duration > 0) {
        const remaining = duration - currentTime;
        setNearEpisodeEnd(remaining > 0 && remaining <= NEXT_EPISODE_REMAINING_SECONDS);
      } else {
        setNearEpisodeEnd(false);
      }

      const currentSecond = Math.floor(currentTime);
      if (currentSecond === lastSavedSecondRef.current || currentSecond % 2 !== 0) {
        return;
      }
      lastSavedSecondRef.current = currentSecond;
      saveVideoProgress(currentTime, event.currentTarget.duration || 0);
    },
    [introSkipUsed, saveVideoProgress, seasonIntroConfig]
  );

  const handleVideoEnded = useCallback(() => {
    setVideoEnded(true);
    setNearEpisodeEnd(true);
    setShowSkipIntro(false);
  }, []);

  const handleVideoPause = useCallback(
    (event) => {
      saveVideoProgress(event.currentTarget.currentTime || 0, event.currentTarget.duration || 0);
    },
    [saveVideoProgress]
  );

  const toggleShellFullscreen = useCallback(async () => {
    const shellNode = playerShellRef.current;
    if (!shellNode) {
      return;
    }

    const activeElement = document.fullscreenElement || document.webkitFullscreenElement;
    const isActive = activeElement === shellNode;

    try {
      if (isActive) {
        if (document.exitFullscreen) {
          await document.exitFullscreen();
        } else if (document.webkitExitFullscreen) {
          document.webkitExitFullscreen();
        }
        return;
      }
      if (shellNode.requestFullscreen) {
        await shellNode.requestFullscreen();
      } else if (shellNode.webkitRequestFullscreen) {
        shellNode.webkitRequestFullscreen();
      } else if (shellNode.msRequestFullscreen) {
        shellNode.msRequestFullscreen();
      }
    } catch (error) {
      // Fail silently if fullscreen is blocked by browser policy.
    }
  }, []);

  return (
    <section className="panel player-panel">
      <h2>
        Плеер | Сезон {safeSeason}, серия {selectedEpisode?.id || 1}
      </h2>
      {resumeLabel ? <p className="muted">{resumeLabel}</p> : null}
      {videoSrc ? (
        <div className="player-shell" ref={playerShellRef}>
          <video
            key={videoSrc}
            ref={playerRef}
            className="video-player video-large"
            playsInline
            preload="metadata"
            src={videoSrc}
            onClick={togglePlayPause}
            onLoadedMetadata={handleVideoLoadedMetadata}
            onTimeUpdate={handleVideoTimeUpdate}
            onPause={(event) => {
              setIsPlaying(false);
              handleVideoPause(event);
            }}
            onPlay={() => {
              setIsPlaying(true);
              setVideoEnded(false);
            }}
            onEnded={handleVideoEnded}
            onError={() => setVideoError(true)}
          />
          <div className="player-custom-controls" aria-label="Управление плеером">
            {showSkipIntro ? (
              <button type="button" className="player-overlay-btn skip-intro-btn" onClick={skipIntro}>
                <SkipForward size={18} />
                <span>Пропустить заставку</span>
              </button>
            ) : null}
            {showNextEpisodeOverlay ? (
              <button type="button" className="player-overlay-btn next-episode-btn" onClick={goToNextEpisode}>
                <span>Следующая серия</span>
                <ChevronRight size={18} />
              </button>
            ) : null}
            <div className="player-chrome-bar">
              <button
                type="button"
                className="player-chrome-btn"
                onClick={togglePlayPause}
                aria-label={isPlaying ? "Пауза" : "Воспроизведение"}
              >
                {isPlaying ? <Pause size={20} /> : <Play size={20} />}
              </button>
              <input
                type="range"
                className="player-timeline"
                min={0}
                max={playbackUi.duration || 0}
                step={0.1}
                value={Math.min(playbackUi.current, playbackUi.duration || 0)}
                onChange={handleSeek}
                aria-label="Позиция воспроизведения"
              />
              <span className="player-time">
                {formatTime(playbackUi.current)} / {formatTime(playbackUi.duration)}
              </span>
              <div className="player-chrome-bar-right">
                <div className="player-settings-wrap" ref={settingsAnchorRef}>
                  <button
                    type="button"
                    className="player-chrome-btn"
                    onClick={() => {
                      setSettingsOpen((prev) => !prev);
                      setSpeedSubmenuOpen(false);
                    }}
                    aria-label="Настройки плеера"
                    aria-expanded={settingsOpen}
                  >
                    <MoreVertical size={20} />
                  </button>
                  {settingsOpen ? (
                    <div className="player-settings-dropdown" ref={settingsDropdownRef} role="menu">
                      <button
                        type="button"
                        className="player-settings-row"
                        onClick={() => setSpeedSubmenuOpen((prev) => !prev)}
                        aria-expanded={speedSubmenuOpen}
                      >
                        <span>Скорость воспроизведения</span>
                        <ChevronRight
                          size={16}
                          className={`player-settings-chevron${speedSubmenuOpen ? " is-open" : ""}`}
                        />
                      </button>
                      {speedSubmenuOpen ? (
                        <div className="player-settings-submenu" role="group" aria-label="Скорость воспроизведения">
                          {PLAYBACK_SPEED_OPTIONS.map((option) => (
                            <button
                              key={option.value}
                              type="button"
                              className={`player-settings-option${
                                playbackSpeed === option.value ? " is-active" : ""
                              }`}
                              onClick={() => handlePlaybackSpeedSelect(option.value)}
                            >
                              <span>{option.label}</span>
                              {playbackSpeed === option.value ? <Check size={16} /> : null}
                            </button>
                          ))}
                        </div>
                      ) : null}
                      <a
                        className="player-settings-row player-settings-download"
                        href={videoSrc}
                        download={downloadFileName}
                        onClick={() => {
                          setSettingsOpen(false);
                          setSpeedSubmenuOpen(false);
                        }}
                      >
                        <Download size={16} />
                        <span>Скачать видео</span>
                      </a>
                    </div>
                  ) : null}
                </div>
                <button
                  type="button"
                  className="player-chrome-btn"
                  onClick={toggleShellFullscreen}
                  aria-label={isShellFullscreen ? "Выйти из полноэкранного режима" : "Полноэкранный режим"}
                >
                  {isShellFullscreen ? <Minimize size={20} /> : <Maximize size={20} />}
                </button>
              </div>
            </div>
          </div>
        </div>
      ) : (
        <div ref={playerRef} className="video-placeholder video-large">
          {remoteVideos.length === 0 ? "Загрузка списка серий…" : "Видеофайл для этой серии не найден"}
        </div>
      )}
      {videoError ? (
        <p className="muted video-error-msg">Не удалось загрузить видео. Проверьте подключение или попробуйте позже.</p>
      ) : null}
      <h3>{selectedEpisode?.title || "Серия недоступна"}</h3>
      <p className="muted">{selectedEpisode?.description || "Описание недоступно."}</p>
      <div className="button-row">
        <button type="button" className="primary-btn" onClick={toggleShellFullscreen}>
          <Maximize size={16} />
          <span>На весь экран</span>
        </button>
        <Link className="secondary-btn" to={`/season/${safeSeason}`}>
          Назад к сезону
        </Link>
      </div>
      <div className="next-videos">
        <h3>Следующие видео</h3>
        {nextEpisodes.length === 0 ? (
          <p className="muted">Это последняя серия сезона.</p>
        ) : (
          nextEpisodes.map((item) => (
            <Link
              key={item.id}
              className="next-video-card"
              to={`/player/${safeSeason}/${item.id}`}
              state={{ episode: item }}
            >
              <div className="episode-main">
                <h3>
                  Серия {item.id}: {item.title}
                </h3>
                <p className="muted">
                  {item.genre} | {item.duration}
                </p>
              </div>
              <PlayCircle size={18} />
            </Link>
          ))
        )}
      </div>
    </section>
  );
}

export default function App() {
  const location = useLocation();
  const [currentSeason, setCurrentSeason] = useState(1);
  const [currentPage, setCurrentPage] = useState("home");
  const [theme, setTheme] = useState(() => localStorage.getItem(STORAGE_KEYS.THEME) || "light");
  const [seasonRatings, setSeasonRatings] = useState(() => readStorageObject(STORAGE_KEYS.SEASON_RATINGS));
  const [videoRatings, setVideoRatings] = useState(() => readStorageObject(STORAGE_KEYS.VIDEO_RATINGS));
  const [apiSeasons, setApiSeasons] = useState({});
  const [apiVideosBySeason, setApiVideosBySeason] = useState({});

  useEffect(() => {
    setCurrentPage(getPageFromPath(location.pathname));
  }, [location.pathname]);

  useEffect(() => {
    document.body.dataset.theme = theme;
    localStorage.setItem(STORAGE_KEYS.THEME, theme);
  }, [theme]);

  useEffect(() => {
    const loadSeasons = async () => {
      try {
        const response = await fetch(apiUrl("/api/season"));
        if (!response.ok) {
          return;
        }
        const seasons = await response.json();
        const map = seasons.reduce((acc, season) => {
          acc[season.number] = season;
          return acc;
        }, {});
        setApiSeasons(map);
      } catch (error) {
        // Keep local fallback data if backend is unavailable.
      }
    };
    loadSeasons();
  }, []);

  const ensureSeasonVideos = useCallback(async (seasonNumber) => {
    if (apiVideosBySeason[seasonNumber]) {
      return;
    }
    try {
      const response = await fetch(apiUrl(`/api/video/season/${seasonNumber}`));
      if (!response.ok) {
        return;
      }
      const videos = await response.json();
      setApiVideosBySeason((prev) => ({ ...prev, [seasonNumber]: videos }));
    } catch (error) {
      // Keep local fallback data if backend is unavailable.
    }
  }, [apiVideosBySeason]);

  const handleRateSeason = (seasonNumber, score) => {
    setSeasonRatings((prev) => {
      const next = { ...prev, [String(seasonNumber)]: score };
      localStorage.setItem(STORAGE_KEYS.SEASON_RATINGS, JSON.stringify(next));
      return next;
    });
  };

  const handleRateVideo = (videoId, score) => {
    setVideoRatings((prev) => {
      const next = { ...prev, [String(videoId)]: score };
      localStorage.setItem(STORAGE_KEYS.VIDEO_RATINGS, JSON.stringify(next));
      return next;
    });
  };

  const handleClearSeasonRating = (seasonNumber) => {
    setSeasonRatings((prev) => {
      const next = { ...prev };
      delete next[String(seasonNumber)];
      localStorage.setItem(STORAGE_KEYS.SEASON_RATINGS, JSON.stringify(next));
      return next;
    });
  };

  const handleClearVideoRating = (videoId) => {
    setVideoRatings((prev) => {
      const next = { ...prev };
      delete next[String(videoId)];
      localStorage.setItem(STORAGE_KEYS.VIDEO_RATINGS, JSON.stringify(next));
      return next;
    });
  };

  const content = useMemo(
    () => (
      <Routes>
        <Route
          path="/"
          element={
            <HomePage
              videoRatings={videoRatings}
              onRateVideo={handleRateVideo}
              onClearVideoRating={handleClearVideoRating}
            />
          }
        />
        <Route
          path="/season/:seasonId"
          element={
            <SeasonPage
              setCurrentSeason={setCurrentSeason}
              seasonRatings={seasonRatings}
              videoRatings={videoRatings}
              onRateSeason={handleRateSeason}
              onRateVideo={handleRateVideo}
              onClearSeasonRating={handleClearSeasonRating}
              onClearVideoRating={handleClearVideoRating}
              apiSeasons={apiSeasons}
              apiVideosBySeason={apiVideosBySeason}
              onEnsureSeasonVideos={ensureSeasonVideos}
            />
          }
        />
        <Route
          path="/player/:seasonId/:episodeId"
          element={
            <PlayerPage
              setCurrentSeason={setCurrentSeason}
              apiVideosBySeason={apiVideosBySeason}
              onEnsureSeasonVideos={ensureSeasonVideos}
            />
          }
        />
        <Route
          path="*"
          element={
            <HomePage
              videoRatings={videoRatings}
              onRateVideo={handleRateVideo}
              onClearVideoRating={handleClearVideoRating}
            />
          }
        />
      </Routes>
    ),
    [apiSeasons, apiVideosBySeason, ensureSeasonVideos, seasonRatings, videoRatings]
  );

  return (
    <div className="page-frame">
      <div className="video-blur video-blur-left" />
      <div className="video-blur video-blur-right" />
      <div className="app-shell">
        <Sidebar
          currentSeason={currentSeason}
          currentPage={currentPage}
          theme={theme}
          onToggleTheme={() => setTheme((prev) => (prev === "dark" ? "light" : "dark"))}
        />
        <main className="content">{content}</main>
      </div>
    </div>
  );
}
