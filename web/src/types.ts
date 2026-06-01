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
export const RES_COLS =
  'bib,grp,day,rk,full_name,team,club,status,reason,start_time,result_time,result_seconds,points,updated_at';
export const GRP_COLS = 'name,controls,distance_km,ord';

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
  ord: number | null;
}

export interface ResultRow {
  bib: number;
  grp: string;
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
}
