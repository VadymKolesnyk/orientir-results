import { useCallback, useMemo, useRef, useSyncExternalStore } from 'react'
import { loadFavs, subscribeFavs, toggleFav } from '../lib/favorites'

// React-обгортка над сховищем обраних (lib/favorites.ts). Повертає реактивний
// набір обраних номерів (bib) поточного змагання + хелпери has/toggle.
//
// useSyncExternalStore вимагає СТАБІЛЬНОГО getSnapshot: повертати ту саму
// референцію, доки дані не змінились — інакше нескінченний ререндер. loadFavs
// щоразу будує новий Set, тож кешуємо останній снапшот і віддаємо нову
// референцію лише коли реально змінився вміст (звіряємо за серіалізацією).
export function useFavorites(eventId: string) {
  const cacheRef = useRef<{ key: string; set: Set<number> }>({
    key: '',
    set: new Set(),
  })

  const getSnapshot = useCallback(() => {
    const fresh = loadFavs(eventId)
    const key = eventId + '|' + [...fresh].sort((a, b) => a - b).join(',')
    if (cacheRef.current.key !== key) {
      cacheRef.current = { key, set: fresh }
    }
    return cacheRef.current.set
  }, [eventId])

  const favs = useSyncExternalStore(subscribeFavs, getSnapshot, getSnapshot)

  const has = useCallback((bib: number) => favs.has(bib), [favs])
  const toggle = useCallback(
    (bib: number) => {
      if (eventId) toggleFav(eventId, bib)
    },
    [eventId],
  )

  // Стабільний масив номерів для запитів (.in('bib', …)).
  const bibs = useMemo(() => [...favs], [favs])

  return { favs, bibs, has, toggle }
}
