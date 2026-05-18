import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Home, Maximize, Moon, PlayCircle, Star, Sun, Tv } from "lucide-react";
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
const STORAGE_KEYS = {
  SEASON_RATINGS: "bronytv-season-ratings",
  VIDEO_RATINGS: "bronytv-video-ratings",
  THEME: "bronytv-theme",
  VIDEO_PROGRESS: "bronytv-video-progress"
};

const SEASON_PREVIEW_MAP = {
  1: "s1e1.jpg",
  2: "s2e1.jpg",
  3: "s3e1.jpg",
  4: "s4e1.jpg",
  5: "s5e1.jpg",
  6: "s6e1.jpg",
  7: "s7e1.jpg",
  8: "s8e1.jpg",
  9: "s9e1.jpg"
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

function RatingButton({ value, onRate, label = "Оценить" }) {
  const [open, setOpen] = useState(false);

  return (
    <div className="rating-widget">
      <button type="button" className="rate-btn" onClick={() => setOpen((prev) => !prev)}>
        <Star size={14} />
        <span>{value ? `${value}/10` : label}</span>
      </button>
      {open ? (
        <div className="rating-popup">
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
      ) : null}
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
  const remoteSeasonData = apiSeasons[safeSeason];
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
  const seasonPreviewFile = SEASON_PREVIEW_MAP[safeSeason] || null;
  const seasonPreviewUrl = remoteSeasonData?.posterPath
    ? toAbsoluteApiUrl(remoteSeasonData.posterPath)
    : seasonPreviewFile
      ? getPublicAssetUrl(`assets/episodes/${seasonPreviewFile}`)
      : null;

  return (
    <section className="panel">
      <div
        className={`season-banner ${seasonPreviewUrl ? "has-season-preview" : ""}`}
        style={seasonPreviewUrl ? { "--season-preview-url": `url("${seasonPreviewUrl}")` } : undefined}
      >
        <h2>{seasonData?.title || `Сезон ${safeSeason}`}</h2>
        <p className="muted">{seasonData?.description}</p>
        <div className="button-row">
          <RatingButton
            value={seasonRatings[String(safeSeason)]}
            label="Оценить сезон"
            onRate={(score) => onRateSeason(safeSeason, score)}
          />
          {seasonRatings[String(safeSeason)] ? (
            <button type="button" className="secondary-btn" onClick={() => onClearSeasonRating(safeSeason)}>
              Удалить оценку сезона
            </button>
          ) : null}
        </div>
      </div>
      <div className="episode-list episode-grid scrollable">
        {episodes.map((episode) => (
          <div className="episode-card" key={`s${safeSeason}-e${episode.id}`}>
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
  const playerRef = useRef(null);
  const progressStorageKey = `s${safeSeason}e${selectedEpisode?.id || 1}`;
  const [resumeLabel, setResumeLabel] = useState("");
  const lastSavedSecondRef = useRef(-1);
  const [videoError, setVideoError] = useState(false);

  const videoSrc = selectedEpisode?.filePath ? getMediaUrl(selectedEpisode.filePath) : "";

  useEffect(() => {
    setCurrentSeason(safeSeason);
    onEnsureSeasonVideos(safeSeason);
  }, [onEnsureSeasonVideos, safeSeason, setCurrentSeason]);

  useEffect(() => {
    setVideoError(false);
    lastSavedSecondRef.current = -1;
  }, [videoSrc, safeSeason, episode]);

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

  const handleVideoLoadedMetadata = useCallback(
    (event) => {
      const player = event.currentTarget;
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
    [progressStorageKey]
  );

  const handleVideoTimeUpdate = useCallback(
    (event) => {
      const currentTime = event.currentTarget.currentTime || 0;
      const currentSecond = Math.floor(currentTime);
      if (currentSecond === lastSavedSecondRef.current || currentSecond % 2 !== 0) {
        return;
      }
      lastSavedSecondRef.current = currentSecond;
      saveVideoProgress(currentTime, event.currentTarget.duration || 0);
    },
    [saveVideoProgress]
  );

  const handleVideoPause = useCallback(
    (event) => {
      saveVideoProgress(event.currentTarget.currentTime || 0, event.currentTarget.duration || 0);
    },
    [saveVideoProgress]
  );

  const openFullscreen = async () => {
    const playerNode = playerRef.current;
    if (!playerNode) {
      return;
    }

    try {
      if (playerNode.requestFullscreen) {
        await playerNode.requestFullscreen();
      } else if (playerNode.webkitRequestFullscreen) {
        playerNode.webkitRequestFullscreen();
      } else if (playerNode.msRequestFullscreen) {
        playerNode.msRequestFullscreen();
      }
    } catch (error) {
      // Fail silently if fullscreen is blocked by browser policy.
    }
  };

  return (
    <section className="panel player-panel">
      <h2>
        Плеер | Сезон {safeSeason}, серия {selectedEpisode?.id || 1}
      </h2>
      {resumeLabel ? <p className="muted">{resumeLabel}</p> : null}
      {videoSrc ? (
        <video
          key={videoSrc}
          ref={playerRef}
          className="video-player video-large"
          controls
          playsInline
          preload="metadata"
          src={videoSrc}
          onLoadedMetadata={handleVideoLoadedMetadata}
          onTimeUpdate={handleVideoTimeUpdate}
          onPause={handleVideoPause}
          onError={() => setVideoError(true)}
        />
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
        <button type="button" className="primary-btn" onClick={openFullscreen}>
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
