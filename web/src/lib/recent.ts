// Список нещодавно відкритих змагань — суто локальний (localStorage), щоб на
// кореневій сторінці (без event у URL) запропонувати швидко повернутись до
// одного з них. Зберігаємо до 5 останніх; найсвіжіше — першим.

export interface RecentEvent {
  id: string
  title: string // людська назва (або id, якщо назви ще нема)
}

const KEY = 'orientir:recent'
const MAX = 5

export function loadRecent(): RecentEvent[] {
  try {
    const raw = localStorage.getItem(KEY)
    if (raw) {
      const arr = JSON.parse(raw) as unknown
      if (Array.isArray(arr)) {
        return arr
          .filter(
            (x): x is RecentEvent =>
              !!x && typeof x.id === 'string' && typeof x.title === 'string',
          )
          .slice(0, MAX)
      }
    }
  } catch {
    // localStorage недоступний (приватний режим) — порожній список.
  }
  return []
}

// Додає/піднімає змагання на початок списку (за id). Дублікати прибираємо,
// title оновлюємо на найсвіжіший. Обрізаємо до MAX.
export function pushRecent(id: string, title: string): void {
  if (!id) return
  try {
    const list = loadRecent().filter((e) => e.id !== id)
    list.unshift({ id, title: title || id })
    localStorage.setItem(KEY, JSON.stringify(list.slice(0, MAX)))
  } catch {
    // Не вдалося зберегти — мовчазний фолбек (функція некритична).
  }
}
