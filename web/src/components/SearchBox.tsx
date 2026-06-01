interface Props {
  value: string
  onChange: (v: string) => void
}

export function SearchBox({ value, onChange }: Props) {
  return (
    <input
      className="q"
      id="q"
      placeholder="Пошук: ПІБ / команда / №…"
      value={value}
      onChange={(e) => onChange(e.target.value)}
    />
  )
}
