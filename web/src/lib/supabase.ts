import { createClient } from '@supabase/supabase-js'

// anon-ключ публічний і безпечний — RLS дозволяє лише читання (SELECT).
// Беремо з env (для гнучкості), із fallback на поточні значення, щоб білд
// працював «з коробки» навіть без налаштованих змінних оточення.
const SUPABASE_URL =
  import.meta.env.VITE_SUPABASE_URL ||
  'https://ayltwyedqgdzjckgdcgv.supabase.co'

const SUPABASE_ANON_KEY =
  import.meta.env.VITE_SUPABASE_ANON_KEY ||
  'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImF5bHR3eWVkcWdkempja2dkY2d2Iiwicm9sZSI6ImFub24iLCJpYXQiOjE3ODAwNjMzMTYsImV4cCI6MjA5NTYzOTMxNn0.piQSoL4H2HJPHuCWbepkCucVwf9MmYnoF-J8Zx9i_n8'

export const sb = createClient(SUPABASE_URL, SUPABASE_ANON_KEY)
