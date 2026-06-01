import { FAV_GRP } from '../lib/favorites'

interface Props {
  names: string[]
  activeGrp: string
  onSelect: (grp: string) => void
}

// Вкладки груп (за алфавітом). Першою — завжди жовта вкладка «★ Обрані»
// (псевдогрупа FAV_GRP), навіть коли обраних ще немає. Клік перемикає групу.
export function GroupTabs({ names, activeGrp, onSelect }: Props) {
  return (
    <div className="tabs" id="tabs">
      <a
        key={FAV_GRP}
        className={`fav${activeGrp === FAV_GRP ? ' on' : ''}`}
        onClick={() => {
          if (activeGrp !== FAV_GRP) onSelect(FAV_GRP)
        }}
      >
        ★ Обрані
      </a>
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
