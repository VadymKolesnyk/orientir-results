import { fmtGap, fmtPts } from '../lib/results'
import { STATUS_ORDER } from '../types'
import type { GroupRow, RelayLeg, RelayTeamRow } from '../types'

const MEDAL = ['', 'gold', 'silver', 'bronze']

interface Props {
  teams: RelayTeamRow[]
  groups: GroupRow[]
  activeGrp: string
  query: string
  showPts: boolean
}

// Порядок команд: як в особистій таблиці — фініш (за місцем) → без місця (за
// часом) → на дистанції → зняті → не старт; за рівних — за номером команди.
function cmpTeams(a: RelayTeamRow, b: RelayTeamRow): number {
  const sa = STATUS_ORDER[a.status] ?? 9
  const sb = STATUS_ORDER[b.status] ?? 9
  if (sa !== sb) return sa - sb
  if (a.status === 'finished' && a.rk && b.rk && a.rk !== b.rk) return a.rk - b.rk
  if (a.status === 'finished' || a.status === 'finished_pending' || a.status === 'dsq') {
    const ta = a.result_seconds ?? 1e9
    const tb = b.result_seconds ?? 1e9
    if (ta !== tb) return ta - tb
  }
  return a.num - b.num
}

// Пошук: номер / назва / клуб / регіон команди, або ПІБ / номер будь-якого етапу.
function matchesTeam(t: RelayTeamRow, q: string): boolean {
  if (!q) return true
  const hit = (s: string | null | undefined) => (s || '').toLowerCase().includes(q)
  return (
    String(t.num).includes(q) ||
    hit(t.name) ||
    hit(t.club) ||
    hit(t.region) ||
    (t.legs || []).some((l) => hit(l.full_name) || l.bib.includes(q))
  )
}

// Текст статусу нефінішера — спільний для команди й етапу.
function statusText(status: string, reason: string | null): React.ReactNode {
  if (status === 'pending') return <span className="b-running">очікує</span>
  if (status === 'running') return <span className="b-running">на дистанції</span>
  if (status === 'dsq') return <span className="flag">{reason || 'зн.'}</span>
  if (status === 'dns')
    return (
      <span className="b-running" style={{ color: '#9aa2b8' }}>
        не старт.
      </span>
    )
  return ''
}

function teamTime(t: RelayTeamRow): React.ReactNode {
  if (t.status === 'finished') return <span className="time">{t.result_time || ''}</span>
  if (t.status === 'finished_pending')
    return (
      <>
        <span className="time">{t.result_time || ''}</span>{' '}
        {!t.out_of_competition && (
          <span className="b-running" style={{ fontSize: 11 }}>
            (обробка)
          </span>
        )}
      </>
    )
  return (
    <>
      {t.result_time && <span className="time-plain">{t.result_time} </span>}
      {statusText(t.status, t.reason)}
    </>
  )
}

function legTime(l: RelayLeg): React.ReactNode {
  if (l.status === 'finished' || l.status === 'finished_pending')
    return <span className="time-plain">{l.leg_time || ''}</span>
  return (
    <>
      {l.leg_time && <span className="time-plain">{l.leg_time} </span>}
      {statusText(l.status, l.reason)}
    </>
  )
}

// Підписи колонок плагіна поряд зі значеннями етапу: «K 0.95 · T·K 23:45».
function legExtras(l: RelayLeg, columns: RelayTeamRow['columns']): string {
  if (!columns?.length || !l.extra) return ''
  return columns
    .map((c) => (l.extra?.[c.key] ? `${c.label} ${l.extra[c.key]}` : ''))
    .filter(Boolean)
    .join(' · ')
}

export function RelayTable({ teams, groups, activeGrp, query, showPts }: Props) {
  const q = query.trim().toLowerCase()
  const list = (q ? teams.filter((t) => matchesTeam(t, q)) : [...teams]).sort(cmpTeams)

  if (!list.length)
    return (
      <div className="empty">
        <div className="empty-title">Нічого не знайдено</div>
        <div className="empty-sub">За запитом «{query.trim()}» немає команд</div>
      </div>
    )

  // Найкращий час команди з місцем — для відставання.
  const winnerSec = list.reduce<number | null>((min, t) => {
    if (t.status !== 'finished' || t.rk == null || t.result_seconds == null) return min
    return min == null || t.result_seconds < min ? t.result_seconds : min
  }, null)
  const withPts = showPts && list.some((t) => t.points != null)

  const g = groups.find((x) => x.name === activeGrp)
  const legCount = Math.max(0, ...list.map((t) => t.legs?.length ?? 0))
  const fin = list.filter((t) => t.status === 'finished' || t.status === 'finished_pending').length
  const run = list.filter((t) => t.status === 'running').length
  const dsq = list.filter((t) => t.status === 'dsq').length

  return (
    <>
      <div className="ghead">
        <b>{activeGrp}</b>
        <>
          {' '}
          &nbsp;{' '}
          <span>
            естафета{legCount ? `, етапів: ${legCount}` : ''}
            {g?.controls ? <>&nbsp; {g.controls} КП</> : ''}
            {g?.distance_km ? <>&nbsp; {g.distance_km} км</> : ''}
          </span>
        </>
      </div>
      <div className="tbl-scroll">
        <table className="relay">
          <thead>
            <tr>
              <th className="rk">
                <span className="th-full">Місце</span>
                <span className="th-short">М</span>
              </th>
              <th className="col-num">№</th>
              <th>Команда / етап</th>
              <th className="col-club">Клуб</th>
              <th>Час</th>
              <th className="col-cum">
                <span className="th-full">Відставання / Σ</span>
                <span className="th-short">Σ</span>
              </th>
              {withPts && <th className="pts">Бали</th>}
            </tr>
          </thead>
          {list.map((t) => {
            const gap = t.result_seconds != null && winnerSec != null && t.rk != null
              ? fmtGap(t.result_seconds - winnerSec)
              : ''
            return (
              <tbody key={t.num} className="relay-team">
                <tr className="team-row">
                  <td className="rk">
                    {t.out_of_competition ? (
                      <span className="oc">П/К</span>
                    ) : t.status === 'finished' && t.rk ? (
                      <span className={MEDAL[t.rk] || ''}>{t.rk}</span>
                    ) : (
                      ''
                    )}
                  </td>
                  <td className="col-num">{t.num}</td>
                  <td>
                    <span className="team-name">{t.name || `Команда ${t.num}`}</span>
                    {t.restarted && <span className="tag">з перезапуском</span>}
                  </td>
                  <td className="col-club">{[t.club, t.region].filter(Boolean).join(', ')}</td>
                  <td>{teamTime(t)}</td>
                  <td className="col-cum">{gap && <span className="time">{gap}</span>}</td>
                  {withPts && <td className="pts">{t.points != null ? fmtPts(t.points) : ''}</td>}
                </tr>
                {(t.legs || []).map((l) => {
                  const extras = legExtras(l, t.columns)
                  return (
                    <tr key={l.leg} className="leg-row">
                      <td className="rk"></td>
                      <td className="col-num leg-bib">{l.bib}</td>
                      <td>
                        {l.full_name ? (
                          <span>{l.full_name}</span>
                        ) : (
                          <span className="vacant">етап {l.leg} — без учасника</span>
                        )}
                        {l.course && <span className="tag">дистанція {l.course}</span>}
                        {l.restarted && <span className="tag">перезапуск</span>}
                        {extras && <div className="leg-extra">{extras}</div>}
                      </td>
                      <td className="col-club"></td>
                      <td>{legTime(l)}</td>
                      <td className="col-cum">
                        {l.cumulative && <span className="time-plain">{l.cumulative}</span>}
                      </td>
                      {withPts && <td className="pts"></td>}
                    </tr>
                  )
                })}
              </tbody>
            )
          })}
        </table>
      </div>
      <p className="sub" style={{ marginTop: 8 }}>
        Фінішували: {fin} · на дистанції: {run}
        {dsq ? ` · знято: ${dsq}` : ''} · команд: {list.length}
      </p>
    </>
  )
}
