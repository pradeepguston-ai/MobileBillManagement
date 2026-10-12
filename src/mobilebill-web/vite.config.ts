import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

import packageJson from './package.json' with { type: 'json' }

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  // The version shown in the sidebar footer comes from package.json.
  define: { __APP_VERSION__: JSON.stringify(packageJson.version) },
})
