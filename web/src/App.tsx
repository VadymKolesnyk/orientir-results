import { useCallback, useMemo, useRef, useState } from 'react'
import { useLiveResults } from './hooks/useLiveResults'
import { groupNames } from './lib/results'
import { parseRoute, replaceHash } from './lib/route'
import { Header } from './components/Header'
import { DayBar } from './components/DayBar'
import { SearchBox } from './components/SearchBox'
import { GroupTabs } from './components/GroupTabs'
import { ResultsTable } from './components/ResultsTable'
import { SummaryTable } from './components/SummaryTable'
import { UpdatedFooter } from './components/UpdatedFooter'

// Маршрут читаємо з hash: …/#/{event}/{d1|d2|sum}. (Із запасним розбором
// старого ?event=&day= — див. parseRoute.)
const route = parseRoute()
const eventId = route.event

export function App() {
  // «Сума» (залік за всі дні) — сегмент sum у hash. Інакше day — номер дня.
  const [sumMode, setSumMode] = useState(route.sumMode)
  const [day, setDay] = useState(route.day)
  const [activeGrp, setActiveGrp] = useState('')
  const [query, setQuery] = useState('')

  // activeGrp потрібен синхронно всередині load() (через callback), тож тримаємо ref.
  const activeGrpRef = useRef(activeGrp)
  activeGrpRef.current = activeGrp

  // Викликається з load() ПІСЛЯ отримання груп: повертає активну групу для
  // запиту результатів і за потреби узгоджує state (як renderTabs у оригіналі).
  const onGroupsResolved = useCallback((names: string[]) => {
    let grp = activeGrpRef.current
    if (!names.includes(grp)) {
      grp = names[0] || ''
      activeGrpRef.current = grp
      setActiveGrp(grp)
    }
    return grp
  }, [])

  // Залік доступний лише коли увімкнено standings — інакше скидаємо режим.
  const onStandingsOff = useCallback(() => setSumMode(false), [])

  const { state, switchGroup } = useLiveResults({
    eventId,
    day,
    sumMode,
    activeGrp,
    onGroupsResolved,
    onStandingsOff,
  })

  const names = useMemo(() => groupNames(state.groups), [state.groups])

  // --- Дії перемикача днів (оновлюють URL для копіювання посилань) ---
  const selectDay = useCallback(
    (d: number) => {
      if (!sumMode && d === day) return
      setSumMode(false)
      setDay(d)
      replaceHash({ event: eventId, day: d, sumMode: false })
    },
    [sumMode, day],
  )

  const selectSum = useCallback(() => {
    if (sumMode) return
    setSumMode(true)
    replaceHash({ event: eventId, day, sumMode: true })
  }, [sumMode, day])

  const selectGroup = useCallback(
    (grp: string) => {
      setActiveGrp(grp)
      activeGrpRef.current = grp
      void switchGroup(grp)
    },
    [switchGroup],
  )

  // --- Метарядок під заголовком ---
  const meta = useMemo(() => {
    const sub = state.event?.subtitle ? state.event.subtitle + ' · ' : ''
    if (!eventId) return ''
    return sumMode
      ? `${sub}Залік за всі дні`
      : `${sub}День ${day} · учасників: ${state.results.length}`
  }, [state.event, state.results.length, sumMode, day])

  const showPts = !!state.event?.standings

  // --- Контент ---
  let content: React.ReactNode
  if (!eventId) {
    content = (
      <div className="empty">
        Не вказано змагання. Додай у посилання{' '}
        <b>#/&lt;id&gt;/d1</b> (або <b>/sum</b> для заліку).
      </div>
    )
  } else if (state.error) {
    content = (
      <div className="empty">
        <div className="empty-title">Не вдалося завантажити результати</div>
        <div className="empty-sub">{state.error}</div>
      </div>
    )
  } else if (state.loading && !state.results.length && !state.allResults.length) {
    content = (
      <div className="empty">
        <span className="spin" aria-hidden="true" />
        <div className="empty-title">Завантажуємо результати…</div>
        <div className="empty-sub">Це займе кілька секунд</div>
      </div>
    )
  } else if (sumMode) {
    content = (
      <SummaryTable
        allResults={state.allResults}
        eventDays={state.eventDays}
        activeGrp={activeGrp}
        day={day}
        query={query}
      />
    )
  } else {
    content = (
      <ResultsTable
        results={state.results}
        groups={state.groups}
        activeGrp={activeGrp}
        query={query}
        showPts={showPts}
      />
    )
  }

  return (
    <div className="wrap">
      <div className="card">
        <Header
          event={state.event}
          meta={eventId ? meta : 'Завантаження…'}
          offline={state.offline}
        />

        <div className="bar">
          <DayBar
            eventDays={state.eventDays}
            event={state.event}
            day={day}
            sumMode={sumMode}
            onSelectDay={selectDay}
            onSelectSum={selectSum}
          />
          <SearchBox value={query} onChange={setQuery} />
        </div>

        <GroupTabs names={names} activeGrp={activeGrp} onSelect={selectGroup} />

        <div id="content">{content}</div>

        <UpdatedFooter results={state.results} offline={state.offline} />
      </div>
    </div>
  )
}
