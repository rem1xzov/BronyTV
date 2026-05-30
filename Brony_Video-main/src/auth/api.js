const API_BASE_URL = (process.env.REACT_APP_API_BASE_URL || "").replace(/\/$/, "");

export const apiUrl = (path) => {
  let normalized = path.startsWith("/") ? path : `/${path}`;

  // REACT_APP_API_BASE_URL is often "/api" — drop a leading "/api" from paths to avoid "/api/api/...".
  if (API_BASE_URL && (normalized.startsWith("/api/") || normalized === "/api")) {
    normalized = normalized === "/api" ? "/" : normalized.slice(4);
  }

  if (!API_BASE_URL) {
    return normalized.startsWith("/api/") || normalized === "/api"
      ? normalized
      : `/api${normalized}`;
  }

  return `${API_BASE_URL}${normalized}`;
};

export const apiFetch = (path, options = {}) => {
  const headers = { ...(options.headers || {}) };
  if (options.body && !headers["Content-Type"]) {
    headers["Content-Type"] = "application/json";
  }

  return fetch(apiUrl(path), {
    ...options,
    credentials: "include",
    headers
  });
};
