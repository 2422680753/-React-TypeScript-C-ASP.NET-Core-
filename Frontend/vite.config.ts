import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import path from 'path'

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    port: 3000,
    proxy: {
      '/api': {
        target: 'https://localhost:7045',
        changeOrigin: true,
        secure: false,
      },
      '/monitoringHub': {
        target: 'https://localhost:7045',
        ws: true,
        changeOrigin: true,
        secure: false,
      },
    },
  },
})
