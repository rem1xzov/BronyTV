// Same-origin /api in production (nginx reverse proxy). Override via REACT_APP_API_BASE_URL for dev.
const API_BASE_URL = (process.env.REACT_APP_API_BASE_URL ?? "/api").replace(/\/$/, "");

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

export const apiUpload = (path, formData, options = {}) =>
  fetch(apiUrl(path), {
    method: "POST",
    credentials: "include",
    body: formData,
    ...options
  });

export const apiFetch = (path, options = {}) => {
  const { credentials: _ignoredCredentials, headers: optionHeaders, ...restOptions } = options;
  const headers = { ...(optionHeaders || {}) };
  if (restOptions.body && !headers["Content-Type"]) {
    headers["Content-Type"] = "application/json";
  }

  return fetch(apiUrl(path), {
    ...restOptions,
    credentials: "include",
    headers
  });
};
