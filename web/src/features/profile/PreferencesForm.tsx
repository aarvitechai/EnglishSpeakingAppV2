import { useState, type FormEvent } from 'react'
import type { Preferences } from '../../shared/api/endpoints'
import { englishLevels, learningGoals, type LearningGoal } from '../../shared/api/schemas'
import { Button } from '../../shared/ui/Form'
import { MAX_GOALS } from './preferences'
import { goalLabels, levelLabels, supportLanguages } from '../../shared/ui/labels'

interface Props {
  initial: Preferences
  submitLabel: string
  busy: boolean
  onSubmit: (prefs: Preferences) => void
}

export function PreferencesForm({ initial, submitLabel, busy, onSubmit }: Props) {
  const [prefs, setPrefs] = useState<Preferences>(initial)
  const [goalError, setGoalError] = useState<string | null>(null)

  function toggleGoal(goal: LearningGoal) {
    setGoalError(null)
    setPrefs((p) => {
      if (p.goals.includes(goal)) return { ...p, goals: p.goals.filter((g) => g !== goal) }
      if (p.goals.length >= MAX_GOALS) {
        setGoalError(`Choose up to ${MAX_GOALS} goals.`)
        return p
      }
      return { ...p, goals: [...p.goals, goal] }
    })
  }

  function submit(event: FormEvent) {
    event.preventDefault()
    if (prefs.goals.length === 0) {
      setGoalError('Choose at least one goal.')
      return
    }
    onSubmit(prefs)
  }

  return (
    <form onSubmit={submit} className="stack">
      <fieldset className="fieldset">
        <legend>How comfortable are you speaking English?</legend>
        <div className="option-grid">
          {englishLevels.map((level) => (
            <label
              key={level}
              className={`option-card${prefs.level === level ? ' option-card--selected' : ''}`}
            >
              <input
                type="radio"
                name="level"
                value={level}
                checked={prefs.level === level}
                onChange={() => setPrefs((p) => ({ ...p, level }))}
              />
              <strong>{levelLabels[level].title}</strong>
              <span className="muted">{levelLabels[level].description}</span>
            </label>
          ))}
        </div>
        <p className="field__hint">
          This is your own choice, not a test result. You can change it any time.
        </p>
      </fieldset>

      <fieldset className="fieldset">
        <legend>What do you want to practise? (up to {MAX_GOALS})</legend>
        <div className="chip-group">
          {learningGoals.map((goal) => (
            <label
              key={goal}
              className={`chip${prefs.goals.includes(goal) ? ' chip--selected' : ''}`}
            >
              <input
                type="checkbox"
                checked={prefs.goals.includes(goal)}
                onChange={() => toggleGoal(goal)}
              />
              {goalLabels[goal]}
            </label>
          ))}
        </div>
        {goalError && (
          <p className="field__error" role="alert">
            {goalError}
          </p>
        )}
      </fieldset>

      <div className="field">
        <label htmlFor="support-language" className="field__label">
          Language for short explanations
        </label>
        <select
          id="support-language"
          className="field__input"
          value={prefs.supportLanguage}
          onChange={(e) => setPrefs((p) => ({ ...p, supportLanguage: e.target.value }))}
        >
          {supportLanguages.map((l) => (
            <option key={l.code} value={l.code}>
              {l.name}
            </option>
          ))}
        </select>
        <p className="field__hint">
          The coach speaks English, with brief help in this language when you need it.
        </p>
      </div>

      <div className="field">
        <label htmlFor="daily-target" className="field__label">
          Daily practice goal: <strong>{prefs.dailyTargetMinutes} minutes</strong>
        </label>
        <input
          id="daily-target"
          type="range"
          min={5}
          max={60}
          step={5}
          value={prefs.dailyTargetMinutes}
          onChange={(e) => setPrefs((p) => ({ ...p, dailyTargetMinutes: Number(e.target.value) }))}
        />
      </div>

      <Button type="submit" busy={busy}>
        {submitLabel}
      </Button>
    </form>
  )
}
