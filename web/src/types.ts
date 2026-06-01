// Типи відповідають таблицям Supabase (online/schema.sql) і колонкам,
// що реально тягне сторінка (RES_COLS / GRP_COLS з online/results.html).

export type Status =
  | 'finished'
  | 'finished_pending'
  | 'running'
  | 'dnf'
  | 'dsq'
  | 'dns';

// Порядок статусів у групі: фініш (за місцем) → ще обробляються → біжать →
// зняті → не старт. (1:1 зі STATUS_ORDER у results.html.)
export const STATUS_ORDER: Record<string, number> = {
  finished: 0,
  finished_pending: 1,
  running: 2,
  dnf: 3,
  dsq: 4,
  dns: 5,
};

// Тягнемо лише ті колонки, що реально показує/використовує сторінка (не *),
// щоб зменшити трафік. Пропущені: finish_time, region, birth, qual.
// grp не тягнемо: кожен запит до results фіксує .eq('grp', …), тож значення
// в усіх рядках однакове й відоме заздалегідь. day лишаємо — у режимі «Сума»
// тягнемо групу за ВСІ дні й розрізняємо рядки саме за day.
export const RES_COLS =
  'bib,day,rk,full_name,team,club,status,reason,start_time,result_time,result_seconds,points,updated_at';

// Для виду «Обрані» тягнемо ще й grp — учасники зібрані з різних груп, тож треба
// показати, з якої кожен (звичайні запити grp не тягнуть навмисно — він сталий
// у межах запиту). grp додаємо в ResultRow як необов'язкове поле.
export const RES_COLS_FAV = RES_COLS + ',grp';

// Фоновий sync (polling/realtime) тягне ЛИШЕ ці колонки: ключ (bib,day) +
// поля, що реально змінюються під час перегонів. Решта (full_name, team, club,
// start_time) під час змагання стабільна — її лишаємо з уже завантаженого рядка.
// Нові учасники (невідомий bib) не мають цих сталих полів — тоді робимо
// разовий повний дозапит (див. useLiveResults.syncResults).
export const SYNC_COLS =
  'bib,day,rk,status,reason,result_time,result_seconds,points,updated_at';
// Підмножина мутабельних полів, що накладаємо на наявний рядок при merge.
export type SyncRow = Pick<
  ResultRow,
  | 'bib'
  | 'day'
  | 'rk'
  | 'status'
  | 'reason'
  | 'result_time'
  | 'result_seconds'
  | 'points'
  | 'updated_at'
>;

// ord не тягнемо: вкладки груп сортуються за алфавітом (groupNames), не за ord.
export const GRP_COLS = 'name,controls,distance_km';

export interface EventRow {
  id: string;
  title: string | null;
  subtitle: string | null;
  standings: boolean | null;
}

export interface EventDay {
  day: number;
  label: string | null;
  ord: number | null;
}

export interface GroupRow {
  name: string;
  controls: number | null;
  distance_km: number | null;
}

export interface ResultRow {
  bib: number;
  day: number;
  rk: number | null;
  full_name: string | null;
  team: string | null;
  club: string | null;
  status: Status;
  reason: string | null;
  start_time: string | null;
  result_time: string | null;
  result_seconds: number | null;
  points: number | null;
  updated_at: string;
  // Назва групи. Тягнемо лише для виду «Обрані» (RES_COLS_FAV); інакше undefined.
  grp?: string | null;
}
