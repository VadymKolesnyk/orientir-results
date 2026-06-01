import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// Pages розгортає проектний репо в підшлях з ім'ям репо, тож base має його містити,
// інакше asset-шляхи (/assets/...) дадуть 404. Сайт буде на …/orientir-results/.
export default defineConfig({
  base: '/orientir-results/',
  plugins: [react(), tailwindcss()],
})
