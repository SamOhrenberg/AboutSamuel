import { defineStore } from 'pinia'
import { ref, computed } from 'vue'

const API_BASE = import.meta.env.VITE_API_URL

export const useAdminStore = defineStore('admin', () => {
  // ── State ──────────────────────────────────────────────────────────────
  const jwt         = ref(sessionStorage.getItem('admin_jwt') ?? null)
  const expiresAt   = ref(sessionStorage.getItem('admin_expires') ?? null)
  const loading     = ref(false)
  const error       = ref(null)

  // ── Computed ───────────────────────────────────────────────────────────
  const isAuthenticated = computed(() => {
    if (!jwt.value || !expiresAt.value) return false
    return new Date(expiresAt.value) > new Date()
  })

  const authHeaders = computed(() => ({
    'Content-Type': 'application/json',
    'Authorization': `Bearer ${jwt.value}`
  }))

  // ── Actions ────────────────────────────────────────────────────────────

  async function requestMagicLink() {
    loading.value = true
    error.value   = null
    try {
      const res = await fetch(`${API_BASE}/admin/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' }
      })
      // Always returns 200 by design (oracle attack prevention)
      return true
    } catch (e) {
      error.value = 'Could not reach the server. Please try again.'
      return false
    } finally {
      loading.value = false
    }
  }

  /** exchange the token from the magic link URL for a JWT */
  async function verifyToken(token) {
    loading.value = true
    error.value   = null
    try {
      const res = await fetch(`${API_BASE}/admin/verify`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ token })
      })

      if (!res.ok) {
        const body = await res.json().catch(() => ({}))
        error.value = body.message ?? 'This link is invalid or has expired.'
        return false
      }

      const data = await res.json()
      jwt.value       = data.jwt
      expiresAt.value = data.expiresAt

      sessionStorage.setItem('admin_jwt',     data.jwt)
      sessionStorage.setItem('admin_expires', data.expiresAt)

      return true
    } catch (e) {
      error.value = 'Verification failed. Please try again.'
      return false
    } finally {
      loading.value = false
    }
  }

  function logout() {
    jwt.value       = null
    expiresAt.value = null
    sessionStorage.removeItem('admin_jwt')
    sessionStorage.removeItem('admin_expires')
  }

  // ── Authenticated fetch helper ─────────────────────────────────────────
  // Wraps fetch with auth header + auto-logout on 401
  async function apiFetch(path, options = {}) {
    if (!isAuthenticated.value) {
      logout()
      throw new Error('Not authenticated')
    }

    const res = await fetch(`${API_BASE}${path}`, {
      ...options,
      headers: {
        ...authHeaders.value,
        ...(options.headers ?? {})
      }
    })

    if (res.status === 401) {
      logout()
      throw new Error('Session expired. Please log in again.')
    }

    return res
  }

  return {
    jwt, expiresAt, loading, error,
    isAuthenticated,
    requestMagicLink, verifyToken, logout, apiFetch
  }
})