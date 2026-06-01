interface Props {
  names: string[]
  activeGrp: string
  onSelect: (grp: string) => void
}

// Вкладки груп (за алфавітом). Клік перемикає активну групу.
export function GroupTabs({ names, activeGrp, onSelect }: Props) {
  return (
    <div className="tabs" id="tabs">
      {names.map((n) => (
        <a
          key={n}
          className={n === activeGrp ? 'on' : ''}
          onClick={() => {
            if (n !== activeGrp) onSelect(n)
          }}
        >
          {n}
        </a>
      ))}
    </div>
  )
}
