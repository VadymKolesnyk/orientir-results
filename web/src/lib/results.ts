// Чиста логіка, портована 1:1 з online/results.html: сортування, форматування
// балів, перелік груп, підпис дня, фільтр пошуку та агрегація режиму «Сума».

import { STATUS_ORDER } from '../types'
import type { EventDay, GroupRow, ResultRow } from '../types'

// Бали завжди з 2 знаками після коми: 100.00, 87.30, 95.42.
export function fmtPts(p: number | null | undefined): string {
  const n = Number(p)
  if (!isFinite(n)) return ''
  return n.toFixed(2)
}

// Порядок у групі: фініш (за місцем/часом) → finished_pending (за часом) →
// решта (за часом старту).
export function cmp(a: ResultRow, b: ResultRow): number {
  const sa = STATUS_ORDER[a.status] ?? 9
  const sbb = STATUS_ORDER[b.status] ?? 9
  if (sa !== sbb) return sa - sbb
  if (a.status === 'finished') {
    if (a.rk && b.rk) return a.rk - b.rk
    return (a.result_seconds ?? 1e9) - (b.result_seconds ?? 1e9)
  }
  if (a.status === 'finished_pending') // ще без місця — за часом
    return (a.result_seconds ?? 1e9) - (b.result_seconds ?? 1e9)
  return (a.start_time || '').localeCompare(b.start_time || '')
}

// Вкладки = групи дня з таблиці groups (за алфавітом, українська локаль).
export function groupNames(groups: GroupRow[]): string[] {
  return [...new Set(groups.map((g) => g.name))].sort((a, b) =>
    a.localeCompare(b, 'uk'),
  )
}

// Повний підпис дня (широкі екрани): «День 1 · Спринт» або «День 1».
export function dayLabel(d: EventDay): string {
  return d.label ? `День ${d.day} · ${d.label}` : `День ${d.day}`
}

// Короткий підпис дня (вузькі екрани): «День 1». Мітку дня (label) опускаємо —
// вона надто довга для маленької кнопки, лишаємо тільки «День N».
export function dayLabelShort(d: EventDay): string {
  return `День ${d.day}`
}

// Фільтр пошуку: ПІБ / команда / клуб / номер (lowercase substring).
export function matchesQuery(r: ResultRow, q: string): boolean {
  if (!q) return true
  return (
    (r.full_name || '').toLowerCase().includes(q) ||
    (r.team || '').toLowerCase().includes(q) ||
    (r.club || '').toLowerCase().includes(q) ||
    String(r.bib || '').includes(q)
  )
}

// --- Режим «Сума» -----------------------------------------------------
// Учасник = bib у межах активної групи. Колонки: місце | ПІБ | № | команда |
// [День N: місце · час · бали] × дні | Сума балів.
export interface SummaryPerson {
  bib: number
  name: string
  team: string
  club: string
  byDay: Record<number, ResultRow>
  total: number
}

// К-ть «результатів» учасника: дні, де він фінішував (не зняття/не-старт).
function finishCount(p: SummaryPerson): number {
  let n = 0
  for (const day in p.byDay) {
    const s = p.byDay[day].status
    if (s === 'finished' || s === 'finished_pending') n++
  }
  return n
}

// Сортування заліку «Сума» з тай-брейком за рівної суми балів:
//   1) більша сума балів;
//   2) більше результатів (фінішів, не DSQ);
//   3) від останнього дня до першого: більше балів того дня, а за рівних —
//      краще місце того дня;
//   4) у крайньому разі — за алфавітом (ПіБ).
export function compareSummary(
  a: SummaryPerson,
  b: SummaryPerson,
  days: number[],
): number {
  if (b.total !== a.total) return b.total - a.total // більша сума — вище

  const fa = finishCount(a)
  const fb = finishCount(b)
  if (fb !== fa) return fb - fa // більше результатів — вище

  // Від останнього дня до першого: спершу бали, потім місце того ж дня.
  for (let i = days.length - 1; i >= 0; i--) {
    const d = days[i]
    const ra = a.byDay[d]
    const rb = b.byDay[d]
    const pa = Number(ra?.points) || 0
    const pb = Number(rb?.points) || 0
    if (pb !== pa) return pb - pa // більше балів — вище
    const pla = ra?.rk ?? Number.MAX_SAFE_INTEGER
    const plb = rb?.rk ?? Number.MAX_SAFE_INTEGER
    if (pla !== plb) return pla - plb // краще (менше) місце — вище
  }

  // Усе однакове — за алфавітом ПІБ (українська локаль).
  return (a.name || '').localeCompare(b.name || '', 'uk')
}

export function aggregateSummary(
  allResults: ResultRow[],
  activeGrp: string,
): SummaryPerson[] {
  const m = new Map<number, SummaryPerson>()
  for (const r of allResults) {
    if (r.grp !== activeGrp) continue
    if (!m.has(r.bib))
      m.set(r.bib, {
        bib: r.bib,
        name: r.full_name || '',
        team: r.team || '',
        club: r.club || '',
        byDay: {},
        total: 0,
      })
    const p = m.get(r.bib)!
    p.byDay[r.day] = r
    // Найсвіжіше ПІБ/регіон/клуб беремо з будь-якого дня (можуть бути порожні).
    if (r.full_name) p.name = r.full_name
    if (r.team) p.team = r.team
    if (r.club) p.club = r.club
    p.total += Number(r.points) || 0 // бали є лише у фінішерів; решта — 0
  }
  return [...m.values()]
}

// Перелік днів за порядком (із event_days; запасний варіант — з даних).
export function summaryDays(
  eventDays: EventDay[],
  allResults: ResultRow[],
  fallbackDay: number,
): number[] {
  let days = eventDays.length
    ? eventDays.map((d) => d.day)
    : [...new Set(allResults.map((r) => r.day))].sort((a, b) => a - b)
  if (!days.length) days = [fallbackDay]
  return days
}

export function matchesPersonQuery(p: SummaryPerson, q: string): boolean {
  if (!q) return true
  return (
    (p.name || '').toLowerCase().includes(q) ||
    (p.team || '').toLowerCase().includes(q) ||
    (p.club || '').toLowerCase().includes(q) ||
    String(p.bib).includes(q)
  )
}
