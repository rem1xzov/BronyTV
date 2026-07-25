export const PASSWORD_MIN_LENGTH = 6;

export const validateChangePassword = (newPassword, confirmPassword) => {
  const next = String(newPassword ?? "");
  const confirm = String(confirmPassword ?? "");

  if (!next.trim() || !confirm.trim()) {
    return { valid: false, error: "Заполните оба поля пароля." };
  }

  if (next !== confirm) {
    return { valid: false, error: "Пароли не совпадают" };
  }

  if (next.length < PASSWORD_MIN_LENGTH) {
    return {
      valid: false,
      error: `Пароль должен содержать минимум ${PASSWORD_MIN_LENGTH} символов.`
    };
  }

  return { valid: true, error: "" };
};
