import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useLiveResults } from './hooks/useLiveResults'
import { useFavorites } from './hooks/useFavorites'
import { groupNames } from './lib/results'
import { FAV_GRP } from './lib/favorites'
import { loadRecent, pushRecent } from './lib/recent'
import { parseRoute, replaceHash, buildHash } from './lib/route'
import { Header } from './components/Header'
import { DayBar } from './components/DayBar'
import { SearchBox } from './components/SearchBox'
import { GroupTabs } from './components/GroupTabs'
import { ResultsTable } from './components/ResultsTable'
import { SummaryTable } from './components/SummaryTable'
import { FavoritesList } from './components/FavoritesList'
import { UpdatedFooter } from './components/UpdatedFooter'

// Маршрут читаємо з hash: …/#/{event}/{d1|d2|sum}. (Із запасним розбором
// старого ?event=&day= — див. parseRoute.)
const route = parseRoute()
const eventId = route.event

export function App() {
  // «Сума» (залік за всі дні) — сегмент sum у hash. Інакше day — номер дня.
  const [sumMode, setSumMode] = useState(route.sumMode)
  const [day, setDay] = useState(route.day)
  // Початкова група — з URL (route.grp), щоб після F5 лишатись на ній.
  // Якщо такої групи не виявиться серед доступних — onGroupsResolved відкине її.
  const [activeGrp, setActiveGrp] = useState(route.grp)
  const [query, setQuery] = useState('')

  // Нещодавно відкриті змагання (для кореневої сторінки без event у URL).
  // Читаємо один раз при монтуванні — на цій сторінці список не змінюється.
  const [recent] = useState(loadRecent)

  // Обрані (зірочка) — локальний стан у localStorage для цього змагання.
  // favBibs передаємо у useLiveResults; там його читають через власний ref.
  const { bibs: favBibs, has: isFav, toggle: toggleFav } = useFavorites(eventId)

  // activeGrp потрібен синхронно всередині load() (через callback), тож тримаємо ref.
  const activeGrpRef = useRef(activeGrp)
  activeGrpRef.current = activeGrp
  // day/sumMode теж читаємо через ref у onGroupsResolved (deps=[], інакше стейл).
  const dayRef = useRef(day)
  dayRef.current = day
  const sumModeRef = useRef(sumMode)
  sumModeRef.current = sumMode

  // Викликається з load() ПІСЛЯ отримання груп: повертає активну групу для
  // запиту результатів і за потреби узгоджує state (як renderTabs у оригіналі).
  const onGroupsResolved = useCallback((names: string[]) => {
    let grp = activeGrpRef.current
    // Псевдогрупа «Обрані» завжди валідна (її немає серед names) — не відкидаємо.
    if (grp === FAV_GRP) return grp
    if (!names.includes(grp)) {
      grp = names[0] || ''
      activeGrpRef.current = grp
      setActiveGrp(grp)
      // Узгоджуємо URL: група з лінка не знайшлась (або була порожня) —
      // фіксуємо реально обрану, щоб F5 лишався на ній.
      replaceHash({
        event: eventId,
        day: dayRef.current,
        sumMode: sumModeRef.current,
        grp,
      })
    }
    return grp
  }, [])

  // Залік доступний лише коли увімкнено standings — інакше скидаємо режим.
  const onStandingsOff = useCallback(() => setSumMode(false), [])

  const { state, switchGroup, reconnect } = useLiveResults({
    eventId,
    day,
    sumMode,
    activeGrp,
    favBibs,
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
      replaceHash({ event: eventId, day: d, sumMode: false, grp: activeGrp })
    },
    [sumMode, day, activeGrp],
  )

  const selectSum = useCallback(() => {
    if (sumMode) return
    setSumMode(true)
    replaceHash({ event: eventId, day, sumMode: true, grp: activeGrp })
  }, [sumMode, day, activeGrp])

  const selectGroup = useCallback(
    (grp: string) => {
      setActiveGrp(grp)
      activeGrpRef.current = grp
      replaceHash({ event: eventId, day, sumMode, grp })
      void switchGroup(grp)
    },
    [switchGroup, day, sumMode],
  )

  // Реакція на зовнішню зміну URL: ручне редагування hash, кнопки «назад/вперед»
  // браузера. Перечитуємо маршрут і застосовуємо лише те, що реально змінилось
  // (інакше зайві перезавантаження). Наш власний replaceHash теж шле hashchange —
  // але до цього моменту state вже збігається з URL, тож нічого не робимо.
  useEffect(() => {
    const onHashChange = () => {
      const r = parseRoute()
      // Зміна змагання — найважчий випадок (інший набір даних, обрані тощо).
      // Найнадійніше — повне перезавантаження сторінки під новий eventId.
      if (r.event !== eventId) {
        location.reload()
        return
      }
      if (r.sumMode !== sumModeRef.current) {
        setSumMode(r.sumMode)
        sumModeRef.current = r.sumMode
      }
      if (r.day !== dayRef.current) {
        setDay(r.day)
        dayRef.current = r.day
      }
      if (r.grp && r.grp !== activeGrpRef.current) {
        setActiveGrp(r.grp)
        activeGrpRef.current = r.grp
        void switchGroup(r.grp)
      }
    }
    window.addEventListener('hashchange', onHashChange)
    return () => window.removeEventListener('hashchange', onHashChange)
  }, [switchGroup])

  // Запам'ятовуємо відкрите змагання у списку нещодавніх (до 5), щойно
  // підтягнулись його метадані. title беремо з даних, інакше — сам id.
  useEffect(() => {
    // eventId — стала на рівні модуля (не залежність); стежимо за state.event.
    if (eventId && state.event)
      pushRecent(eventId, state.event.title || eventId)
  }, [state.event])

  const favView = activeGrp === FAV_GRP

  // --- Метарядок під заголовком ---
  const meta = useMemo(() => {
    const sub = state.event?.subtitle ? state.event.subtitle + ' · ' : ''
    if (!eventId) return ''
    if (favView)
      return sumMode
        ? `${sub}Обрані · Залік за всі дні`
        : `${sub}Обрані · День ${day} · учасників: ${state.results.length}`
    return sumMode
      ? `${sub}Залік за всі дні`
      : `${sub}День ${day} · учасників: ${state.results.length}`
  }, [state.event, state.results.length, sumMode, day, favView])

  const showPts = !!state.event?.standings

  // --- Контент ---
  let content: React.ReactNode
  if (!eventId) {
    content = (
      <div className="empty">
        <div className="empty-title">Не вказано змагання</div>
        <div className="empty-sub">
          Додай у посилання <b>#/&lt;id&gt;/d1</b> (або <b>/sum</b> для заліку).
        </div>
        {recent.length > 0 && (
          <div className="recent">
            <div className="recent-head">Нещодавно відкриті:</div>
            <ul className="recent-list">
              {recent.map((e) => (
                <li key={e.id}>
                  <a
                    className="recent-link"
                    href={buildHash({
                      event: e.id,
                      day: 1,
                      sumMode: false,
                      grp: '',
                    })}
                  >
                    {e.title}
                  </a>
                </li>
              ))}
            </ul>
          </div>
        )}
      </div>
    )
  } else if (state.error) {
    content = (
      <div className="empty">
        <div className="empty-title">Не вдалося завантажити результати</div>
        <div className="empty-sub">{state.error}</div>
      </div>
    )
  } else if (state.loading) {
    // state.loading вмикається ЛИШЕ для «навігаційних» завантажень
    // (перше відкриття, зміна дня/режиму/групи). Фоновий polling/realtime
    // оновлює дані тихо, без спінера. Тож показуємо спінер на будь-яке
    // навігаційне завантаження — інакше встигають блимнути старі дані
    // попереднього дня/групи або помилковий «немає результатів».
    content = (
      <div className="empty">
        <span className="spin" aria-hidden="true" />
        <div className="empty-title">Завантаження…</div>
        <div className="empty-sub">Це займе кілька секунд</div>
      </div>
    )
  } else if (favView && sumMode) {
    // Обрані + «Сума»: залік за всі дні для обраних (agg по bib, з усіх груп).
    content = (
      <SummaryTable
        allResults={state.allResults}
        eventDays={state.eventDays}
        activeGrp={activeGrp}
        day={day}
        query={query}
        isFav={isFav}
        onToggleFav={toggleFav}
      />
    )
  } else if (favView) {
    // Обрані — окремий вид: алфавітний список усіх позначених учасників
    // (з усіх груп) за поточний день.
    content = (
      <FavoritesList
        results={state.results}
        query={query}
        showPts={showPts}
        isFav={isFav}
        onToggleFav={toggleFav}
        onSelectGroup={selectGroup}
      />
    )
  } else if (sumMode) {
    content = (
      <SummaryTable
        allResults={state.allResults}
        eventDays={state.eventDays}
        activeGrp={activeGrp}
        day={day}
        query={query}
        isFav={isFav}
        onToggleFav={toggleFav}
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
        isFav={isFav}
        onToggleFav={toggleFav}
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
          onReconnect={reconnect}
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
