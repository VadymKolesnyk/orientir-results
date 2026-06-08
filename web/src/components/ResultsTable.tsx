import { cmp, fmtGap, fmtPts, matchesQuery } from '../lib/results'
import { StarButton } from './StarButton'
import { DEFAULT_DISPLAY_CONFIG } from '../types'
import type { DisplayConfig, GroupRow, ResultRow } from '../types'

const MEDAL = ['', 'gold', 'silver', 'bronze']

interface Props {
  results: ResultRow[]
  groups: GroupRow[]
  activeGrp: string
  query: string
  showPts: boolean
  cfg: DisplayConfig | null
  showAll: boolean // «Всі колонки» (малий екран): показати й приховані
  isFav: (bib: number) => boolean
  onToggleFav: (bib: number) => void
}

// Опис колонки: заголовок (повний/короткий) + рендер клітинки. align — клас td.
interface ColMeta {
  header: { full: string; short?: string }
  cls?: string // додатковий клас (напр. 'rk', 'pts')
  cell: (ctx: CellCtx) => React.ReactNode
}

interface CellCtx {
  r: ResultRow
  separateLg: boolean // розділяти час/DSQ на великому екрані
  separateSm: boolean // те саме на малому екрані
  winnerSec: number | null // найкращий час у групі (для відставання)
}

// Текст статусу нефінішера (running/dsq/dns) — спільний для обох колонок.
function statusText(r: ResultRow): React.ReactNode {
  if (r.status === 'running') return <span className="b-running">на дистанції</span>
  if (r.status === 'dsq') return <span className="flag">{r.reason || 'зн.'}</span>
  if (r.status === 'dns')
    return (
      <span className="b-running" style={{ color: '#9aa2b8' }}>
        не старт.
      </span>
    )
  return ''
}

// Колонка «Результат».
//  • finished — час жирним (.time);
//  • finished_pending — час жирним + «(обробка)»;
//  • нефінішер (running/dsq/dns) — якщо в нього вже є час, показуємо його
//    ЗВИЧАЙНИМ шрифтом (.time-plain), бо це не залік; плюс текст статусу
//    (причина/«на дистанції»/«не старт.»), який ховаємо на екранах із окремою
//    колонкою DSQ (там його показує statusCell). Час лишається тут завжди.
function timeCell({ r, separateLg, separateSm }: CellCtx): React.ReactNode {
  if (r.status === 'finished') return <span className="time">{r.result_time || ''}</span>
  if (r.status === 'finished_pending')
    return (
      <>
        <span className="time">{r.result_time || ''}</span>{' '}
        <span className="b-running" style={{ fontSize: 11 }}>
          (обробка)
        </span>
      </>
    )
  // Статус ховається там, де розділено (його покаже окрема колонка).
  const statusCls = `${separateLg ? 'hide-lg' : ''} ${separateSm ? 'hide-sm' : ''}`.trim()
  return (
    <>
      {r.result_time && <span className="time-plain">{r.result_time}</span>}
      {r.result_time && statusText(r) ? ' ' : ''}
      <span className={statusCls}>{statusText(r)}</span>
    </>
  )
}

// Окрема колонка «Статус/DSQ»: усе, що не фініш. Видимість самої колонки
// (на якому екрані) керується її lg/sm у конфігу.
function statusCell({ r }: CellCtx): React.ReactNode {
  return statusText(r)
}

// Відставання від лідера групи — для будь-кого з відомим часом (зокрема знятих).
// Нефінішерам (DSQ) показуємо звичайним шрифтом, як і їхній час.
function gapCell({ r, winnerSec }: CellCtx): React.ReactNode {
  if (r.result_seconds == null || winnerSec == null) return ''
  const gap = fmtGap(r.result_seconds - winnerSec)
  if (!gap) return ''
  const fin = r.status === 'finished' || r.status === 'finished_pending'
  return <span className={fin ? 'time' : 'time-plain'}>{gap}</span>
}

const COLS: Record<string, ColMeta> = {
  rk: {
    header: { full: 'Місце', short: 'М' },
    cls: 'rk',
    cell: ({ r }) =>
      r.status === 'finished' ? (
        <span className={MEDAL[r.rk ?? 0] || ''}>{r.rk || ''}</span>
      ) : (
        ''
      ),
  },
  full_name: { header: { full: "Прізвище, ім'я" }, cell: ({ r }) => r.full_name || '' },
  bib: { header: { full: '№' }, cell: ({ r }) => r.bib || '' },
  team: { header: { full: 'Регіон' }, cell: ({ r }) => r.team || '' },
  club: { header: { full: 'Клуб' }, cell: ({ r }) => r.club || '' },
  start_time: {
    header: { full: 'Старт' },
    cell: ({ r }) => (
      <span className="time" style={{ fontWeight: 400 }}>
        {r.start_time || ''}
      </span>
    ),
  },
  result_time: { header: { full: 'Час' }, cell: timeCell },
  status: { header: { full: 'Статус' }, cell: statusCell },
  gap: { header: { full: 'Відставання', short: 'Відст.' }, cls: 'time', cell: gapCell },
  points: {
    header: { full: 'Бали' },
    cls: 'pts',
    cell: ({ r }) => (r.points != null ? fmtPts(r.points) : ''),
  },
  birth: { header: { full: 'Рік нар.' }, cell: ({ r }) => r.birth || '' },
  qual: { header: { full: 'Розряд' }, cell: ({ r }) => r.qual || '' },
}

// Клас адаптивності: ховаємо колонку на тому екрані, де lg/sm = false.
function respClass(lg: boolean, sm: boolean): string {
  const c: string[] = []
  if (!lg) c.push('hide-lg')
  if (!sm) c.push('hide-sm')
  return c.join(' ')
}

export function ResultsTable({
  results,
  groups,
  activeGrp,
  query,
  showPts,
  cfg,
  showAll,
  isFav,
  onToggleFav,
}: Props) {
  const q = query.trim().toLowerCase()
  let list = q ? results.filter((r) => matchesQuery(r, q)) : results

  if (!list.length) {
    const msg = q
      ? {
          title: 'Нічого не знайдено',
          sub: `За запитом «${query.trim()}» немає учасників`,
        }
      : {
          title: 'У цій групі ще немає результатів',
          sub: 'Тут з’являться учасники, щойно стартує група',
        }
    return (
      <div className="empty">
        <div className="empty-title">{msg.title}</div>
        <div className="empty-sub">{msg.sub}</div>
      </div>
    )
  }

  list = [...list].sort(cmp)

  const conf = cfg ?? DEFAULT_DISPLAY_CONFIG
  const separateLg = conf.separateDsqLg
  // «Всі колонки» (малий екран) → поводимось як на великому: і набір колонок,
  // і логіка розділення DSQ беруться з великого екрана.
  const separateSm = showAll ? separateLg : conf.separateDsqSm

  // Найкращий час у групі — для колонки відставання.
  const winnerSec = list.reduce<number | null>((min, r) => {
    const fin = r.status === 'finished' || r.status === 'finished_pending'
    if (!fin || r.result_seconds == null) return min
    return min == null || r.result_seconds < min ? r.result_seconds : min
  }, null)

  // Активні колонки: за порядком, лише наявні в реєстрі. У режимі «Всі колонки»
  // малий екран дублює великий (sm := lg). Колонку «Статус/DSQ» показуємо лише
  // де ввімкнено розділення (lg→separateLg, sm→separateSm). points — лише showPts.
  const cols = [...conf.columns]
    .sort((a, b) => a.order - b.order)
    .map((c) => (showAll ? { ...c, sm: c.lg } : c))
    .map((c) =>
      c.key === 'status'
        ? { ...c, lg: c.lg && separateLg, sm: c.sm && separateSm }
        : c,
    )
    .filter((c) => COLS[c.key] && (c.lg || c.sm))
    .filter((c) => (c.key === 'points' ? showPts : true))

  const g = groups.find((x) => x.name === activeGrp)
  const fin = list.filter(
    (r) => r.status === 'finished' || r.status === 'finished_pending',
  ).length
  const run = list.filter((r) => r.status === 'running').length
  const dsq = list.filter((r) => r.status === 'dsq').length

  return (
    <>
      <div className="ghead">
        <b>{activeGrp}</b>
        {g && (
          <>
            {' '}
            &nbsp;{' '}
            <span>
              {g.controls ? `${g.controls} КП` : ''}{' '}
              {g.distance_km ? <>&nbsp; {g.distance_km} км</> : ''}
            </span>
          </>
        )}
      </div>
      <div className={`tbl-scroll cfg-table${showAll ? ' show-all' : ''}`}>
        <table>
          <thead>
            <tr>
              <th className="col-star" aria-label="Обране"></th>
              {cols.map((c) => {
                const m = COLS[c.key]
                const cls = `col-${c.key} ${m.cls ?? ''} ${respClass(c.lg, c.sm)}`.trim()
                return (
                  <th key={c.key} className={cls}>
                    {m.header.short ? (
                      <>
                        <span className="th-full">{m.header.full}</span>
                        <span className="th-short">{m.header.short}</span>
                      </>
                    ) : (
                      m.header.full
                    )}
                  </th>
                )
              })}
            </tr>
          </thead>
          <tbody>
            {list.map((r) => {
              const fav = isFav(r.bib)
              const ctx: CellCtx = { r, separateLg, separateSm, winnerSec }
              return (
                <tr key={r.bib} className={fav ? 'fav-row' : ''}>
                  <td className="col-star">
                    <StarButton active={fav} onToggle={() => onToggleFav(r.bib)} />
                  </td>
                  {cols.map((c) => {
                    const m = COLS[c.key]
                    // pts-клітинка має модифікатор win для 100 балів — як раніше.
                    const extra =
                      c.key === 'points' && r.points === 100 ? ' win' : ''
                    const cls =
                      `col-${c.key} ${m.cls ?? ''}${extra} ${respClass(c.lg, c.sm)}`.trim()
                    return (
                      <td key={c.key} className={cls}>
                        {m.cell(ctx)}
                      </td>
                    )
                  })}
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>
      <p className="sub" style={{ marginTop: 8 }}>
        Фінішували: {fin} · на дистанції: {run}
        {dsq ? ` · знято: ${dsq}` : ''} · усього: {list.length}
      </p>
    </>
  )
}
