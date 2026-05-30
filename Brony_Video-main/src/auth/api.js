const API_BASE_URL = (process.env.REACT_APP_API_BASE_URL || "").replace(/\/$/, "");

export const apiUrl = (path) => {
  const normalized = path.startsWith("/") ? path : `/${path}`;
  if (!API_BASE_URL) {
    return normalized;
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
