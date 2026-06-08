-- =====================================================================
--  Схема онлайн-результатів для Supabase (PostgreSQL).
--  Підтримує БАГАТО змагань, у кожного — довільна кількість днів.
--  Структура «Орієнтир» (OLD.DBF + GRUPA.DBF), кодування Windows-1251.
--
--  Виконати один раз: Supabase → SQL Editor → New query → вставити → Run.
--  ⚠️ Скрипт ПЕРЕСТВОРЮЄ таблиці (drop+create). Якщо в тебе вже є дані,
--     які треба зберегти — закоментуй блок DROP нижче.
-- =====================================================================

drop table if exists public.results    cascade;
drop table if exists public.groups     cascade;
drop table if exists public.event_days cascade;
drop table if exists public.events     cascade;

-- ---------------------------------------------------------------------
--  Змагання (верхня сутність).
-- ---------------------------------------------------------------------
create table public.events (
  id          text primary key,        -- slug: "kyiv2025" (з URL ?event=...)
  title       text not null,           -- "Kyiv City Race"
  subtitle    text,                    -- "31.05–01.06.2025"
  days_count  int  not null default 1,
  standings   boolean not null default false, -- вмикає вкладку «Сума» (залік) на сторінці
  points      boolean not null default false, -- показувати колонку «Бали»
  display_config jsonb,                        -- конфіг колонок (які/порядок/великий-малий екран)
  updated_at  timestamptz not null default now()
);

-- Дні змагання (для вкладок і підписів на сторінці).
create table public.event_days (
  event   text not null references public.events(id) on delete cascade,
  day     int  not null,
  label   text,                        -- "30 травня"
  ord     int  not null default 0,
  primary key (event, day)
);

-- ---------------------------------------------------------------------
--  Параметри груп (GRUPA.DBF): довжина, к-сть КП.
-- ---------------------------------------------------------------------
create table public.groups (
  event       text not null,
  name        text not null,           -- "Ч18", "Ж21Е", "OPEN"
  day         int  not null default 1,
  distance_km numeric,                 -- DLINA
  controls    int,                     -- KP (к-сть КП)
  ord         int  not null default 0, -- порядок виводу (як у GRUPA.DBF)
  primary key (event, name, day)
);

-- ---------------------------------------------------------------------
--  Результати учасників (OLD.DBF). Рядок = учасник у дні змагання.
-- ---------------------------------------------------------------------
create table public.results (
  event          text not null,
  bib            int  not null,        -- NOMER
  day            int  not null default 1,

  grp            text not null,        -- GRUP ("Ч18")
  rk             int,                  -- M_1: місце (порожнє = ще нема)
  full_name      text not null,        -- FAM
  team           text,                 -- KOM1 (область/регіон) — як "Team" на бланку
  club           text,                 -- KOM2 (клуб)
  region         text,                 -- дубль KOM1 для зручності фільтра
  birth          text,                 -- GR ("15.06.2007")
  qual           text,                 -- KVAL виконаний розряд / W_1
  reason         text,                 -- причина зняття для показу: "MP", "DNS"…

  start_time     text,                 -- S_1 "13:42:31"
  finish_time    text,                 -- F_1 "14:01:03.3"
  result_time    text,                 -- R_1 "00:18:32"
  result_seconds int,                  -- R_1 у секундах (для сортування)
  points         numeric,              -- бали: 100*(2 - час/час_переможця); NULL = не фінішував

  -- Обчислений статус (рахується в publisher, спирається на U_DAL, а НЕ на M_1=0):
  --  finished         — фініш + місце (M_1 > 0)
  --  finished_pending — фініш є, час є, місце ще рахується (M_1 = 0, без зняття)
  --  running          — стартував, ще не фінішував
  --  dsq              — знятий (U_DAL = MP / DNS / …)
  --  dns              — не стартував
  status         text not null default 'dns'
                 check (status in ('finished','finished_pending','running','dsq','dnf','dns')),

  updated_at     timestamptz not null default now(),

  primary key (event, bib, day)
);

create index results_grp_idx on public.results (event, day, grp, rk);

-- ---------------------------------------------------------------------
--  RLS: глядачі читають через anon-ключ (тільки SELECT).
--  Запис іде з publisher через service_role-ключ (обходить RLS).
-- ---------------------------------------------------------------------
alter table public.events     enable row level security;
alter table public.event_days enable row level security;
alter table public.groups     enable row level security;
alter table public.results    enable row level security;

create policy "read events"     on public.events     for select to anon using (true);
create policy "read event_days" on public.event_days for select to anon using (true);
create policy "read groups"     on public.groups     for select to anon using (true);
create policy "read results"    on public.results    for select to anon using (true);

-- ---------------------------------------------------------------------
--  Якщо БД уже існує і таблиці перестворювати НЕ хочеш — додай лише нові
--  колонки (бали + прапорець заліку) без drop:
--    alter table public.results add column if not exists points numeric;
--    alter table public.events  add column if not exists standings boolean not null default false;
--    alter table public.events  add column if not exists points boolean not null default false;
--    alter table public.events  add column if not exists display_config jsonb;
-- ---------------------------------------------------------------------

-- (Опційно) Realtime — миттєве оновлення сторінки:
-- alter publication supabase_realtime add table public.results;
-- alter publication supabase_realtime add table public.groups;
-- alter publication supabase_realtime add table public.event_days;
