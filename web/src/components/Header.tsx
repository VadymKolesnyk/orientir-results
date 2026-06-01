import type { EventRow } from '../types'

interface Props {
  event: EventRow | null
  meta: string
  offline: boolean
  // Клік по «офлайн» — ручне перепідключення + повне перезавантаження даних.
  onReconnect: () => void
}

// Шапка: назва змагання + підзаголовок/метадані + live-індикатор.
// В офлайні індикатор стає кнопкою «перепідключитись».
export function Header({ event, meta, offline, onReconnect }: Props) {
  return (
    <div className="top">
      <div>
        <h1 id="title">{event?.title || 'Онлайн результати'}</h1>
        <p className="sub" id="meta">
          {meta}
        </p>
      </div>
      {offline ? (
        <button
          type="button"
          className="live off"
          id="live"
          onClick={onReconnect}
          title="Перепідключитись і оновити дані"
        >
          <span className="dot" />
          офлайн · оновити
        </button>
      ) : (
        <span className="live" id="live">
          <span className="dot" />
          наживо
        </span>
      )}
    </div>
  )
}
