import { useAdminStore } from '@/stores/adminStore'
import { useRouter } from 'vue-router'

/**
 * Call at the top of any admin page component.
 * Redirects to /admin/login if the session has expired or is missing.
 */
export function useAdminGuard() {
  const adminStore = useAdminStore()
  const router     = useRouter()

  if (!adminStore.isAuthenticated) {
    router.replace('/admin/login')
  }

  return { adminStore }
}