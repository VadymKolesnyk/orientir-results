import { useCallback, useEffect, useRef, useState } from 'react'
import type { RealtimeChannel } from '@supabase/supabase-js'
import { sb } from '../lib/supabase'
import { groupNames } from '../lib/results'
import { FAV_GRP } from '../lib/favorites'
import { RES_COLS, RES_COLS_FAV, SYNC_COLS, GRP_COLS } from '../types'
import type {
  EventRow,
  EventDay,
  GroupRow,
  ResultRow,
  SyncRow,
} from '../types'

// Авто-офлайн: якщо час останньої синхронізації не змінюється N опитувань
// поспіль — вважаємо змагання завершеним, спиняємо polling+realtime.
// Поновлення — лише перезавантаженням сторінки (F5).
const STALE_LIMIT = 4 // скільки опитувань поспіль без змін → офлайн
const STALE_MS = 5 * 60 * 1000 // остання зміна давніша за 5 хв → офлайн одразу
const POLL_MS = 10000

interface Args {
  eventId: string
  day: number
  sumMode: boolean
  activeGrp: string
  // Номери обраних учасників (bib). Потрібні, коли activeGrp === FAV_GRP:
  // тягнемо рядки всіх груп, відфільтровані за цими bib.
  favBibs: number[]
  // викликається після кожного load, щоб App міг узгодити активну групу
  onGroupsResolved: (names: string[]) => string
  // вмикається, коли event.standings === false (App має скинути sumMode)
  onStandingsOff: () => void
}

export interface LiveState {
  event: EventRow | null
  eventDays: EventDay[]
  groups: GroupRow[]
  results: ResultRow[]
  allResults: ResultRow[]
  offline: boolean
  loading: boolean
  error: string | null
}

export function useLiveResults(args: Args) {
  const {
    eventId,
    day,
    sumMode,
    activeGrp,
    favBibs,
    onGroupsResolved,
    onStandingsOff,
  } = args

  const [state, setState] = useState<LiveState>({
    event: null,
    eventDays: [],
    groups: [],
    results: [],
    allResults: [],
    offline: false,
    loading: true,
    error: null,
  })

  // Мутабельний стан, потрібний замиканням polling/realtime (без ререндерів).
  const offlineRef = useRef(false)
  const lastSyncRef = useRef<string | null>(null)
  const staleCountRef = useRef(0)
  const pollTimerRef = useRef<ReturnType<typeof setInterval> | null>(null)
  const channelRef = useRef<RealtimeChannel | null>(null)

  // Дзеркало вже завантажених рядків — щоб фоновий syncResults міг накласти
  // лише змінні поля на наявні (full_name/team/club/start_time лишаються).
  // У sum-режимі повний набір — allResults (усі дні), інакше — results.
  const rowsRef = useRef<{ results: ResultRow[]; allResults: ResultRow[] }>({
    results: [],
    allResults: [],
  })
  rowsRef.current = { results: state.results, allResults: state.allResults }

  // Останні значення керуючих параметрів — щоб таймер/realtime читали свіже.
  const argsRef = useRef({ eventId, day, sumMode, activeGrp, favBibs })
  argsRef.current = { eventId, day, sumMode, activeGrp, favBibs }
  const cbRef = useRef({ onGroupsResolved, onStandingsOff })
  cbRef.current = { onGroupsResolved, onStandingsOff }

  const setOffline = useCallback((v: boolean) => {
    offlineRef.current = v
    setState((s) => (s.offline === v ? s : { ...s, offline: v }))
  }, [])

  const goOffline = useCallback(() => {
    if (offlineRef.current) return
    setOffline(true)
    if (pollTimerRef.current) {
      clearInterval(pollTimerRef.current)
      pollTimerRef.current = null
    }
    if (channelRef.current) {
      sb.removeChannel(channelRef.current)
      channelRef.current = null
    }
  }, [setOffline])

  // (Пере)підписка на realtime + запасне опитування раз на 10 с.
  const startLive = useCallback(() => {
    if (!channelRef.current) {
      channelRef.current = sb
        .channel('live')
        .on(
          'postgres_changes',
          { event: '*', schema: 'public', table: 'results' },
          () => {
            // Фоновий апдейт — лише змінні поля (merge), без метаданих.
            if (!offlineRef.current) void syncResults()
          },
        )
        .subscribe()
    }
    if (!pollTimerRef.current) {
      pollTimerRef.current = setInterval(() => {
        // Фоновий polling — лише змінні поля (merge), без метаданих.
        if (!offlineRef.current) void syncResults()
      }, POLL_MS)
    }
    // syncResults визначено нижче — стабільне завдяки useCallback.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  // Повернення в онлайн: знову вмикаємо realtime + опитування й скидаємо «застій».
  const goBackOnline = useCallback(() => {
    setOffline(false)
    staleCountRef.current = 0
    lastSyncRef.current = null
    startLive()
  }, [setOffline, startLive])

  // Стежимо за «свіжістю»: остання зміна давніша за пів години → офлайн.
  // Інакше — лічильник застою (updated_at не рухається N опитувань поспіль).
  const checkStale = useCallback(
    (allResults: ResultRow[], results: ResultRow[]) => {
      const src = allResults.length ? allResults : results
      const cur = src.reduce((m, r) => (r.updated_at > m ? r.updated_at : m), '')
      if (!cur) return // ще немає даних — стан не чіпаємо

      const fresh = Date.now() - new Date(cur).getTime() <= STALE_MS
      if (!fresh) {
        goOffline()
        return
      }
      // Дані свіжі. Якщо були офлайн — пробуємо повернутись в онлайн.
      if (offlineRef.current) goBackOnline()

      if (lastSyncRef.current === null) {
        lastSyncRef.current = cur
        staleCountRef.current = 0
        return
      }
      if (cur === lastSyncRef.current) {
        if (++staleCountRef.current >= STALE_LIMIT) goOffline()
      } else {
        lastSyncRef.current = cur
        staleCountRef.current = 0
      }
    },
    [goOffline, goBackOnline],
  )

  // Рядки обраних учасників (FAV_GRP) із усіх груп. sum=false → лише поточний
  // день; sum=true → усі дні (для заліку «Сума»). Тягнемо grp, щоб знати групу
  // кожного. Порожній список обраних → без запиту.
  const fetchFavorites = useCallback(
    async (sum: boolean): Promise<ResultRow[]> => {
      const { eventId, day, favBibs } = argsRef.current
      if (!favBibs.length) return []
      let q = sb
        .from('results')
        .select(RES_COLS_FAV)
        .eq('event', eventId)
        .in('bib', favBibs)
      if (!sum) q = q.eq('day', day)
      const r = await q
      // RES_COLS_FAV — динамічний рядок, тож supabase не виводить тип рядка;
      // приводимо через unknown (дані відповідають ResultRow + grp).
      return r.error ? [] : ((r.data as unknown as ResultRow[]) || [])
    },
    [],
  )

  // Результати однієї групи поточного дня (лише потрібні колонки).
  // Псевдогрупа «Обрані» (FAV_GRP) делегується у fetchFavorites (поточний день).
  const fetchGroup = useCallback(
    async (grp: string): Promise<ResultRow[]> => {
      if (grp === FAV_GRP) return fetchFavorites(false)
      const { eventId, day } = argsRef.current
      const r = await sb
        .from('results')
        .select(RES_COLS)
        .eq('event', eventId)
        .eq('day', day)
        .eq('grp', grp)
      return r.error ? [] : ((r.data as ResultRow[]) || [])
    },
    [fetchFavorites],
  )

  // showLoading=true піднімає спінер на час запиту — для «навігаційних»
  // перезапитів (перше завантаження, зміна дня/режиму). Фоновий polling/realtime
  // викликає load() без прапорця, щоб не блимати поверх уже показаних даних.
  const load = useCallback(async (showLoading = false) => {
    const { eventId, day, sumMode } = argsRef.current
    if (!eventId) {
      setState((s) => ({ ...s, loading: false, error: 'no-event' }))
      return
    }
    if (showLoading) setState((s) => (s.loading ? s : { ...s, loading: true }))

    // Легкі метадані: групи дня, саме змагання, дні.
    const [rGrp, rEv, rDays] = await Promise.all([
      sb.from('groups').select(GRP_COLS).eq('event', eventId).eq('day', day),
      sb.from('events').select('id,title,subtitle,standings,points,display_config').eq('id', eventId).maybeSingle(),
      sb.from('event_days').select('day,label,ord').eq('event', eventId).order('ord'),
    ])
    const err = rGrp.error || rEv.error || rDays.error
    if (err) {
      setState((s) => ({ ...s, loading: false, error: err.message }))
      return
    }

    const groups = (rGrp.data as GroupRow[]) || []
    const event = (rEv.data as EventRow) || null
    const eventDays = (rDays.data as EventDay[]) || []

    // Залік доступний лише коли увімкнено standings — інакше скидаємо режим.
    let effSum = sumMode
    if (!event?.standings) {
      effSum = false
      cbRef.current.onStandingsOff()
    }

    // Визначаємо активну групу ДО завантаження її результатів.
    const names = groupNames(groups)
    const grp = cbRef.current.onGroupsResolved(names)

    let allResults: ResultRow[] = []
    let results: ResultRow[] = []
    if (effSum && grp === FAV_GRP) {
      // «Обрані» + «Сума»: залік за всі дні для обраних bib (з усіх груп).
      allResults = await fetchFavorites(true)
      results = allResults.filter((r) => r.day === day)
    } else if (effSum) {
      // Режим «Сума»: тягнемо лише активну групу, але за ВСІ дні.
      const rAll = await sb
        .from('results')
        .select(RES_COLS)
        .eq('event', eventId)
        .eq('grp', grp)
      allResults = rAll.error ? [] : ((rAll.data as ResultRow[]) || [])
      results = allResults.filter((r) => r.day === day)
    } else {
      // Звичайна група або «Обрані» за день (fetchGroup розрізняє FAV_GRP).
      results = grp ? await fetchGroup(grp) : []
    }

    checkStale(allResults, results)
    setState({
      event,
      eventDays,
      groups,
      results,
      allResults,
      offline: offlineRef.current,
      loading: false,
      error: null,
    })
  }, [checkStale, fetchGroup, fetchFavorites])

  // Фонове оновлення (polling/realtime): тягнемо ЛИШЕ змінні поля (SYNC_COLS)
  // активної групи й накладаємо їх на вже завантажені рядки — сталі поля
  // (ПІБ/команда/клуб/час старту) не перезапитуємо. Метадані теж не чіпаємо.
  // Нові учасники (bib, якого ще нема локально) приходять без сталих полів —
  // для них робимо разовий повний дозапит (RES_COLS) лише по їхніх bib.
  const syncResults = useCallback(async () => {
    const { eventId, day, sumMode, activeGrp: grp, favBibs } = argsRef.current
    if (!eventId || !grp) return

    const fav = grp === FAV_GRP
    if (fav && !favBibs.length) {
      // Обраних немає — нема чого синхронізувати; тримаємо порожній список.
      setState((s) =>
        s.results.length ? { ...s, results: [], allResults: [] } : s,
      )
      return
    }
    // Залік (за всі дні) — застосовуємо й до «Обраних» (тоді agg по bib),
    // і до звичайної групи. effSum керує наявністю фільтра за днем.
    const effSum = sumMode
    // Колонки повного дозапиту: для «Обраних» тягнемо ще й grp.
    const fullCols = fav ? RES_COLS_FAV : RES_COLS

    // 1) Тягнемо лише змінні поля. У sum-режимі — за всі дні, інакше — поточний.
    // Для «Обраних» — без grp-фільтра, за списком bib; решта — за grp.
    let q = sb.from('results').select(SYNC_COLS).eq('event', eventId)
    q = fav ? q.in('bib', favBibs) : q.eq('grp', grp)
    if (!effSum) q = q.eq('day', day)
    const rSync = await q
    if (rSync.error) return // тихо: фоновий sync не показує помилок
    const syncRows = (rSync.data as SyncRow[]) || []

    // 2) Індекс наявних повних рядків за ключем (bib,day).
    const prevFull = effSum
      ? rowsRef.current.allResults
      : rowsRef.current.results
    const key = (bib: number, d: number) => `${bib}|${d}`
    const prevByKey = new Map(prevFull.map((r) => [key(r.bib, r.day), r]))

    // 3) Накладаємо змінні поля на наявні; невідомі bib (нові) — у fallback.
    const merged: ResultRow[] = []
    const missing: SyncRow[] = []
    for (const s of syncRows) {
      const prev = prevByKey.get(key(s.bib, s.day))
      if (prev) merged.push({ ...prev, ...s })
      else missing.push(s)
    }

    // 4) Fallback: для нових учасників один повний запит саме по їхніх bib.
    if (missing.length) {
      const bibs = [...new Set(missing.map((m) => m.bib))]
      let fq = sb
        .from('results')
        .select(fullCols)
        .eq('event', eventId)
        .in('bib', bibs)
      if (!fav) fq = fq.eq('grp', grp)
      if (!effSum) fq = fq.eq('day', day)
      const rFull = await fq
      // fullCols — динамічний рядок (RES_COLS/RES_COLS_FAV), тип рядка не
      // виводиться; приводимо через unknown.
      const fullRows = rFull.error
        ? []
        : ((rFull.data as unknown as ResultRow[]) || [])
      merged.push(...fullRows)
    }

    // 5) Розкладаємо на results/allResults так само, як робив повний fetch.
    const allResults = effSum ? merged : []
    const results = effSum ? merged.filter((r) => r.day === day) : merged

    checkStale(allResults, results)
    setState((s) => ({
      ...s,
      results,
      allResults,
      offline: offlineRef.current,
    }))
  }, [checkStale])

  // Перемикання групи: дотягуємо результати лише цієї групи, потім рендеримо.
  const switchGroup = useCallback(
    async (grp: string) => {
      const { eventId, sumMode, day } = argsRef.current
      // Спінер на час дотягування нової групи — інакше між кліком і відповіддю
      // показувалися б старі/порожні дані (й помилковий empty-стан).
      setState((s) => (s.loading ? s : { ...s, loading: true }))
      if (sumMode) {
        // Залік за всі дні: «Обрані» — за обраними bib (усі групи), звичайна
        // група — за grp. Поточний день виокремлюємо для тих видів, що його ще
        // використовують.
        const allResults =
          grp === FAV_GRP
            ? await fetchFavorites(true)
            : await (async () => {
                const r = await sb
                  .from('results')
                  .select(RES_COLS)
                  .eq('event', eventId)
                  .eq('grp', grp)
                return r.error ? [] : ((r.data as ResultRow[]) || [])
              })()
        setState((s) => ({
          ...s,
          allResults,
          results: allResults.filter((x) => x.day === day),
          loading: false,
        }))
      } else {
        // Звичайна група або «Обрані» за день (fetchGroup розрізняє FAV_GRP).
        const results = await fetchGroup(grp)
        setState((s) => ({ ...s, results, allResults: [], loading: false }))
      }
    },
    [fetchGroup, fetchFavorites],
  )

  // Ручне перепідключення (клік по «офлайн»): вмикаємо live назад, скидаємо
  // лічильники застою й ПОВНІСТЮ перезавантажуємо все (метадані + результати)
  // зі спінером — як F5, але без перезавантаження сторінки.
  const reconnect = useCallback(() => {
    goBackOnline() // setOffline(false) + reset stale + startLive()
    void load(true)
  }, [goBackOnline, load])

  // Перше завантаження + старт live. Перезавантаження при зміні дня/режиму.
  useEffect(() => {
    void load(true)
    startLive()
    return () => {
      if (pollTimerRef.current) {
        clearInterval(pollTimerRef.current)
        pollTimerRef.current = null
      }
      if (channelRef.current) {
        sb.removeChannel(channelRef.current)
        channelRef.current = null
      }
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  // Зміна дня або режиму «Сума» → дотягуємо відповідні дані (зі спінером).
  useEffect(() => {
    void load(true)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [day, sumMode])

  // На вкладці «Обрані»: зміна складу обраних (додали/зняли зірочку) → тихо
  // переотримуємо список (без спінера — швидке точкове оновлення). Решти вкладок
  // це не стосується. favBibs.join — стабільний ключ за вмістом, не референцією.
  const favKey = favBibs.join(',')
  useEffect(() => {
    if (activeGrp !== FAV_GRP) return
    void (async () => {
      if (sumMode) {
        // «Сума»: тягнемо всі дні обраних для перерахунку заліку.
        const allResults = await fetchFavorites(true)
        setState((s) => ({
          ...s,
          allResults,
          results: allResults.filter((r) => r.day === day),
        }))
      } else {
        const results = await fetchFavorites(false)
        setState((s) => ({ ...s, results, allResults: [] }))
      }
    })()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [favKey, activeGrp, sumMode, day])

  return { state, reload: load, switchGroup, reconnect }
}
