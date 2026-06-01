# web/ — глядацький фронт (React + Vite + Tailwind)

React-порт `online/results.html`. Показує live-результати орієнтування з Supabase:
перемикач днів, вкладки груп, таблицю результатів, режим **«Сума»** (залік за всі дні),
пошук та live-оновлення (Realtime + опитування раз на 10 с) з авто-офлайном.

Старий `online/results.html` лишається в репо як референс/бекап — джерелом GitHub
Pages після цієї міграції стає білд із `web/`.

## Розробка

```powershell
cd web
npm install
npm run dev
# відкрити http://localhost:5173/orientir-results/#/<id>/d1
```

> `base` у `vite.config.ts` = `/orientir-results/` (ім'я репо), тож і локально, і на
> Pages URL містить цей префікс.
>
> **Маршрут** задається через hash: `#/{event}/{d1|d2|…|sum}`. Hash (а не звичайний
> шлях) обрано свідомо — GitHub Pages віддає статику, тож прямий перехід на справжній
> шлях `/orientir-results/<id>/d1` дав би 404. Із hash F5 і прямі лінки працюють без
> `404.html`. День — сегмент `dN` (`d1`, `d2`, …), залік — `sum`. Старий
> `?event=<id>&day=<n>` (зокрема `day=summ`) ще читається як запасний варіант, щоб не
> ламати раніше роздані лінки.

## Білд

```powershell
npm run build      # tsc + vite build → web/dist
npm run preview    # локальний перегляд білда
```

## Supabase-ключі

`src/lib/supabase.ts` читає `VITE_SUPABASE_URL` / `VITE_SUPABASE_ANON_KEY` з оточення,
а за їх відсутності використовує публічний **anon**-ключ (RLS дозволяє лише читання —
безпечно тримати у фронті). Перевизначити можна через `web/.env.local` або
repo-variables в Actions. **service_role**-ключ сюди НЕ потрапляє ніколи.

## Деплой (GitHub Pages через Actions)

Воркфлоу `.github/workflows/deploy-pages.yml` білдить `web/` і деплоїть на Pages при
кожному пуші в `master` (офіційний artifact-флоу, без гілки `gh-pages`).

**Одноразове налаштування після першого мерджу:** у GitHub репо →
**Settings → Pages → Source = GitHub Actions**.

Після успішного деплою сайт буде на:

```
https://vadymkolesnyk.github.io/orientir-results/#/<id>/d<n>     # день n
https://vadymkolesnyk.github.io/orientir-results/#/<id>/sum      # залік
```

## Зв'язок із publisher (.NET)

URL змінився (зник `/online/results.html`). У застосунку (Orientir Desktop/Console)
онови поле **«Публічний URL»** на новий base:

```
https://vadymkolesnyk.github.io/orientir-results/
```

Це runtime-налаштування (зберігається в SQLite) — **код .NET міняти не треба**.

> ⚠️ **Формат лінка змінився.** Якщо publisher формує посилання як
> `…/orientir-results/?event=…&day=…`, воно ще працюватиме (фронт читає старий query
> як запасний варіант), але новий канонічний формат — hash: `#/{event}/{d1|d2|sum}`.
> Коли оновлюватимеш генерацію лінків у publisher, переходь на hash-формат.
