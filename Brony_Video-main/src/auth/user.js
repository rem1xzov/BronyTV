export const normalizeAuthUser = (raw) => {
  if (!raw || typeof raw !== "object") {
    return null;
  }

  const email = raw.email ?? raw.Email ?? "";
  const race = raw.race ?? raw.Race ?? "";
  const usernameRaw = raw.username ?? raw.Username ?? null;
  const id = raw.id ?? raw.Id;

  if (!email && !id) {
    return null;
  }

  const username =
    usernameRaw == null || usernameRaw === ""
      ? null
      : String(usernameRaw).trim().toLowerCase() || null;

  return {
    id,
    email,
    username,
    race: typeof race === "string" ? race : String(race ?? "")
  };
};
