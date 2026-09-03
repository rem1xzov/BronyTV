import React, { useState } from "react";
import { Eye, EyeOff } from "lucide-react";

/**
 * Поле ввода пароля с кнопкой «глазик» (показать/скрыть).
 * По умолчанию пароль скрыт (type="password"). Клик по иконке переключает видимость.
 */
export default function PasswordInput({ value, onChange, style, ...rest }) {
  const [visible, setVisible] = useState(false);

  return (
    <div className="password-input-wrap">
      <input
        type={visible ? "text" : "password"}
        value={value}
        onChange={onChange}
        style={{ paddingRight: 46, ...style }}
        {...rest}
      />
      <button
        type="button"
        className="password-input-toggle"
        onClick={() => setVisible((prev) => !prev)}
        aria-label={visible ? "Скрыть пароль" : "Показать пароль"}
        tabIndex={-1}
      >
        {visible ? <EyeOff size={18} /> : <Eye size={18} />}
      </button>
    </div>
  );
}
