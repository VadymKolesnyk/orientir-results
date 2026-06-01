// Сховище «обраних» учасників — суто локальне, у localStorage браузера (нічого
// не пишемо у Supabase). Обраний ідентифікується за номером (bib): за умовами
// змагань номер унікальний у межах усього змагання, тож він стабільний через
// усі групи та дні. Обрані прив'язані до конкретного event (ключ містить eventId),
// щоб номери різних змагань не змішувались.

// Маркер псевдогрупи «Обрані» (службове значення activeGrp). У звичайних назв
// груп такого символу не буває, тож колізій із реальною групою нема. У URL
// кодується окремо як сегмент «fav» (див. lib/route.ts).
export const FAV_GRP = '★'; // ★

const PREFIX = 'orientir:favs:';
const keyFor = (eventId: string) => PREFIX + eventId;

// Підписники на зміни обраних (для useSyncExternalStore). Зберігаємо також
// in-memory копію — фолбек, якщо localStorage недоступний (приватний режим).
const listeners = new Set<() => void>();
const memory = new Map<string, Set<number>>();

function emit(): void {
  for (const l of listeners) l();
}

// Зчитуємо набір обраних для змагання. Будь-яка помилка (вимкнений localStorage,
// зіпсований JSON) → беремо in-memory копію або порожній набір.
export function loadFavs(eventId: string): Set<number> {
  if (!eventId) return new Set();
  try {
    const raw = localStorage.getItem(keyFor(eventId));
    if (raw) {
      const arr = JSON.parse(raw) as unknown;
      if (Array.isArray(arr)) {
        const set = new Set(arr.filter((x): x is number => typeof x === 'number'));
        memory.set(eventId, set);
        return set;
      }
    }
  } catch {
    // localStorage недоступний — лишаємось на in-memory.
  }
  return memory.get(eventId) ?? new Set();
}

function saveFavs(eventId: string, set: Set<number>): void {
  memory.set(eventId, set);
  try {
    localStorage.setItem(keyFor(eventId), JSON.stringify([...set]));
  } catch {
    // Не вдалося зберегти (приватний режим/квота) — лишаємось на in-memory.
  }
}

// Перемикає обраність учасника й повертає новий набір (іммутабельно — щоб
// useSyncExternalStore бачив нову референцію). Сповіщає підписників.
export function toggleFav(eventId: string, bib: number): Set<number> {
  const cur = loadFavs(eventId);
  const next = new Set(cur);
  if (next.has(bib)) next.delete(bib);
  else next.add(bib);
  saveFavs(eventId, next);
  emit();
  return next;
}

// Підписка для useSyncExternalStore. Окремо слухаємо подію 'storage' — щоб
// зміни в одній вкладці відображались в інших.
export function subscribeFavs(cb: () => void): () => void {
  listeners.add(cb);
  const onStorage = (e: StorageEvent) => {
    if (e.key === null || e.key.startsWith(PREFIX)) cb();
  };
  window.addEventListener('storage', onStorage);
  return () => {
    listeners.delete(cb);
    window.removeEventListener('storage', onStorage);
  };
}
