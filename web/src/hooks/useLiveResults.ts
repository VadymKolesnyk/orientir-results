import { useCallback, useEffect, useRef, useState } from 'react'
import type { RealtimeChannel } from '@supabase/supabase-js'
import { sb } from '../lib/supabase'
import { groupNames } from '../lib/results'
import { RES_COLS, GRP_COLS } from '../types'
import type { EventRow, EventDay, GroupRow, ResultRow } from '../types'

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
  const { eventId, day, sumMode, activeGrp, onGroupsResolved, onStandingsOff } =
    args

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

  // Останні значення керуючих параметрів — щоб таймер/realtime читали свіже.
  const argsRef = useRef({ eventId, day, sumMode, activeGrp })
  argsRef.current = { eventId, day, sumMode, activeGrp }
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
            // Фоновий апдейт — лише results, без перезапиту метаданих.
            if (!offlineRef.current) void loadResults()
          },
        )
        .subscribe()
    }
    if (!pollTimerRef.current) {
      pollTimerRef.current = setInterval(() => {
        // Фоновий polling — лише results, без перезапиту метаданих.
        if (!offlineRef.current) void loadResults()
      }, POLL_MS)
    }
    // loadResults визначено нижче — стабільне завдяки useCallback.
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

  // Результати однієї групи поточного дня (лише потрібні колонки).
  const fetchGroup = useCallback(
    async (grp: string): Promise<ResultRow[]> => {
      const { eventId, day } = argsRef.current
      const r = await sb
        .from('results')
        .select(RES_COLS)
        .eq('event', eventId)
        .eq('day', day)
        .eq('grp', grp)
      return r.error ? [] : ((r.data as ResultRow[]) || [])
    },
    [],
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
      sb.from('events').select('id,title,subtitle,standings').eq('id', eventId).maybeSingle(),
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
    if (effSum) {
      // Режим «Сума»: тягнемо лише активну групу, але за ВСІ дні.
      const rAll = await sb
        .from('results')
        .select(RES_COLS)
        .eq('event', eventId)
        .eq('grp', grp)
      allResults = rAll.error ? [] : ((rAll.data as ResultRow[]) || [])
      results = allResults.filter((r) => r.day === day)
    } else {
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
  }, [checkStale, fetchGroup])

  // Фонове оновлення (polling/realtime): тягнемо ЛИШЕ results активної групи.
  // Метадані (event/eventDays/groups) не чіпаємо — у межах дня вони не
  // змінюються, а активну групу беремо з уже узгодженого state (через ref),
  // без повторного запиту до groups. Спінер не піднімаємо — це тихий sync.
  const loadResults = useCallback(async () => {
    const { eventId, day, sumMode, activeGrp } = argsRef.current
    if (!eventId) return
    // standings-режим уже узгоджено попереднім load(); тут просто слухаємось state.
    const effSum = sumMode
    const grp = activeGrp

    let allResults: ResultRow[] = []
    let results: ResultRow[] = []
    if (effSum) {
      const rAll = await sb
        .from('results')
        .select(RES_COLS)
        .eq('event', eventId)
        .eq('grp', grp)
      allResults = rAll.error ? [] : ((rAll.data as ResultRow[]) || [])
      results = allResults.filter((r) => r.day === day)
    } else {
      results = grp ? await fetchGroup(grp) : []
    }

    checkStale(allResults, results)
    setState((s) => ({
      ...s,
      results,
      allResults,
      offline: offlineRef.current,
    }))
  }, [checkStale, fetchGroup])

  // Перемикання групи: дотягуємо результати лише цієї групи, потім рендеримо.
  const switchGroup = useCallback(
    async (grp: string) => {
      const { eventId, sumMode, day } = argsRef.current
      // Спінер на час дотягування нової групи — інакше між кліком і відповіддю
      // показувалися б старі/порожні дані (й помилковий empty-стан).
      setState((s) => (s.loading ? s : { ...s, loading: true }))
      if (sumMode) {
        const r = await sb
          .from('results')
          .select(RES_COLS)
          .eq('event', eventId)
          .eq('grp', grp)
        const allResults = r.error ? [] : ((r.data as ResultRow[]) || [])
        setState((s) => ({
          ...s,
          allResults,
          results: allResults.filter((x) => x.day === day),
          loading: false,
        }))
      } else {
        const results = await fetchGroup(grp)
        setState((s) => ({ ...s, results, loading: false }))
      }
    },
    [fetchGroup],
  )

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

  return { state, reload: load, switchGroup }
}
