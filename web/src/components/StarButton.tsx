interface Props {
  active: boolean
  onToggle: () => void
}

// Кнопка-зірочка для додавання/зняття учасника з обраних. Активна — жовта ★,
// неактивна — порожня ☆. stopPropagation на випадок кліку всередині клікабельних
// рядків. aria-label/aria-pressed — для доступності та зрозумілості скрінрідерам.
export function StarButton({ active, onToggle }: Props) {
  return (
    <button
      type="button"
      className={`star${active ? ' on' : ''}`}
      aria-label={active ? 'Прибрати з обраних' : 'Додати в обрані'}
      aria-pressed={active}
      title={active ? 'Прибрати з обраних' : 'Додати в обрані'}
      onClick={(e) => {
        e.stopPropagation()
        onToggle()
      }}
    >
      {active ? '★' : '☆'}
    </button>
  )
}
