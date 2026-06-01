import type { EventRow } from '../types'

interface Props {
  event: EventRow | null
  meta: string
  offline: boolean
}

// Шапка: назва змагання + підзаголовок/метадані + live-індикатор.
export function Header({ event, meta, offline }: Props) {
  return (
    <div className="top">
      <div>
        <h1 id="title">{event?.title || 'Онлайн результати'}</h1>
        <p className="sub" id="meta">
          {meta}
        </p>
      </div>
      <span className={`live${offline ? ' off' : ''}`} id="live">
        <span className="dot" />
        {offline ? 'офлайн' : 'наживо'}
      </span>
    </div>
  )
}
