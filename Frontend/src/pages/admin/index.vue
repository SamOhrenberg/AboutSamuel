<template>
  <div class="admin-page">

    <!-- ── Header ── -->
    <div class="admin-header">
      <div class="admin-header__left">
        <v-icon color="secondary" size="28" class="mr-2">mdi-shield-account</v-icon>
        <span class="admin-title">Admin Panel</span>
      </div>
      <div class="d-flex align-center" style="gap:0.5rem">
        <v-btn variant="tonal" color="secondary" size="small" :loading="regenerating" @click="regenerateVisualization">
          <v-icon start>mdi-scatter-plot</v-icon>Regenerate Skill Map
        </v-btn>
        <v-btn variant="tonal" color="secondary" size="small" :loading="embeddingLoading"
          @click="generateAllEmbeddings">
          <v-icon start>mdi-brain</v-icon>Generate Embeddings
        </v-btn>
        <v-btn variant="text" color="error" size="small" @click="handleLogout">
          <v-icon start>mdi-logout</v-icon>Logout
        </v-btn>
      </div>
    </div>

    <!-- ── Stats bar ── -->
    <div class="stats-bar" v-if="stats">
      <div class="stat-chip" v-for="s in statItems" :key="s.label">
        <span class="stat-chip__value">{{ s.value }}</span>
        <span class="stat-chip__label">{{ s.label }}</span>
      </div>
    </div>

    <!-- ── Tabs ── -->
    <v-tabs v-model="tab" color="secondary" class="admin-tabs">
      <v-tab value="work-experience"><v-icon start>mdi-briefcase-clock</v-icon>Work Experience</v-tab>
      <v-tab value="projects"><v-icon start>mdi-briefcase</v-icon>Projects</v-tab>
      <v-tab value="information"><v-icon start>mdi-database</v-icon>Information</v-tab>
      <v-tab value="chats"><v-icon start>mdi-chat</v-icon>Chat Logs</v-tab>
    </v-tabs>

    <v-divider />

    <!-- ── Tab content ── -->
    <div class="admin-tab-content">
      <AdminWorkExperience v-if="tab === 'work-experience'" ref="workRef" />
      <AdminProjects       v-if="tab === 'projects'"        ref="projectRef" />
      <AdminInformation    v-if="tab === 'information'"     ref="infoRef" />
      <AdminChatLogs       v-if="tab === 'chats'"           ref="chatRef" />
    </div>

    <v-snackbar v-model="snackbar.show" :color="snackbar.color" :timeout="3000" location="bottom right">
      {{ snackbar.text }}
    </v-snackbar>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAdminStore } from '@/stores/adminStore'
import AdminWorkExperience from './AdminWorkExperience.vue'
import AdminProjects from './AdminProjects.vue'
import AdminInformation from './AdminInformation.vue'
import AdminChatLogs from './AdminChatLog.vue'

const router = useRouter()
const adminStore = useAdminStore()

const tab = ref('work-experience')
const stats = ref(null)
const embeddingLoading = ref(false)
const snackbar = ref({ show: false, text: '', color: 'success' })
const regenerating = ref(false);

// Child component refs — used to trigger reloads after global embedding generation
const workRef = ref(null)
const projectRef = ref(null)
const infoRef = ref(null)
const chatRef = ref(null)

const statItems = computed(() => stats.value ? [
  { label: 'Total Messages', value: stats.value.totalMessages },
  { label: 'Sessions',       value: stats.value.totalSessions },
  { label: 'Errors',         value: stats.value.errorCount },
  { label: 'Today',          value: stats.value.messagesToday },
  { label: 'Avg Response',   value: stats.value.avgResponseMs + 'ms' },
] : [])

function notify(text, color = 'success') { snackbar.value = { show: true, text, color } }

async function loadStats() {
  try {
    const res = await adminStore.apiFetch('/admin/chats/stats')
    if (res.ok) stats.value = await res.json()
  } catch { /* non-critical */ }
}

async function generateAllEmbeddings() {
  embeddingLoading.value = true
  try {
    const res = await adminStore.apiFetch('/admin/generate-embeddings', { method: 'POST' })
    if (!res.ok) throw new Error()
    const data = await res.json()
    notify(`Embeddings generated: ${data.success} succeeded, ${data.failed} failed`)
    // Reload all mounted tab components
    workRef.value?.load()
    projectRef.value?.load()
    infoRef.value?.load()
  } catch { notify('Failed to generate embeddings', 'error') }
  finally { embeddingLoading.value = false }
}

function handleLogout() {
  adminStore.logout()
  router.replace('/admin/login')
}

async function regenerateVisualization() {
  regenerating.value = true;
  try {
    const res = await adminStore.apiFetch(`/admin/embedding-visualization/regenerate`, {
      method: 'POST'
    });
    const data = await res.json();
    // show success — data.message will say how many points were projected
    console.log(data.message);
  } catch (e) {
    console.error('Regeneration failed', e);
  } finally {
    regenerating.value = false;
  }
}

onMounted(loadStats)
</script>

<style scoped>
.admin-page {
  font-family: 'Raleway', sans-serif;
  min-height: 100%;
  background: rgb(var(--v-theme-background));
}

/* ── Header ── */
.admin-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 1rem 1.5rem;
  background: rgb(var(--v-theme-surface));
  border-bottom: 1px solid rgba(var(--v-theme-secondary), 0.15);
}
.admin-header__left { display: flex; align-items: center; }
.admin-title { font-family: 'Patua One', serif; font-size: 1.3rem; color: rgb(var(--v-theme-on-surface)); }

/* ── Stats bar ── */
.stats-bar {
  display: flex;
  gap: 0;
  background: rgb(var(--v-theme-surface));
  border-bottom: 1px solid rgba(var(--v-theme-secondary), 0.1);
  overflow-x: auto;
}
.stat-chip { display: flex; flex-direction: column; align-items: center; padding: 0.6rem 1.5rem; border-right: 1px solid rgba(var(--v-theme-secondary), 0.1); min-width: 100px; }
.stat-chip__value { font-size: 1.3rem; font-weight: 700; color: rgb(var(--v-theme-secondary)); line-height: 1; }
.stat-chip__label { font-size: 0.7rem; color: rgba(var(--v-theme-on-surface), 0.5); text-transform: uppercase; letter-spacing: 0.05em; margin-top: 0.2rem; }

/* ── Tabs ── */
.admin-tabs { background: rgb(var(--v-theme-surface)); }

/* ── Tab content ── */
.admin-tab-content { padding: 1.5rem; }
</style>