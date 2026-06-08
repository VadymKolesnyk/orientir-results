import { dayLabel, dayLabelShort } from '../lib/results'
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
  // За одного дня вкладки днів не показуємо (немає між чим перемикатись),
  // але кнопку «Сума» лишаємо, якщо залік увімкнено.
  const showDayTabs = eventDays.length > 1
  return (
    <div className="day" id="days">
      {showDayTabs &&
        eventDays.map((d) => (
          <button
            key={d.day}
            className={!sumMode && d.day === day ? 'on' : ''}
            onClick={() => onSelectDay(d.day)}
          >
            <span className="lbl-full">{dayLabel(d)}</span>
            <span className="lbl-short">{dayLabelShort(d)}</span>
          </button>
        ))}
      {event?.standings && (
        <button className={sumMode ? 'on' : ''} onClick={onSelectSum}>
          Сума
        </button>
      )}
    </div>
  )
}
