export const normalizeAuthUser = (raw) => {
  if (!raw || typeof raw !== "object") {
    return null;
  }

  const email = raw.email ?? raw.Email ?? "";
  const race = raw.race ?? raw.Race ?? "";
  const id = raw.id ?? raw.Id;

  if (!email && !id) {
    return null;
  }

  return {
    id,
    email,
    race: typeof race === "string" ? race : String(race ?? "")
  };
};
