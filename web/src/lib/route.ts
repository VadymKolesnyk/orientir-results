// Hash-роутинг: URL виглядає як …/orientir-results/#/{event}/{d1|d2|sum}.
// Hash (а не звичайний шлях) обрано свідомо — GitHub Pages віддає статику,
// тож прямий перехід на справжній шлях /event/d1 дав би 404. Із hash увесь
// маршрут лишається на клієнті, F5 і прямі лінки працюють без 404.html.

export interface Route {
  event: string
  // День змагання (1, 2, …). У режимі «Сума» day лишається останнім обраним.
  day: number
  sumMode: boolean
}

// Парсимо «#/{event}/{d1|d2|sum}». Сегмент дня: dN → день N; sum → залік.
// Запасний шлях: підтримуємо старий ?event=&day= (зокрема ?day=summ), щоб не
// ламати раніше роздані лінки.
export function parseRoute(): Route {
  const hash = location.hash.replace(/^#\/?/, '') // прибираємо «#/» або «#»
  const parts = hash.split('/').filter(Boolean)

  if (parts.length) {
    const event = decodeURIComponent(parts[0] || '')
    const seg = (parts[1] || '').toLowerCase()
    if (seg === 'sum') return { event, day: 1, sumMode: true }
    const n = Number(seg.replace(/^d/, ''))
    return { event, day: Number.isFinite(n) && n > 0 ? n : 1, sumMode: false }
  }

  // --- Запасний варіант: старий query-формат ---
  const params = new URLSearchParams(location.search)
  const event = params.get('event') || ''
  const dayParam = (params.get('day') || '').toLowerCase()
  if (dayParam === 'summ' || dayParam === 'sum')
    return { event, day: 1, sumMode: true }
  return { event, day: Number(dayParam) || 1, sumMode: false }
}

// Будуємо hash «#/{event}/{dN|sum}». Без event — порожній hash.
export function buildHash(r: Route): string {
  if (!r.event) return ''
  const seg = r.sumMode ? 'sum' : `d${r.day}`
  return `#/${encodeURIComponent(r.event)}/${seg}`
}

// Оновлюємо лише hash, не чіпаючи history-стек (replace, як було раніше).
export function replaceHash(r: Route): void {
  const h = buildHash(r)
  const u = new URL(location.href)
  u.hash = h
  u.search = '' // прибираємо старий ?event=&day=, якщо лишився
  history.replaceState(null, '', u)
}
