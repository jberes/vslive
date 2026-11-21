/// <reference types="vitest/config" />
import { playwright } from '@vitest/browser-playwright'
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vitejs.dev/config/
export default defineConfig({
  build: {
    chunkSizeWarningLimit: 10 * 1024 * 1024, // 10 MB
  },
  plugins: [react()],
  test: {
    browser: {
      enabled: true,
      provider: playwright(),
      instances: [{ browser: 'chromium' }]
    }
  },
  resolve: {
    mainFields: ['module'],
  },
  preview: {
    port: 3102,
    strictPort: true
  },
  server: {
    open: false,
    port: 3102,
    strictPort: true,
    // Proxy /api to the Dispatch API (override the target with VITE_API_TARGET).
    proxy: {
      '/api': { target: process.env.VITE_API_TARGET ?? 'http://localhost:5207', changeOrigin: true }
    }
  }
})
