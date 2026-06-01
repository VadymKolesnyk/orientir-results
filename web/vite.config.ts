import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// Сайт віддається з кореня власного домену (custom domain у Settings → Pages),
// тож base = '/'. Підшлях /orientir-results/ більше не потрібен. CNAME-файл у
// public/ зберігає домен при кожному деплої через GitHub Actions.
export default defineConfig({
  base: '/',
  plugins: [react(), tailwindcss()],
})
