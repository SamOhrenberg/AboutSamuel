<template>
  <div class="admin-verify-page">
    <div class="verify-card">

      <div v-if="state === 'loading'" class="verify-state">
        <v-progress-circular indeterminate color="secondary" size="56" />
        <p class="verify-text">Verifying your login link...</p>
      </div>

      <div v-else-if="state === 'success'" class="verify-state">
        <v-icon size="56" color="success">mdi-check-circle</v-icon>
        <p class="verify-text">Logged in successfully! Redirecting...</p>
      </div>

      <div v-else class="verify-state">
        <v-icon size="56" color="error">mdi-alert-circle</v-icon>
        <h2 class="verify-error-title">Link Invalid or Expired</h2>
        <p class="verify-text">{{ errorMessage }}</p>
        <v-btn color="secondary" variant="tonal" class="mt-4" to="/admin/login">
          Request a New Link
        </v-btn>
      </div>

    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAdminStore } from '@/stores/adminStore'

const adminStore   = useAdminStore()
const route        = useRoute()
const router       = useRouter()
const state        = ref('loading')
const errorMessage = ref('This link is invalid or has already been used. Please request a new one.')

onMounted(async () => {
  const token = route.query.token

  // No token in URL — user navigated here directly, send them to login
  if (!token || typeof token !== 'string' || !token.trim()) {
    window.location.replace('/admin/login')
    return
  }

  const ok = await adminStore.verifyToken(token)

  if (ok) {
    state.value = 'success'
    router.replace("/admin")
  } else {
    state.value = 'error'
    errorMessage.value = adminStore.error ?? errorMessage.value
  }
})
</script>

<style scoped>
.admin-verify-page {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgb(var(--v-theme-background));
  padding: 2rem;
}

.verify-card {
  background: rgb(var(--v-theme-surface));
  border: 1px solid rgba(var(--v-theme-secondary), 0.2);
  border-radius: 16px;
  padding: 3rem 2.5rem;
  width: 100%;
  max-width: 400px;
  box-shadow: 0 8px 40px rgba(0, 0, 0, 0.3);
}

.verify-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  gap: 1rem;
}

.verify-text {
  font-family: 'Raleway', sans-serif;
  font-size: 0.95rem;
  color: rgba(var(--v-theme-on-surface), 0.7);
  margin: 0;
  line-height: 1.6;
}

.verify-error-title {
  font-family: 'Patua One', serif;
  font-size: 1.3rem;
  color: rgb(var(--v-theme-error));
  margin: 0;
}
</style>