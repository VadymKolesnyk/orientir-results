import { fmtPts, matchesQuery } from '../lib/results'
import { StarButton } from './StarButton'
import type { ResultRow } from '../types'

const MEDAL = ['', 'gold', 'silver', 'bronze']

interface Props {
  // Рядки обраних учасників за поточний день (з усіх груп). Містять grp.
  results: ResultRow[]
  query: string
  showPts: boolean
  isFav: (bib: number) => boolean
  onToggleFav: (bib: number) => void
  // Перехід на групу учасника (клік по назві групи в списку обраних).
  onSelectGroup: (grp: string) => void
}

// Час/місце за статусом — спрощена версія з ResultsTable (без колонок старту).
function timeCell(r: ResultRow): React.ReactNode {
  if (r.status === 'finished')
    return <span className="time">{r.result_time || ''}</span>
  if (r.status === 'finished_pending')
    return (
      <>
        <span className="time">{r.result_time || ''}</span>{' '}
        <span className="b-running" style={{ fontSize: 11 }}>
          (обробка)
        </span>
      </>
    )
  if (r.status === 'running')
    return <span className="b-running">на дистанції</span>
  if (r.status === 'dsq') return <span className="flag">{r.reason || 'зн.'}</span>
  if (r.status === 'dns')
    return (
      <span className="b-running" style={{ color: '#9aa2b8' }}>
        не старт.
      </span>
    )
  return ''
}

// Вид «Обрані»: простий алфавітний список (за ПІБ, українська локаль) усіх
// позначених зірочкою учасників — незалежно від групи. Для кожного показуємо
// групу, місце/час за поточний день і (за наявності) бали. Зняття зірочки прибирає
// учасника зі списку (через оновлення favs у App → переотримання даних).
export function FavoritesList({
  results,
  query,
  showPts,
  isFav,
  onToggleFav,
  onSelectGroup,
}: Props) {
  const q = query.trim().toLowerCase()
  let list = q ? results.filter((r) => matchesQuery(r, q)) : results

  if (!list.length) {
    const msg = q
      ? {
          title: 'Нічого не знайдено',
          sub: `Серед обраних немає «${query.trim()}»`,
        }
      : {
          title: 'Ще нікого не додано в обрані',
          sub: 'Натисніть зірочку ☆ біля учасника в будь-якій групі',
        }
    return (
      <div className="empty">
        <div className="empty-title">{msg.title}</div>
        <div className="empty-sub">{msg.sub}</div>
      </div>
    )
  }

  // Алфавітно за ПІБ (українська локаль); за рівних — за номером.
  list = [...list].sort(
    (a, b) =>
      (a.full_name || '').localeCompare(b.full_name || '', 'uk') ||
      a.bib - b.bib,
  )

  return (
    <>
      <div className="ghead">
        <b className="fav-title">★ Обрані</b>{' '}
        <span>учасників: {list.length}</span>
      </div>
      <div className="tbl-scroll">
        <table>
          <thead>
            <tr>
              <th className="col-star" aria-label="Обране"></th>
              <th>Прізвище, ім'я</th>
              <th className="col-bib">№</th>
              <th>Група</th>
              <th className="rk">
                <span className="th-full">Місце</span>
                <span className="th-short">М</span>
              </th>
              <th>Час</th>
              {showPts && <th className="pts">Бали</th>}
            </tr>
          </thead>
          <tbody>
            {list.map((r) => {
              const rk =
                r.status === 'finished' ? (
                  <span className={MEDAL[r.rk ?? 0] || ''}>{r.rk || ''}</span>
                ) : (
                  ''
                )
              return (
                <tr key={r.bib} className="fav-row">
                  <td className="col-star">
                    <StarButton
                      active={isFav(r.bib)}
                      onToggle={() => onToggleFav(r.bib)}
                    />
                  </td>
                  <td>{r.full_name}</td>
                  <td className="col-bib">{r.bib || ''}</td>
                  <td>
                    {r.grp ? (
                      <a
                        className="grp-link"
                        onClick={() => onSelectGroup(r.grp as string)}
                      >
                        {r.grp}
                      </a>
                    ) : (
                      ''
                    )}
                  </td>
                  <td className="rk">{rk}</td>
                  <td>{timeCell(r)}</td>
                  {showPts && (
                    <td className={`pts${r.points === 100 ? ' win' : ''}`}>
                      {r.points != null ? fmtPts(r.points) : ''}
                    </td>
                  )}
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>
    </>
  )
}
