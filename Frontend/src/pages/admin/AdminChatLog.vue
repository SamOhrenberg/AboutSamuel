<template>
  <div>
    <div class="tab-toolbar">
      <v-switch v-model="errorsOnly" label="Errors only" color="error" hide-details density="compact"
        @change="load" />
      <v-spacer />
      <v-btn variant="text" size="small" color="secondary" @click="load">
        <v-icon start>mdi-refresh</v-icon>Refresh
      </v-btn>
    </div>

    <div v-if="loading" class="loading-state">
      <v-progress-circular indeterminate color="secondary" />
    </div>

    <div v-else class="sessions-list">
      <v-expansion-panels variant="accordion" class="sessions-panels">
        <v-expansion-panel v-for="session in sessions" :key="session.sessionTrackingId ?? 'anon'"
          class="session-panel">
          <v-expansion-panel-title class="session-header">
            <div class="session-header__left">
              <v-icon :color="session.hadError ? 'error' : 'secondary'" size="18" class="mr-2">
                {{ session.hadError ? 'mdi-alert-circle' : 'mdi-account-circle' }}
              </v-icon>
              <span class="session-id">
                {{ session.sessionTrackingId ? session.sessionTrackingId.substring(0, 8) + '…' : 'Anonymous' }}
              </span>
              <v-chip v-if="session.hadTokenLimit" size="x-small" color="warning" variant="tonal" class="ml-2">Token Limit</v-chip>
              <v-chip v-if="session.hadError" size="x-small" color="error" variant="tonal" class="ml-2">Error</v-chip>
            </div>
            <div class="session-header__right">
              <span class="session-meta">
                {{ session.messageCount }} msg{{ session.messageCount !== 1 ? 's' : '' }}
                · {{ formatDate(session.firstMessageAt) }}
              </span>
            </div>
          </v-expansion-panel-title>

          <v-expansion-panel-text class="session-messages">
            <div v-for="msg in session.messages" :key="msg.chatId" class="chat-exchange"
              :class="{ 'chat-exchange--error': msg.error }">
              <div class="exchange-meta">
                <span class="exchange-time">{{ formatDateTime(msg.receivedAt) }}</span>
                <span class="exchange-duration">{{ msg.responseTookMs.toFixed(0) }}ms</span>
                <v-chip v-if="msg.error" size="x-small" color="error" variant="tonal">Error</v-chip>
                <v-chip v-if="msg.tokenLimitReached" size="x-small" color="warning" variant="tonal">Token Limit</v-chip>
              </div>
              <div class="exchange-user">
                <span class="exchange-label">User</span>
                <p class="exchange-text">{{ msg.message }}</p>
              </div>
              <div class="exchange-assistant">
                <span class="exchange-label exchange-label--assistant">SamuelLM</span>
                <p class="exchange-text">{{ msg.response }}</p>
              </div>
            </div>
          </v-expansion-panel-text>
        </v-expansion-panel>
      </v-expansion-panels>

      <div class="pagination-bar">
        <v-btn variant="text" size="small" :disabled="page <= 1" @click="page--; load()">
          <v-icon>mdi-chevron-left</v-icon>
        </v-btn>
        <span class="page-label">Page {{ page }}</span>
        <v-btn variant="text" size="small" :disabled="sessions.length < pageSize" @click="page++; load()">
          <v-icon>mdi-chevron-right</v-icon>
        </v-btn>
      </div>
    </div>

    <v-snackbar v-model="snackbar.show" :color="snackbar.color" :timeout="3000" location="bottom right">
      {{ snackbar.text }}
    </v-snackbar>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useAdminStore } from '@/stores/adminStore'

const adminStore = useAdminStore()

const sessions = ref([])
const loading = ref(false)
const errorsOnly = ref(false)
const page = ref(1)
const pageSize = 20
const snackbar = ref({ show: false, text: '', color: 'success' })

function notify(text, color = 'success') { snackbar.value = { show: true, text, color } }

async function load() {
  loading.value = true
  try {
    const params = new URLSearchParams({ page: page.value, pageSize, errorsOnly: errorsOnly.value })
    const res = await adminStore.apiFetch(`/admin/chats?${params}`)
    if (res.ok) sessions.value = await res.json()
  } catch { notify('Failed to load chat logs', 'error') }
  finally { loading.value = false }
}

function formatDate(iso) {
  return new Date(iso).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })
}

function formatDateTime(iso) {
  return new Date(iso).toLocaleString('en-US', { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit', hour12: true })
}

defineExpose({ load })
onMounted(load)
</script>

<style scoped>
.tab-toolbar { display: flex; align-items: center; gap: 0.75rem; margin-bottom: 1.25rem; flex-wrap: wrap; }
.loading-state { display: flex; justify-content: center; padding: 4rem; }

.sessions-panels { border-radius: 12px !important; overflow: hidden; }
.session-panel { background: rgb(var(--v-theme-surface)) !important; border-bottom: 1px solid rgba(255,255,255,0.06) !important; }
.session-header { font-family: 'Raleway', sans-serif; font-size: 0.875rem; }
.session-header__left { display: flex; align-items: center; gap: 4px; }
.session-header__right { margin-left: auto; padding-right: 1rem; }
.session-id { font-family: monospace; font-size: 0.8rem; color: rgba(var(--v-theme-on-surface), 0.7); }
.session-meta { font-size: 0.75rem; color: rgba(var(--v-theme-on-surface), 0.45); }
.session-messages { padding: 0 !important; }

.chat-exchange { padding: 1rem 1.25rem; border-top: 1px solid rgba(255,255,255,0.05); }
.chat-exchange--error { border-left: 3px solid rgb(var(--v-theme-error)); background: rgba(var(--v-theme-error), 0.04); }

.exchange-meta { display: flex; align-items: center; gap: 0.5rem; margin-bottom: 0.5rem; }
.exchange-time { font-size: 0.72rem; color: rgba(var(--v-theme-on-surface), 0.4); }
.exchange-duration { font-size: 0.72rem; color: rgba(var(--v-theme-secondary), 0.6); font-family: monospace; }

.exchange-label { font-size: 0.7rem; font-weight: 700; text-transform: uppercase; letter-spacing: 0.06em; color: rgba(var(--v-theme-on-surface), 0.4); display: block; margin-bottom: 0.2rem; }
.exchange-label--assistant { color: rgb(var(--v-theme-secondary)); }
.exchange-user, .exchange-assistant { margin-bottom: 0.6rem; }
.exchange-text { font-size: 0.875rem; line-height: 1.6; color: rgba(var(--v-theme-on-surface), 0.85); margin: 0; white-space: pre-wrap; }

.pagination-bar { display: flex; align-items: center; justify-content: center; gap: 1rem; margin-top: 1rem; }
.page-label { font-size: 0.85rem; color: rgba(var(--v-theme-on-surface), 0.5); }
</style>