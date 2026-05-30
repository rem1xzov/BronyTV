import { normalizeRaceKey, RACE_DEFAULT_EMOJI } from "./race";

const splitGraphemes = (text) => {
  const value = String(text ?? "");
  if (typeof Intl !== "undefined" && typeof Intl.Segmenter === "function") {
    const segmenter = new Intl.Segmenter(undefined, { granularity: "grapheme" });
    return [...segmenter.segment(value)].map((part) => part.segment);
  }

  const elements = [];
  for (let index = 0; index < value.length;) {
    const next = value.codePointAt(index);
    if (next === undefined) {
      break;
    }
    const char = String.fromCodePoint(next);
    elements.push(char);
    index += char.length;
  }
  return elements;
};

export const validateAvatarEmoji = (raw) => {
  const trimmed = String(raw ?? "").trim();

  if (!trimmed) {
    return { valid: false, error: "Выберите эмодзи." };
  }

  const graphemes = splitGraphemes(trimmed);
  if (graphemes.length !== 1) {
    return { valid: false, error: "Укажите ровно один эмодзи." };
  }

  return { valid: true, value: graphemes[0], error: "" };
};

export const resolveAvatarEmoji = (user) => {
  const custom = user?.avatarEmoji?.trim();
  if (custom) {
    return custom;
  }

  const raceKey = normalizeRaceKey(user?.race);
  if (RACE_DEFAULT_EMOJI[raceKey]) {
    return RACE_DEFAULT_EMOJI[raceKey];
  }

  const username = user?.username?.trim();
  if (username) {
    return username[0].toUpperCase();
  }

  return RACE_DEFAULT_EMOJI.unknown;
};
