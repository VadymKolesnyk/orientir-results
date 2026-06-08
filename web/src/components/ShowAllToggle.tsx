interface Props {
  on: boolean
  onToggle: () => void
}

// Перемикач «Всі колонки» — показується ЛИШЕ на малому екрані (CSS). Вмикає
// показ усіх колонок, навіть прихованих на вузькому екрані.
export function ShowAllToggle({ on, onToggle }: Props) {
  return (
    <button
      type="button"
      className={`show-all-btn${on ? ' on' : ''}`}
      onClick={onToggle}
      aria-pressed={on}
      title="Показати всі колонки"
    >
      {on ? '☑' : '☐'} Всі колонки
    </button>
  )
}
