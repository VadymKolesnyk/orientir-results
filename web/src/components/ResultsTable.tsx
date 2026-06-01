import { cmp, fmtPts, matchesQuery } from '../lib/results'
import type { GroupRow, ResultRow } from '../types'

const MEDAL = ['', 'gold', 'silver', 'bronze']

interface Props {
  results: ResultRow[]
  groups: GroupRow[]
  activeGrp: string
  query: string
  showPts: boolean
}

// Час/місце залежно від статусу учасника (1:1 з rowHtml у results.html).
function ResultRowView({ r, showPts }: { r: ResultRow; showPts: boolean }) {
  let rk: React.ReactNode = ''
  let time: React.ReactNode = ''

  if (r.status === 'finished') {
    rk = <span className={MEDAL[r.rk ?? 0] || ''}>{r.rk || ''}</span>
    time = <span className="time">{r.result_time || ''}</span>
  } else if (r.status === 'finished_pending') {
    // Фінішував, місце ще рахується — показуємо час, але без номера місця.
    time = (
      <>
        <span className="time">{r.result_time || ''}</span>{' '}
        <span className="b-running" style={{ fontSize: 11 }}>
          (обробка)
        </span>
      </>
    )
  } else if (r.status === 'running') {
    time = <span className="b-running">на дистанції</span>
  } else if (r.status === 'dsq') {
    // Показуємо причину з U_DAL ("MP", "DNS"…), як на бланку.
    time = <span className="flag">{r.reason || 'зн.'}</span>
  } else if (r.status === 'dns') {
    time = (
      <span className="b-running" style={{ color: '#9aa2b8' }}>
        не старт.
      </span>
    )
  }

  return (
    <tr>
      <td className="rk">{rk}</td>
      <td>{r.full_name}</td>
      <td className="col-bib">{r.bib || ''}</td>
      <td>{r.team || ''}</td>
      <td className="col-club">{r.club || ''}</td>
      <td className="time col-start" style={{ fontWeight: 400 }}>
        {r.start_time || ''}
      </td>
      <td>{time}</td>
      {showPts && (
        <td className={`pts${r.points === 100 ? ' win' : ''}`}>
          {r.points != null ? fmtPts(r.points) : ''}
        </td>
      )}
    </tr>
  )
}

export function ResultsTable({
  results,
  groups,
  activeGrp,
  query,
  showPts,
}: Props) {
  const q = query.trim().toLowerCase()
  // results уже містить лише активну групу (запит фіксує grp), тож не фільтруємо.
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
      <div className="tbl-scroll">
        <table>
          <thead>
            <tr>
              <th className="rk">
                <span className="th-full">Місце</span>
                <span className="th-short">М</span>
              </th>
              <th>Прізвище, ім'я</th>
              <th className="col-bib">№</th>
              <th>Регіон</th>
              <th className="col-club">Клуб</th>
              <th className="col-start">Старт</th>
              <th>Час</th>
              {showPts && <th className="pts">Бали</th>}
            </tr>
          </thead>
          <tbody>
            {list.map((r) => (
              <ResultRowView key={r.bib} r={r} showPts={showPts} />
            ))}
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
