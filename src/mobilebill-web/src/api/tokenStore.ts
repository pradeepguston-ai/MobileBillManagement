const STORAGE_KEY = 'mobilebill.token'
let unauthorizedHandler: (() => void) | undefined

export function getToken(): string | null {
  try { return localStorage.getItem(STORAGE_KEY) } catch { return null }
}

export function setToken(token: string) {
  try { localStorage.setItem(STORAGE_KEY, token) } catch { /* storage unavailable */ }
}

export function clearToken() {
  try { localStorage.removeItem(STORAGE_KEY) } catch { /* storage unavailable */ }
}

export function onUnauthorized(handler: () => void) {
  unauthorizedHandler = handler
}

export function notifyUnauthorized() {
  unauthorizedHandler?.()
}
