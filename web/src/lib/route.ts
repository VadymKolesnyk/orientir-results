// Hash-роутинг: URL виглядає як …/#/{event}/{d1|d2|sum}[/{grp}].
// Hash (а не звичайний шлях) обрано свідомо — GitHub Pages віддає статику,
// тож прямий перехід на справжній шлях /event/d1 дав би 404. Із hash увесь
// маршрут лишається на клієнті, F5 і прямі лінки працюють без 404.html.

import { FAV_GRP } from './favorites'

// Псевдогрупу «Обрані» (FAV_GRP = «★») у URL кодуємо стабільним сегментом «fav»,
// щоб лінк був читабельний і не залежав від символу зірочки в адресі.
const FAV_SEG = 'fav'

export interface Route {
  event: string
  // День змагання (1, 2, …). У режимі «Сума» day лишається останнім обраним.
  day: number
  sumMode: boolean
  // Активна група (необов'язкова). Зберігаємо в URL, щоб після F5 лишатись
  // на тій самій групі. Порожня → App обере першу доступну.
  grp: string
}

// Парсимо «#/{event}/{d1|d2|sum}[/{grp}]». Сегмент дня: dN → день N; sum → залік.
// Третій сегмент (за наявності) — активна група.
// Запасний шлях: підтримуємо старий ?event=&day= (зокрема ?day=summ), щоб не
// ламати раніше роздані лінки.
export function parseRoute(): Route {
  const hash = location.hash.replace(/^#\/?/, '') // прибираємо «#/» або «#»
  const parts = hash.split('/').filter(Boolean)

  if (parts.length) {
    const event = decodeURIComponent(parts[0] || '')
    const seg = (parts[1] || '').toLowerCase()
    const rawGrp = decodeURIComponent(parts[2] || '')
    // Сегмент «fav» → маркер псевдогрупи «Обрані».
    const grp = rawGrp.toLowerCase() === FAV_SEG ? FAV_GRP : rawGrp
    if (seg === 'sum') return { event, day: 1, sumMode: true, grp }
    const n = Number(seg.replace(/^d/, ''))
    return {
      event,
      day: Number.isFinite(n) && n > 0 ? n : 1,
      sumMode: false,
      grp,
    }
  }

  // --- Запасний варіант: старий query-формат ---
  const params = new URLSearchParams(location.search)
  const event = params.get('event') || ''
  const dayParam = (params.get('day') || '').toLowerCase()
  if (dayParam === 'summ' || dayParam === 'sum')
    return { event, day: 1, sumMode: true, grp: '' }
  return { event, day: Number(dayParam) || 1, sumMode: false, grp: '' }
}

// Будуємо hash «#/{event}/{dN|sum}[/{grp}]». Без event — порожній hash.
// Групу додаємо лише коли вона задана.
export function buildHash(r: Route): string {
  if (!r.event) return ''
  const seg = r.sumMode ? 'sum' : `d${r.day}`
  const grpVal = r.grp === FAV_GRP ? FAV_SEG : r.grp
  const grpSeg = grpVal ? `/${encodeURIComponent(grpVal)}` : ''
  return `#/${encodeURIComponent(r.event)}/${seg}${grpSeg}`
}

// Оновлюємо лише hash, не чіпаючи history-стек (replace, як було раніше).
export function replaceHash(r: Route): void {
  const h = buildHash(r)
  const u = new URL(location.href)
  u.hash = h
  u.search = '' // прибираємо старий ?event=&day=, якщо лишився
  history.replaceState(null, '', u)
}
