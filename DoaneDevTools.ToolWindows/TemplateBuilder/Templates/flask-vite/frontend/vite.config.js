import { defineConfig } from 'vite'
import {{ frontend_framework }} from '@vitejs/plugin-{{ frontend_framework }}'

export default defineConfig({
  plugins: [{{ frontend_framework }}()],
  server: {
    port: {{ vite_port }},
    proxy: {
      '/api': 'http://localhost:{{ port }}',
      '/health': 'http://localhost:{{ port }}'
    }
  },
  build: {
    outDir: 'dist'
  }
})
