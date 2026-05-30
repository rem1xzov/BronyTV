import { RACE_OPTIONS } from "./AuthContext";

export const RACE_LABEL_BY_ID = RACE_OPTIONS.reduce((acc, race) => {
  acc[race.id] = race.label;
  return acc;
}, {});

export const getRaceLabel = (raceId) => RACE_LABEL_BY_ID[raceId] || raceId || "—";

export const getRaceBadgeClassName = (raceId) => {
  const safeId = String(raceId || "").toLowerCase();
  if (safeId === "pegasus" || safeId === "unicorn" || safeId === "earth_pony") {
    return `profile-race-badge profile-race-badge--${safeId}`;
  }
  return "profile-race-badge profile-race-badge--default";
};
