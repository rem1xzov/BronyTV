import React, { useState } from "react";
import { RACE_OPTIONS } from "../auth/AuthContext";

export default function RaceSelectionModal({ open, onConfirm }) {
  const [selectedRace, setSelectedRace] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");

  if (!open) {
    return null;
  }

  const handleSubmit = async () => {
    if (!selectedRace) {
      setError("Выберите расу — это решение навсегда.");
      return;
    }
    setSubmitting(true);
    setError("");
    try {
      await onConfirm(selectedRace);
    } catch (submitError) {
      setError("Не удалось сохранить выбор. Попробуйте снова.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="race-modal-backdrop" role="dialog" aria-modal="true" aria-labelledby="race-modal-title">
      <div className="race-modal-card">
        <h2 id="race-modal-title">Выберите свою расу</h2>
        <p className="muted race-modal-subtitle">
          Этот выбор делается один раз и навсегда — изменить его позже будет невозможно.
        </p>
        <div className="race-modal-grid">
          {RACE_OPTIONS.map((race) => (
            <button
              key={race.id}
              type="button"
              className={`race-option${selectedRace === race.id ? " is-selected" : ""}`}
              onClick={() => setSelectedRace(race.id)}
              disabled={submitting}
            >
              <strong>{race.title}</strong>
              <span className="muted">{race.subtitle}</span>
              <p>{race.description}</p>
            </button>
          ))}
        </div>
        {error ? <p className="race-modal-error">{error}</p> : null}
        <button
          type="button"
          className="primary-btn race-modal-submit"
          onClick={handleSubmit}
          disabled={submitting || !selectedRace}
        >
          {submitting ? "Сохранение…" : "Подтвердить выбор навсегда"}
        </button>
      </div>
    </div>
  );
}
