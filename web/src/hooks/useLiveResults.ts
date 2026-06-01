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
const STALE_MS = 30 * 60 * 1000 // остання зміна давніша за пів години → офлайн одразу
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
            if (!offlineRef.current) void load()
          },
        )
        .subscribe()
    }
    if (!pollTimerRef.current) {
      pollTimerRef.current = setInterval(() => {
        if (!offlineRef.current) void load()
      }, POLL_MS)
    }
    // load визначено нижче — стабільне завдяки useCallback із порожніми deps.
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

  const load = useCallback(async () => {
    const { eventId, day, sumMode } = argsRef.current
    if (!eventId) {
      setState((s) => ({ ...s, loading: false, error: 'no-event' }))
      return
    }

    // Легкі метадані: групи дня, саме змагання, дні.
    const [rGrp, rEv, rDays] = await Promise.all([
      sb.from('groups').select(GRP_COLS).eq('event', eventId).eq('day', day).order('ord'),
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

  // Перемикання групи: дотягуємо результати лише цієї групи, потім рендеримо.
  const switchGroup = useCallback(
    async (grp: string) => {
      const { eventId, sumMode, day } = argsRef.current
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
        }))
      } else {
        const results = await fetchGroup(grp)
        setState((s) => ({ ...s, results }))
      }
    },
    [fetchGroup],
  )

  // Перше завантаження + старт live. Перезавантаження при зміні дня/режиму.
  useEffect(() => {
    void load()
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

  // Зміна дня або режиму «Сума» → дотягуємо відповідні дані.
  useEffect(() => {
    void load()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [day, sumMode])

  return { state, reload: load, switchGroup }
}
