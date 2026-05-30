export const USERNAME_MIN_LENGTH = 4;
export const USERNAME_MAX_LENGTH = 25;
export const USERNAME_PATTERN = /^[a-zA-Z0-9_]{4,25}$/;

export const validateUsername = (raw) => {
  const value = String(raw ?? "").trim();

  if (!value) {
    return { valid: false, error: "Укажите юзернейм." };
  }

  if (value.length < USERNAME_MIN_LENGTH || value.length > USERNAME_MAX_LENGTH) {
    return {
      valid: false,
      error: `Юзернейм: от ${USERNAME_MIN_LENGTH} до ${USERNAME_MAX_LENGTH} символов.`
    };
  }

  if (!USERNAME_PATTERN.test(value)) {
    return {
      valid: false,
      error: "Только латинские буквы, цифры и подчёркивание (_)."
    };
  }

  return { valid: true, value: value.toLowerCase(), error: "" };
};
