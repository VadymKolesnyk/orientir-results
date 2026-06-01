import { Fragment } from 'react'
import {
  aggregateSummary,
  compareSummary,
  fmtPts,
  matchesPersonQuery,
  summaryDays,
} from '../lib/results'
import { StarButton } from './StarButton'
import { FAV_GRP } from '../lib/favorites'
import type { EventDay, ResultRow } from '../types'

const MEDAL = ['', 'gold', 'silver', 'bronze']

interface Props {
  allResults: ResultRow[]
  eventDays: EventDay[]
  activeGrp: string
  day: number
  query: string
  isFav: (bib: number) => boolean
  onToggleFav: (bib: number) => void
}

// Режим «Сума»: залік за всі дні для активної групи. Учасник = bib.
// Колонки: місце | ПІБ | № | команда | [День N: М · Час · бали] × дні | Сума.
export function SummaryTable({
  allResults,
  eventDays,
  activeGrp,
  day,
  query,
  isFav,
  onToggleFav,
}: Props) {
  const q = query.trim().toLowerCase()
  const fav = activeGrp === FAV_GRP
  const days = summaryDays(eventDays, allResults, day)

  // Місце рахуємо за ПОВНИМ заліком (після сортування), і лише потім фільтруємо
  // за пошуком — інакше місце «з'їжджало» б на 1, 2, … у відфільтрованому списку.
  const ranked = [...aggregateSummary(allResults)]
    .sort((a, b) => compareSummary(a, b, days))
    .map((p, i) => ({ p, rank: i + 1 }))
  const rows = q ? ranked.filter(({ p }) => matchesPersonQuery(p, q)) : ranked

  if (!rows.length) {
    const msg = q
      ? {
          title: 'Нічого не знайдено',
          sub: `За запитом «${query.trim()}» немає учасників`,
        }
      : fav
        ? {
            title: 'Ще нікого не додано в обрані',
            sub: 'Натисніть зірочку ☆ біля учасника в будь-якій групі',
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

  return (
    <>
      <div className="ghead">
        <b className={fav ? 'fav-title' : ''}>
          {fav ? '★ Обрані' : activeGrp}
        </b>{' '}
        <span>залік за {days.length} дн.</span>
      </div>
      <div className="tbl-scroll">
        <table>
          <thead>
            <tr>
              <th className="col-star" aria-label="Обране"></th>
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
            {rows.map(({ p, rank }) => {
              const medal = MEDAL[rank] || ''
              const fav = isFav(p.bib)
              return (
                <tr key={p.bib} className={fav ? 'fav-row' : ''}>
                  <td className="col-star">
                    <StarButton
                      active={fav}
                      onToggle={() => onToggleFav(p.bib)}
                    />
                  </td>
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
