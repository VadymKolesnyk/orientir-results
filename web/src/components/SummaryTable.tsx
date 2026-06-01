import { Fragment } from 'react'
import {
  aggregateSummary,
  compareSummary,
  fmtPts,
  matchesPersonQuery,
  summaryDays,
} from '../lib/results'
import type { EventDay, ResultRow } from '../types'

const MEDAL = ['', 'gold', 'silver', 'bronze']

interface Props {
  allResults: ResultRow[]
  eventDays: EventDay[]
  activeGrp: string
  day: number
  query: string
}

// Режим «Сума»: залік за всі дні для активної групи. Учасник = bib.
// Колонки: місце | ПІБ | № | команда | [День N: М · Час · бали] × дні | Сума.
export function SummaryTable({
  allResults,
  eventDays,
  activeGrp,
  day,
  query,
}: Props) {
  const q = query.trim().toLowerCase()
  const days = summaryDays(eventDays, allResults, day)

  let people = aggregateSummary(allResults)
  if (q) people = people.filter((p) => matchesPersonQuery(p, q))

  if (!people.length) {
    const msg = q
      ? {
          title: 'Нічого не знайдено',
          sub: `За запитом «${query.trim()}» немає учасників`,
        }
      : {
          title: 'Залік ще порожній',
          sub: 'Бали з’являться після перших фінішів у групі',
        }
    return (
      <div className="empty">
        <div className="empty-title">{msg.title}</div>
        <div className="empty-sub">{msg.sub}</div>
      </div>
    )
  }

  // Сортування за сумою балів (спадання) з тай-брейком: к-ть результатів,
  // потім бали за останнім → попереднім днем.
  people = [...people].sort((a, b) => compareSummary(a, b, days))

  return (
    <>
      <div className="ghead">
        <b>{activeGrp}</b> <span>залік за {days.length} дн.</span>
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
              <th className="col-detail">Регіон</th>
              <th className="col-club">Клуб</th>
              {days.map((d) => (
                <Fragment key={d}>
                  <th className="col-detail">М</th>
                  <th className="col-detail">Час</th>
                  <th className="pts">Д{d}</th>
                </Fragment>
              ))}
              <th className="total">Сума</th>
            </tr>
          </thead>
          <tbody>
            {people.map((p, i) => {
              const rank = i + 1
              const medal = MEDAL[rank] || ''
              return (
                <tr key={p.bib}>
                  <td className="rk">
                    <span className={medal}>{rank}</span>
                  </td>
                  <td>{p.name || ''}</td>
                  <td className="col-bib">{p.bib || ''}</td>
                  <td className="col-detail">{p.team || ''}</td>
                  <td className="col-club">{p.club || ''}</td>
                  {days.map((d) => {
                    const r = p.byDay[d]
                    const fin =
                      r &&
                      (r.status === 'finished' ||
                        r.status === 'finished_pending')
                    const pl =
                      r && r.status === 'finished' && r.rk ? r.rk : '–'
                    const tm = fin ? r.result_time || '' : '–'
                    const pts = r && r.points != null ? fmtPts(r.points) : '0'
                    const win = r && r.points === 100 ? ' win' : ''
                    return (
                      <Fragment key={d}>
                        <td className="col-detail daycell">
                          <span className="pl">{pl}</span>
                        </td>
                        <td className="col-detail daycell">
                          <span className="tm">{tm}</span>
                        </td>
                        <td className={`pts${win}`}>{pts}</td>
                      </Fragment>
                    )
                  })}
                  <td className="total">{fmtPts(p.total)}</td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>
    </>
  )
}
