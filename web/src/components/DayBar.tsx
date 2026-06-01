import { dayLabel } from '../lib/results'
import type { EventDay, EventRow } from '../types'

interface Props {
  eventDays: EventDay[]
  event: EventRow | null
  day: number
  sumMode: boolean
  onSelectDay: (day: number) => void
  onSelectSum: () => void
}

// Динамічний перемикач днів — кнопки з event_days + (опційно) «Σ Сума».
export function DayBar({
  eventDays,
  event,
  day,
  sumMode,
  onSelectDay,
  onSelectSum,
}: Props) {
  if (!eventDays.length) return <div className="day" id="days" />
  return (
    <div className="day" id="days">
      {eventDays.map((d) => (
        <button
          key={d.day}
          className={!sumMode && d.day === day ? 'on' : ''}
          onClick={() => onSelectDay(d.day)}
        >
          {dayLabel(d)}
        </button>
      ))}
      {event?.standings && (
        <button className={sumMode ? 'on' : ''} onClick={onSelectSum}>
          Σ Сума
        </button>
      )}
    </div>
  )
}
