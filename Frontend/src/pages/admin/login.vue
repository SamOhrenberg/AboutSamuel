<template>
  <div class="admin-login-page">
    <div class="login-card">
      <div class="login-card__header">
        <v-icon size="40" color="secondary" class="mb-3">mdi-shield-account</v-icon>
        <h1 class="login-title">Admin Panel</h1>
        <p class="login-subtitle">AboutSamuel.com</p>
      </div>

      <div v-if="!sent" class="login-card__body">
        <p class="login-description">
          Click below to receive a secure magic link at your configured admin email address.
        </p>
        <v-btn
          color="secondary"
          size="large"
          block
          :loading="adminStore.loading"
          @click="handleRequest"
          class="send-btn"
        >
          <v-icon start>mdi-email-fast</v-icon>
          Send Magic Link
        </v-btn>
        <v-alert
          v-if="adminStore.error"
          type="error"
          variant="tonal"
          class="mt-4"
          density="compact"
        >
          {{ adminStore.error }}
        </v-alert>
      </div>

      <div v-else class="login-card__body login-card__body--sent">
        <v-icon size="56" color="success" class="mb-4">mdi-email-check</v-icon>
        <h2 class="sent-title">Check your email</h2>
        <p class="sent-description">
          A magic link has been sent to your admin email address.
          It expires in <strong>15 minutes</strong> and can only be used once.
        </p>
        <v-btn
          variant="text"
          color="secondary"
          size="small"
          class="mt-4"
          @click="sent = false"
        >
          Send another link
        </v-btn>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import { useAdminStore } from '@/stores/adminStore'

const adminStore = useAdminStore()
const sent = ref(false)

async function handleRequest() {
  const ok = await adminStore.requestMagicLink()
  if (ok) sent.value = true
}
</script>

<style scoped>
.admin-login-page {
  min-height: 100dvh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgb(var(--v-theme-background));
  padding: 2rem;
}

.login-card {
  background: rgb(var(--v-theme-surface));
  border: 1px solid rgba(var(--v-theme-secondary), 0.2);
  border-radius: 16px;
  padding: 3rem 2.5rem;
  width: 100%;
  max-width: 420px;
  box-shadow: 0 8px 40px rgba(0, 0, 0, 0.3);
}

.login-card__header {
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  margin-bottom: 2rem;
}

.login-title {
  font-family: 'Patua One', serif;
  font-size: 1.8rem;
  margin: 0 0 0.25rem;
  color: rgb(var(--v-theme-on-surface));
}

.login-subtitle {
  font-family: 'Raleway', sans-serif;
  font-size: 0.85rem;
  color: rgba(var(--v-theme-on-surface), 0.5);
  margin: 0;
}

.login-card__body {
  display: flex;
  flex-direction: column;
}

.login-card__body--sent {
  align-items: center;
  text-align: center;
}

.login-description {
  font-family: 'Raleway', sans-serif;
  font-size: 0.9rem;
  color: rgba(var(--v-theme-on-surface), 0.7);
  line-height: 1.6;
  margin: 0 0 1.5rem;
  text-align: center;
}

.send-btn { font-weight: 600; letter-spacing: 0.03em; }

.sent-title {
  font-family: 'Patua One', serif;
  font-size: 1.4rem;
  margin: 0 0 0.75rem;
  color: rgb(var(--v-theme-on-surface));
}

.sent-description {
  font-family: 'Raleway', sans-serif;
  font-size: 0.9rem;
  color: rgba(var(--v-theme-on-surface), 0.7);
  line-height: 1.6;
  margin: 0;
}
</style>