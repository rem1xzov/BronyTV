import { RACE_OPTIONS } from "./AuthContext";

export const RACE_STYLES = {
  pegasus: {
    label: "пегасы",
    badgeClass: "profile-race-badge profile-race-badge--pegasus",
    avatarClass: "profile-avatar-circle profile-avatar-circle--pegasus"
  },
  unicorn: {
    label: "единороги",
    badgeClass: "profile-race-badge profile-race-badge--unicorn",
    avatarClass: "profile-avatar-circle profile-avatar-circle--unicorn"
  },
  earth_pony: {
    label: "земные пони",
    badgeClass: "profile-race-badge profile-race-badge--earth_pony",
    avatarClass: "profile-avatar-circle profile-avatar-circle--earth_pony"
  }
};

export const RACE_DEFAULT_EMOJI = {
  pegasus: "🪶",
  unicorn: "🦄",
  earth_pony: "🐎",
  unknown: "🐴"
};

export const RACE_LABEL_BY_ID = RACE_OPTIONS.reduce((acc, race) => {
  acc[race.id] = race.label;
  acc[race.id.toLowerCase()] = race.label;
  return acc;
}, {});

export const normalizeRaceKey = (race) => {
  const raw = String(race ?? "")
    .trim()
    .toLowerCase()
    .replace(/\s+/g, "_")
    .replace(/-/g, "_");

  if (!raw) {
    return "unknown";
  }

  if (raw === "earthpony" || raw === "earth") {
    return "earth_pony";
  }

  if (RACE_STYLES[raw]) {
    return raw;
  }

  return "unknown";
};

export const getRaceDisplay = (race) => {
  const raceKey = normalizeRaceKey(race);
  const known = RACE_STYLES[raceKey];
  if (known) {
    return known;
  }

  const fallbackLabel =
    RACE_LABEL_BY_ID[String(race ?? "").toLowerCase()] ||
    (typeof race === "string" && race.trim() ? race.trim() : "Не указана");

  return {
    label: fallbackLabel,
    badgeClass: "profile-race-badge profile-race-badge--default",
    avatarClass: "profile-avatar-circle profile-avatar-circle--default"
  };
};

export const getRaceAvatarClassName = (race) => {
  const raceKey = normalizeRaceKey(race);
  const known = RACE_STYLES[raceKey];
  if (known?.avatarClass) {
    return known.avatarClass;
  }
  return "profile-avatar-circle profile-avatar-circle--default";
};

export const getRaceLabel = (raceId) => getRaceDisplay(raceId).label;

export const getRaceBadgeClassName = (raceId) => getRaceDisplay(raceId).badgeClass;
