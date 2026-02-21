<template>
  <div class="admin-page">

    <div class="admin-header">
      <div class="admin-header__left">
        <v-icon color="secondary" size="28" class="mr-2">mdi-shield-account</v-icon>
        <span class="admin-title">Admin Panel</span>
      </div>
      <v-btn variant="text" color="error" size="small" @click="handleLogout">
        <v-icon start>mdi-logout</v-icon>
        Logout
      </v-btn>
    </div>

    <div class="stats-bar" v-if="stats">
      <div class="stat-chip" v-for="s in statItems" :key="s.label">
        <span class="stat-chip__value">{{ s.value }}</span>
        <span class="stat-chip__label">{{ s.label }}</span>
      </div>
    </div>

    <v-tabs v-model="tab" color="secondary" class="admin-tabs">
      <v-tab value="work-experience">
        <v-icon start>mdi-briefcase-clock</v-icon>Work Experience
      </v-tab>
      <v-tab value="projects">
        <v-icon start>mdi-briefcase</v-icon>Projects
      </v-tab>
      <v-tab value="information">
        <v-icon start>mdi-database</v-icon>Information
      </v-tab>
      <v-tab value="chats">
        <v-icon start>mdi-chat</v-icon>Chat Logs
      </v-tab>
    </v-tabs>

    <v-divider />

    <div class="admin-tab-content">

      <div v-if="tab === 'work-experience'">
        <div class="tab-toolbar">
          <v-btn color="secondary" variant="tonal" @click="openWorkDialog()">
            <v-icon start>mdi-plus</v-icon>Add Work
          </v-btn>
        </div>

        <v-progress-linear v-if="worksLoading" indeterminate color="secondary" class="mb-4" />

        <div v-else-if="works.length" class="works-list">
          <div v-for="work in works" :key="work.workExperienceId" class="work-row"
            :class="{ 'work-row--inactive': !work.isActive }">
            <div class="work-row__info">
              <span class="work-row__title">{{ work.title }}</span>
              <span class="work-row__employer">{{ work.employer }}</span>
              <span class="work-row__years">
                {{ work.startYear }}{{ work.endYear ? '–' + work.endYear : '–Present' }}
              </span>
              <v-chip v-if="!work.isActive" size="x-small" color="warning" variant="tonal">Inactive</v-chip>
            </div>
            <div class="work-row__meta">
              <span class="work-row__achievements">
                {{ work.achievements?.length || 0 }} achievement{{ work.achievements?.length !== 1 ? 's' : '' }}
              </span>
              <v-btn icon size="x-small" variant="text" @click="openWorkDialog(work)">
                <v-icon>mdi-pencil</v-icon>
              </v-btn>
              <v-btn icon size="x-small" variant="text" color="error" @click="confirmDeleteWork(work)">
                <v-icon>mdi-delete</v-icon>
              </v-btn>
            </div>
          </div>
        </div>

        <p v-else class="admin-section__empty">No work experience entries yet.</p>
      </div>

      <div v-if="tab === 'projects'">
        <div class="tab-toolbar">
          <v-btn color="secondary" variant="tonal" @click="openProjectDialog()">
            <v-icon start>mdi-plus</v-icon>New Project
          </v-btn>
          <v-text-field v-model="projectSearch" placeholder="Search projects..." prepend-inner-icon="mdi-magnify"
            variant="outlined" density="compact" hide-details class="toolbar-search" clearable />
          <v-chip-group v-model="projectFilter" class="ml-2">
            <v-chip value="all" size="small" filter>All</v-chip>
            <v-chip value="active" size="small" filter>Active</v-chip>
            <v-chip value="inactive" size="small" filter>Inactive</v-chip>
            <v-chip value="featured" size="small" filter>Featured</v-chip>
          </v-chip-group>
        </div>

        <div v-if="projectsLoading" class="loading-state">
          <v-progress-circular indeterminate color="secondary" />
        </div>

        <v-data-table v-else :headers="projectHeaders" :items="filteredProjects" :search="projectSearch"
          item-value="projectId" class="admin-table" hover>
          <template #item.isActive="{ item }">
            <v-chip :color="item.isActive ? 'success' : 'error'" size="x-small" variant="tonal">
              {{ item.isActive ? 'Active' : 'Inactive' }}
            </v-chip>
          </template>

          <template #item.isFeatured="{ item }">
            <v-icon v-if="item.isFeatured" color="yellow" size="18">mdi-star</v-icon>
            <v-icon v-else color="surface-variant" size="18">mdi-star-outline</v-icon>
          </template>

          <template #item.techStack="{ item }">
            <div class="chip-wrap">
              <v-chip v-for="t in item.techStack.slice(0, 3)" :key="t" size="x-small" variant="tonal" color="secondary"
                class="mr-1">
                {{ t }}
              </v-chip>
              <span v-if="item.techStack.length > 3" class="text-caption text-medium-emphasis">
                +{{ item.techStack.length - 3 }}
              </span>
            </div>
          </template>

          <template #item.workExperienceId="{ item }">
            <span class="text-body-2">
              {{works.find(w => w.workExperienceId === item.workExperienceId)?.employer ?? '—'}}
            </span>
          </template>

          <template #item.actions="{ item }">
            <div class="row-actions">
              <v-btn icon size="x-small" variant="text" @click="openProjectDialog(item)" title="Edit">
                <v-icon>mdi-pencil</v-icon>
              </v-btn>
              <v-btn icon size="x-small" variant="text" :color="item.isActive ? 'error' : 'success'"
                @click="toggleProjectActive(item)" :title="item.isActive ? 'Deactivate' : 'Restore'">
                <v-icon>{{ item.isActive ? 'mdi-eye-off' : 'mdi-eye' }}</v-icon>
              </v-btn>
            </div>
          </template>
        </v-data-table>
      </div>

      <div v-if="tab === 'information'">
        <div class="tab-toolbar">
          <v-btn color="secondary" variant="tonal" @click="openInfoDialog()">
            <v-icon start>mdi-plus</v-icon>New Entry
          </v-btn>
          <v-text-field v-model="infoSearch" placeholder="Search information..." prepend-inner-icon="mdi-magnify"
            variant="outlined" density="compact" hide-details class="toolbar-search" clearable />
        </div>

        <div v-if="infoLoading" class="loading-state">
          <v-progress-circular indeterminate color="secondary" />
        </div>

        <div v-else class="info-grid">
          <v-card v-for="item in filteredInfo" :key="item.informationId" class="info-card" elevation="0">
            <div class="info-card__body">
              <p class="info-card__text">{{ item.text || '(no text)' }}</p>
              <div class="info-card__keywords">
                <v-chip v-for="kw in item.keywords" :key="kw" size="x-small" variant="tonal" color="secondary" closable
                  @click:close="deleteKeyword(item, kw)" class="mr-1 mb-1">
                  {{ kw }}
                </v-chip>
                <v-chip size="x-small" variant="outlined" color="secondary" class="mr-1 mb-1 add-keyword-chip"
                  @click="openAddKeyword(item)">
                  <v-icon start size="12">mdi-plus</v-icon>Add
                </v-chip>
              </div>
            </div>
            <v-divider />
            <div class="info-card__actions">
              <v-btn size="x-small" variant="text" color="secondary" @click="openInfoDialog(item)">
                <v-icon start size="14">mdi-pencil</v-icon>Edit
              </v-btn>
              <v-btn size="x-small" variant="text" color="error" @click="deleteInfo(item)">
                <v-icon start size="14">mdi-delete</v-icon>Delete
              </v-btn>
            </div>
          </v-card>
        </div>
      </div>

      <div v-if="tab === 'chats'">
        <div class="tab-toolbar">
          <v-switch v-model="errorsOnly" label="Errors only" color="error" hide-details density="compact"
            @change="loadChats" />
          <v-spacer />
          <v-btn variant="text" size="small" color="secondary" @click="loadChats">
            <v-icon start>mdi-refresh</v-icon>Refresh
          </v-btn>
        </div>

        <div v-if="chatsLoading" class="loading-state">
          <v-progress-circular indeterminate color="secondary" />
        </div>

        <div v-else class="sessions-list">
          <v-expansion-panels variant="accordion" class="sessions-panels">
            <v-expansion-panel v-for="session in chatSessions" :key="session.sessionTrackingId ?? 'anon'"
              class="session-panel">
              <v-expansion-panel-title class="session-header">
                <div class="session-header__left">
                  <v-icon :color="session.hadError ? 'error' : 'secondary'" size="18" class="mr-2">
                    {{ session.hadError ? 'mdi-alert-circle' : 'mdi-account-circle' }}
                  </v-icon>
                  <span class="session-id">
                    {{ session.sessionTrackingId
                      ? session.sessionTrackingId.substring(0, 8) + '…'
                      : 'Anonymous' }}
                  </span>
                  <v-chip v-if="session.hadTokenLimit" size="x-small" color="warning" variant="tonal" class="ml-2">
                    Token Limit
                  </v-chip>
                  <v-chip v-if="session.hadError" size="x-small" color="error" variant="tonal" class="ml-2">
                    Error
                  </v-chip>
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
                    <v-chip v-if="msg.tokenLimitReached" size="x-small" color="warning" variant="tonal">Token
                      Limit</v-chip>
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
            <v-btn variant="text" size="small" :disabled="chatPage <= 1" @click="chatPage--; loadChats()">
              <v-icon>mdi-chevron-left</v-icon>
            </v-btn>
            <span class="page-label">Page {{ chatPage }}</span>
            <v-btn variant="text" size="small" :disabled="chatSessions.length < chatPageSize"
              @click="chatPage++; loadChats()">
              <v-icon>mdi-chevron-right</v-icon>
            </v-btn>
          </div>
        </div>
      </div>
    </div>

    <v-dialog v-model="workDialog" max-width="720" persistent scrollable>
      <v-card class="dialog-card">
        <v-card-title class="dialog-title">
          {{ editingWorkId ? 'Edit Work Experience' : 'Add Work Experience' }}
          <v-btn icon variant="text" size="small" @click="workDialog = false" class="ml-auto">
            <v-icon>mdi-close</v-icon>
          </v-btn>
        </v-card-title>
        <v-divider />
        <v-card-text class="dialog-body">
          <v-row dense>
            <v-col cols="12" sm="6">
              <v-text-field v-model="wf.employer" label="Employer" variant="outlined" density="comfortable"
                color="secondary" />
            </v-col>
            <v-col cols="12" sm="6">
              <v-text-field v-model="wf.title" label="Work Title" variant="outlined" density="comfortable"
                color="secondary" />
            </v-col>
            <v-col cols="6" sm="3">
              <v-text-field v-model="wf.startYear" label="Start Year" variant="outlined" density="comfortable"
                color="secondary" placeholder="2019" />
            </v-col>
            <v-col cols="6" sm="3">
              <v-text-field v-model="wf.endYear" label="End Year" variant="outlined" density="comfortable"
                color="secondary" placeholder="Present" />
            </v-col>
            <v-col cols="12" sm="6">
              <v-switch v-model="wf.isActive" label="Active" color="secondary" density="comfortable" hide-details />
            </v-col>
            <v-col cols="12">
              <v-textarea v-model="wf.summary" label="Summary (optional)" variant="outlined" density="comfortable"
                color="secondary" rows="2" auto-grow />
            </v-col>
            <v-col cols="12">
              <div class="achievements-label">
                Achievements
                <v-btn size="x-small" variant="text" color="secondary" @click="addAchievement">+ Add</v-btn>
              </div>
              <div v-for="(achievement, i) in wf.achievements" :key="i" class="achievement-row">
                <v-text-field v-model="wf.achievements[i]" :label="`Achievement ${i + 1}`" variant="outlined"
                  density="compact" color="secondary" hide-details class="achievement-field" />
                <v-btn icon size="x-small" variant="text" color="error" @click="removeAchievement(i)">
                  <v-icon>mdi-close</v-icon>
                </v-btn>
              </div>
              <p v-if="!wf.achievements.length" class="achievements-empty">No achievements added yet.</p>
            </v-col>
            <v-col cols="12" sm="4">
              <v-text-field v-model.number="wf.displayOrder" label="Display Order" type="number" variant="outlined"
                density="comfortable" color="secondary" hide-details />
            </v-col>
          </v-row>
        </v-card-text>
        <v-divider />
        <v-card-actions class="dialog-actions">
          <v-spacer />
          <v-btn variant="text" @click="workDialog = false">Cancel</v-btn>
          <v-btn color="secondary" variant="tonal" :loading="worksSaving" @click="saveWork">
            {{ editingWorkId ? 'Save Changes' : 'Create' }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="workDeleteDialog" max-width="400">
      <v-card>
        <v-card-title>Delete Work Experience</v-card-title>
        <v-card-text>
          Are you sure you want to delete <strong>{{ deletingWorkItem?.title }}</strong>?
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="workDeleteDialog = false">Cancel</v-btn>
          <v-btn color="error" variant="flat" :loading="worksSaving" @click="deleteWork">Delete</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="projectDialog" max-width="900" scrollable>
      <v-card class="dialog-card">
        <v-card-title class="dialog-title">
          {{ editingProject ? 'Edit Project' : 'New Project' }}
          <v-btn icon variant="text" size="small" @click="projectDialog = false" class="ml-auto">
            <v-icon>mdi-close</v-icon>
          </v-btn>
        </v-card-title>
        <v-divider />
        <v-card-text class="dialog-body">
          <v-row>
            <v-col cols="12">
              <v-text-field v-model="pf.title" label="Title *" variant="outlined" density="comfortable"
                color="secondary" />
            </v-col>
            <v-col cols="12" md="6">
              <v-autocomplete v-model="pf.workExperienceId" :items="workExperienceOptions" item-title="title"
                item-value="value" label="Work Experience (optional)" variant="outlined" density="comfortable"
                color="secondary" clearable no-data-text="No work experiences found"
                aria-label="Link this project to a work experience entry">
                <!-- Richer item slot so the user can see the year range -->
                <template #item="{ props, item }">
                  <v-list-item v-bind="props" :subtitle="item.raw.subtitle" />
                </template>

                <!-- Show "Unassigned" when nothing is selected -->
                <template #prepend-inner>
                  <v-icon :color="pf.workExperienceId ? 'secondary' : 'surface-variant'" size="18" class="mr-1">
                    mdi-briefcase-outline
                  </v-icon>
                </template>
              </v-autocomplete>
            </v-col>

            <v-col cols="12" md="6">
              <v-text-field v-model="pf.role" label="Role *" variant="outlined" density="comfortable"
                color="secondary" />
            </v-col>
            <v-col cols="6" md="3">
              <v-text-field v-model="pf.startYear" label="Start Year" variant="outlined" density="comfortable"
                color="secondary" />
            </v-col>
            <v-col cols="6" md="3">
              <v-text-field v-model="pf.endYear" label="End Year" variant="outlined" density="comfortable"
                color="secondary" placeholder="Present" />
            </v-col>
            <v-col cols="6" md="3">
              <v-text-field v-model.number="pf.displayOrder" label="Display Order" type="number" variant="outlined"
                density="comfortable" color="secondary" />
            </v-col>
            <v-col cols="6" md="3" class="d-flex align-center justify-center"
              style="gap: 2rem; padding-bottom: 0.5rem;">
              <div class="toggle-group">
                <v-switch v-model="pf.isFeatured" color="secondary" density="compact" hide-details inset />
                <span class="toggle-label">Featured</span>
              </div>
              <div class="toggle-group">
                <v-switch v-model="pf.isActive" color="success" density="compact" hide-details inset />
                <span class="toggle-label">Active</span>
              </div>
            </v-col>
            <v-col cols="12">
              <v-textarea v-model="pf.summary" label="Summary *" variant="outlined" density="comfortable" rows="4"
                color="secondary" auto-grow />
            </v-col>
            <v-col cols="12">
              <v-textarea v-model="pf.detail" label="Detail (optional)" variant="outlined" density="comfortable"
                rows="4" color="secondary" auto-grow />
            </v-col>
            <v-col cols="12">
              <v-textarea v-model="pf.impactStatement" label="Impact Statement (optional)" variant="outlined"
                density="comfortable" rows="2" color="secondary" auto-grow
                hint="A single punchy sentence quantifying the outcome, e.g. 'Reduced deployment time by 60% and eliminated manual hotfixes'"
                persistent-hint />
            </v-col>
            <v-col cols="12">
              <label class="field-label">Tech Stack</label>
              <p class="field-hint mb-2">Press Enter or comma after each item to add it.</p>
              <div class="tag-input-wrap">
                <v-chip v-for="(t, i) in pf.techStack" :key="t" size="small" closable class="mr-1 mb-1"
                  @click:close="pf.techStack.splice(i, 1)">{{ t }}</v-chip>
                <input v-model="techInput" class="tag-input" placeholder="Add tech, press Enter"
                  @keydown.enter.prevent="addTech" @keydown.comma.prevent="addTech" />
              </div>
            </v-col>
          </v-row>
        </v-card-text>
        <v-divider />
        <v-card-actions class="dialog-actions">
          <v-spacer />
          <v-btn variant="text" @click="projectDialog = false">Cancel</v-btn>
          <v-btn color="secondary" variant="tonal" :loading="saving" @click="saveProject">
            {{ editingProject ? 'Save Changes' : 'Create Project' }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="infoDialog" max-width="600" scrollable>
      <v-card class="dialog-card">
        <v-card-title class="dialog-title">
          {{ editingInfo ? 'Edit Information' : 'New Information Entry' }}
          <v-btn icon variant="text" size="small" @click="infoDialog = false" class="ml-auto">
            <v-icon>mdi-close</v-icon>
          </v-btn>
        </v-card-title>
        <v-divider />
        <v-card-text class="dialog-body">
          <p class="field-hint mb-3">This text is used by SamuelLM's RAG pipeline to answer questions about Samuel.</p>
          <v-textarea v-model="inf.text" label="Information Text" variant="outlined" rows="8" color="secondary"
            auto-grow />
          <label class="field-label mt-4 d-block">Keywords</label>
          <p class="field-hint">Keywords are used for fuzzy matching. They're generated automatically from the text but
            you
            can add or remove them here.</p>
          <div class="tag-input-wrap mt-2">
            <v-chip v-for="(kw, i) in inf.keywords" :key="kw" size="small" closable class="mr-1 mb-1"
              @click:close="inf.keywords.splice(i, 1)">{{ kw }}</v-chip>
            <input v-model="kwInput" class="tag-input" placeholder="Add keyword, press Enter"
              @keydown.enter.prevent="addKeyword" @keydown.comma.prevent="addKeyword" />
          </div>
        </v-card-text>
        <v-divider />
        <v-card-actions class="dialog-actions">
          <v-spacer />
          <v-btn variant="text" @click="infoDialog = false">Cancel</v-btn>
          <v-btn color="secondary" variant="tonal" :loading="saving" @click="saveInfo">
            {{ editingInfo ? 'Save Changes' : 'Create Entry' }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="addKeywordDialog" max-width="400">
      <v-card class="dialog-card">
        <v-card-title class="dialog-title">Add Keyword</v-card-title>
        <v-card-text>
          <v-text-field v-model="quickKw" label="Keyword" variant="outlined" density="compact" color="secondary"
            autofocus @keydown.enter="saveQuickKeyword" />
        </v-card-text>
        <v-card-actions class="dialog-actions">
          <v-spacer />
          <v-btn variant="text" @click="addKeywordDialog = false">Cancel</v-btn>
          <v-btn color="secondary" variant="tonal" :loading="saving" @click="saveQuickKeyword">Add</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="snackbar.show" :color="snackbar.color" :timeout="3000" location="bottom right">
      {{ snackbar.text }}
    </v-snackbar>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useAdminStore } from '@/stores/adminStore'

const router = useRouter()
const adminStore = useAdminStore()

// ── Tab state ────────────────────────────────────────────────────────────
const tab = ref('work-experience')

// ── Snackbar ─────────────────────────────────────────────────────────────
const snackbar = ref({ show: false, text: '', color: 'success' })
function notify(text, color = 'success') {
  snackbar.value = { show: true, text, color }
}

// ── Stats ─────────────────────────────────────────────────────────────────
const stats = ref(null)
const statItems = computed(() => stats.value ? [
  { label: 'Total Messages', value: stats.value.totalMessages },
  { label: 'Sessions', value: stats.value.totalSessions },
  { label: 'Errors', value: stats.value.errorCount },
  { label: 'Today', value: stats.value.messagesToday },
  { label: 'Avg Response', value: stats.value.avgResponseMs + 'ms' },
] : [])

async function loadStats() {
  try {
    const res = await adminStore.apiFetch('/admin/chats/stats')
    if (res.ok) stats.value = await res.json()
  } catch { }
}

// ═══════════════════════════════════════════════════════════════════════════
// WORK EXPERIENCE
// ═══════════════════════════════════════════════════════════════════════════
const works = ref([])
const worksLoading = ref(false)
const worksSaving = ref(false)
const workDialog = ref(false)
const workDeleteDialog = ref(false)
const editingWorkId = ref(null)
const deletingWorkItem = ref(null)

const workExperienceOptions = computed(() =>
  works.value.map(w => ({
    title: `${w.employer} — ${w.title}`,
    value: w.workExperienceId,
    subtitle: `${w.startYear ?? '?'}${w.endYear ? '–' + w.endYear : '–Present'}`,
  }))
)

const emptyWork = () => ({
  employer: '',
  title: '',
  startYear: '',
  endYear: '',
  summary: '',
  achievements: [],
  displayOrder: 0,
  isActive: true,
})

const wf = ref(emptyWork())

async function loadWorks() {
  worksLoading.value = true
  try {
    const res = await adminStore.apiFetch('/admin/work-experience')
    if (res.ok) works.value = await res.json()
  } catch {
    notify('Failed to load works', 'error')
  } finally {
    worksLoading.value = false
  }
}

function openWorkDialog(work = null) {
  if (work) {
    editingWorkId.value = work.workExperienceId
    wf.value = {
      ...work,
      startYear: work.startYear ?? '',
      endYear: work.endYear ?? '',
      summary: work.summary ?? '',
      achievements: [...(work.achievements || [])],
    }
  } else {
    editingWorkId.value = null
    wf.value = emptyWork()
  }
  workDialog.value = true
}

function addAchievement() { wf.value.achievements.push('') }
function removeAchievement(index) { wf.value.achievements.splice(index, 1) }

async function saveWork() {
  worksSaving.value = true
  try {
    const payload = {
      ...wf.value,
      startYear: wf.value.startYear || null,
      endYear: wf.value.endYear || null,
      achievements: wf.value.achievements.filter(a => a.trim() !== ''),
    }

    const isEdit = !!editingWorkId.value
    const url = isEdit ? `/admin/work-experience/${editingWorkId.value}` : '/admin/work-experience'

    const res = await adminStore.apiFetch(url, {
      method: isEdit ? 'PUT' : 'POST',
      body: JSON.stringify(payload)
    })

    if (!res.ok) throw new Error()
    await loadWorks()
    workDialog.value = false
    notify(isEdit ? 'Work updated' : 'Work created')
  } catch {
    notify('Save failed', 'error')
  } finally {
    worksSaving.value = false
  }
}

function confirmDeleteWork(work) {
  deletingWorkItem.value = work
  workDeleteDialog.value = true
}

async function deleteWork() {
  worksSaving.value = true
  try {
    const res = await adminStore.apiFetch(`/admin/work-experience/${deletingWorkItem.value.workExperienceId}`, {
      method: 'DELETE'
    })
    if (!res.ok) throw new Error()
    await loadWorks()
    workDeleteDialog.value = false
    notify('Work deleted')
  } catch {
    notify('Delete failed', 'error')
  } finally {
    worksSaving.value = false
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// PROJECTS
// ═══════════════════════════════════════════════════════════════════════════
const projects = ref([])
const projectsLoading = ref(false)
const projectSearch = ref('')
const projectFilter = ref('all')
const projectDialog = ref(false)
const editingProject = ref(null)
const saving = ref(false)
const techInput = ref('')

const pf = ref(emptyProject())

function emptyProject() {
  return {
    title: '', workExperienceId: null, role: '', summary: '', detail: '',
    impactStatement: '',
    techStack: [], displayOrder: 0, isFeatured: false, isActive: true,
    startYear: '', endYear: ''
  }
}

const projectHeaders = [
  { title: 'Title', key: 'title', sortable: true },
  { title: 'Employer', key: 'workExperienceId', sortable: false },
  { title: 'Role', key: 'role', sortable: false },
  { title: 'Order', key: 'displayOrder', sortable: true, width: 80 },
  { title: 'Featured', key: 'isFeatured', sortable: true, width: 80 },
  { title: 'Status', key: 'isActive', sortable: true, width: 100 },
  { title: 'Stack', key: 'techStack', sortable: false },
  { title: '', key: 'actions', sortable: false, width: 90 }
]

const filteredProjects = computed(() => {
  let list = projects.value
  if (projectFilter.value === 'active') list = list.filter(p => p.isActive)
  if (projectFilter.value === 'inactive') list = list.filter(p => !p.isActive)
  if (projectFilter.value === 'featured') list = list.filter(p => p.isFeatured)
  return list
})

async function loadProjects() {
  projectsLoading.value = true
  try {
    const res = await adminStore.apiFetch('/admin/projects')
    if (res.ok) projects.value = await res.json()
  } catch (e) {
    notify('Failed to load projects', 'error')
  } finally {
    projectsLoading.value = false
  }
}

function openProjectDialog(project = null) {
  editingProject.value = project
  pf.value = project
    ? {
      ...project,
      workExperienceId: project.workExperienceId ?? null,
      techStack: [...project.techStack],
    }
    : emptyProject()
  techInput.value = ''
  projectDialog.value = true
}

function addTech() {
  const val = techInput.value.trim().replace(/,$/, '')
  if (val && !pf.value.techStack.includes(val)) {
    pf.value.techStack.push(val)
  }
  techInput.value = ''
}

async function saveProject() {
  saving.value = true
  try {
    const isEdit = !!editingProject.value
    const url = isEdit
      ? `/admin/projects/${editingProject.value.projectId}`
      : '/admin/projects'

    const res = await adminStore.apiFetch(url, {
      method: isEdit ? 'PUT' : 'POST',
      body: JSON.stringify(pf.value)
    })

    if (!res.ok) throw new Error()

    await loadProjects()
    projectDialog.value = false
    notify(isEdit ? 'Project updated' : 'Project created')
  } catch {
    notify('Save failed', 'error')
  } finally {
    saving.value = false
  }
}

async function toggleProjectActive(project) {
  try {
    const url = project.isActive
      ? `/admin/projects/${project.projectId}`
      : `/admin/projects/${project.projectId}/restore`

    const res = await adminStore.apiFetch(url, {
      method: project.isActive ? 'DELETE' : 'PATCH'
    })

    if (!res.ok) throw new Error()
    await loadProjects()
    notify(project.isActive ? 'Project deactivated' : 'Project restored')
  } catch {
    notify('Action failed', 'error')
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// INFORMATION
// ═══════════════════════════════════════════════════════════════════════════
const information = ref([])
const infoLoading = ref(false)
const infoSearch = ref('')
const infoDialog = ref(false)
const editingInfo = ref(null)
const kwInput = ref('')
const addKeywordDialog = ref(false)
const quickKw = ref('')
const kwTargetItem = ref(null)

const inf = ref({ text: '', keywords: [] })

const filteredInfo = computed(() => {
  if (!infoSearch.value) return information.value
  const q = infoSearch.value.toLowerCase()
  return information.value.filter(i =>
    i.text?.toLowerCase().includes(q) ||
    i.keywords.some(k => k.toLowerCase().includes(q))
  )
})

async function loadInformation() {
  infoLoading.value = true
  try {
    const res = await adminStore.apiFetch('/admin/information')
    if (res.ok) information.value = await res.json()
  } catch {
    notify('Failed to load information', 'error')
  } finally {
    infoLoading.value = false
  }
}

function openInfoDialog(item = null) {
  editingInfo.value = item
  inf.value = item
    ? { text: item.text ?? '', keywords: [...item.keywords] }
    : { text: '', keywords: [] }
  kwInput.value = ''
  infoDialog.value = true
}

function addKeyword() {
  const val = kwInput.value.trim().replace(/,$/, '')
  if (val && !inf.value.keywords.includes(val)) {
    inf.value.keywords.push(val)
  }
  kwInput.value = ''
}

async function saveInfo() {
  saving.value = true
  try {
    const isEdit = !!editingInfo.value
    const url = isEdit
      ? `/admin/information/${editingInfo.value.informationId}`
      : '/admin/information'

    const res = await adminStore.apiFetch(url, {
      method: isEdit ? 'PUT' : 'POST',
      body: JSON.stringify(inf.value)
    })

    if (!res.ok) throw new Error()
    await loadInformation()
    infoDialog.value = false
    notify(isEdit ? 'Entry updated' : 'Entry created')
  } catch {
    notify('Save failed', 'error')
  } finally {
    saving.value = false
  }
}

async function deleteInfo(item) {
  if (!confirm(`Delete this information entry? This cannot be undone.`)) return
  try {
    const res = await adminStore.apiFetch(`/admin/information/${item.informationId}`, {
      method: 'DELETE'
    })
    if (!res.ok) throw new Error()
    await loadInformation()
    notify('Entry deleted')
  } catch {
    notify('Delete failed', 'error')
  }
}

// Quick keyword management inline on cards
function openAddKeyword(item) {
  kwTargetItem.value = item
  quickKw.value = ''
  addKeywordDialog.value = true
}

async function saveQuickKeyword() {
  if (!quickKw.value.trim() || !kwTargetItem.value) return
  saving.value = true
  try {
    const res = await adminStore.apiFetch(
      `/admin/information/${kwTargetItem.value.informationId}/keywords`,
      { method: 'POST', body: JSON.stringify({ text: quickKw.value.trim() }) }
    )
    if (!res.ok) throw new Error()
    await loadInformation()
    addKeywordDialog.value = false
    notify('Keyword added')
  } catch {
    notify('Failed to add keyword', 'error')
  } finally {
    saving.value = false
  }
}

async function deleteKeyword(item, keyword) {
  try {
    const updated = { text: item.text, keywords: item.keywords.filter(k => k !== keyword) }
    const putRes = await adminStore.apiFetch(`/admin/information/${item.informationId}`, {
      method: 'PUT',
      body: JSON.stringify(updated)
    })
    if (!putRes.ok) throw new Error()
    await loadInformation()
    notify('Keyword removed')
  } catch {
    notify('Failed to remove keyword', 'error')
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// CHAT LOGS
// ═══════════════════════════════════════════════════════════════════════════
const chatSessions = ref([])
const chatsLoading = ref(false)
const errorsOnly = ref(false)
const chatPage = ref(1)
const chatPageSize = 20

async function loadChats() {
  chatsLoading.value = true
  try {
    const params = new URLSearchParams({
      page: chatPage.value,
      pageSize: chatPageSize,
      errorsOnly: errorsOnly.value
    })
    const res = await adminStore.apiFetch(`/admin/chats?${params}`)
    if (res.ok) chatSessions.value = await res.json()
  } catch {
    notify('Failed to load chat logs', 'error')
  } finally {
    chatsLoading.value = false
  }
}

// ── Date formatting ───────────────────────────────────────────────────────
function formatDate(iso) {
  return new Date(iso).toLocaleDateString('en-US', {
    month: 'short', day: 'numeric', year: 'numeric'
  })
}

function formatDateTime(iso) {
  return new Date(iso).toLocaleString('en-US', {
    month: 'short', day: 'numeric',
    hour: 'numeric', minute: '2-digit', hour12: true
  })
}

// ── Logout ────────────────────────────────────────────────────────────────
function handleLogout() {
  adminStore.logout()
  router.replace('/admin/login')
}

// ── Load on tab change ────────────────────────────────────────────────────
watch(tab, (val) => {
  if (val === 'work-experience' && !works.value.length) loadWorks()
  if (val === 'projects') {
    if (!projects.value.length) loadProjects()
    if (!works.value.length) loadWorks()   // needed for the employer dropdown
  }
  if (val === 'information' && !information.value.length) loadInformation()
  if (val === 'chats' && !chatSessions.value.length) loadChats()
})

// ── Initial load ──────────────────────────────────────────────────────────
onMounted(() => {
  loadStats()
  loadWorks() // Initial tab is work-experience
})
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

.admin-header__left {
  display: flex;
  align-items: center;
}

.admin-title {
  font-family: 'Patua One', serif;
  font-size: 1.3rem;
  color: rgb(var(--v-theme-on-surface));
}

/* ── Stats bar ── */
.stats-bar {
  display: flex;
  gap: 0;
  background: rgb(var(--v-theme-surface));
  border-bottom: 1px solid rgba(var(--v-theme-secondary), 0.1);
  overflow-x: auto;
}

.stat-chip {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 0.6rem 1.5rem;
  border-right: 1px solid rgba(var(--v-theme-secondary), 0.1);
  min-width: 100px;
}

.stat-chip__value {
  font-size: 1.3rem;
  font-weight: 700;
  color: rgb(var(--v-theme-secondary));
  line-height: 1;
}

.stat-chip__label {
  font-size: 0.7rem;
  color: rgba(var(--v-theme-on-surface), 0.5);
  text-transform: uppercase;
  letter-spacing: 0.05em;
  margin-top: 0.2rem;
}

/* ── Tabs ── */
.admin-tabs {
  background: rgb(var(--v-theme-surface));
}

/* ── Tab content ── */
.admin-tab-content {
  padding: 1.5rem;
}

/* ── Toolbar ── */
.tab-toolbar {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 1.5rem;
}

.toolbar-search {
  max-width: 300px;
}

/* ── Work Rows ── */
.works-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.work-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.75rem 1rem;
  background: rgb(var(--v-theme-surface));
  border: 1px solid rgba(var(--v-theme-secondary), 0.15);
  border-radius: 8px;
  gap: 1rem;
  transition: border-color 0.15s;
}

.work-row:hover {
  border-color: rgba(var(--v-theme-secondary), 0.35);
}

.work-row--inactive {
  opacity: 0.55;
}

.work-row__info {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
  flex: 1;
  min-width: 0;
}

.work-row__title {
  font-size: 0.875rem;
  font-weight: 700;
  color: rgb(var(--v-theme-on-surface));
}

.work-row__employer {
  font-size: 0.8rem;
  color: rgba(var(--v-theme-on-surface), 0.6);
}

.work-row__years {
  font-size: 0.75rem;
  color: rgb(var(--v-theme-secondary));
  font-weight: 600;
}

.work-row__meta {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-shrink: 0;
}

.work-row__achievements {
  font-size: 0.72rem;
  color: rgba(var(--v-theme-on-surface), 0.4);
}

/* ── Dialogs ── */
.dialog-card {
  background: rgb(var(--v-theme-surface));
}

.dialog-title {
  display: flex;
  align-items: center;
  font-family: 'Patua One', serif;
  font-size: 1.2rem;
  padding: 1rem 1.5rem;
}

.dialog-body {
  padding: 1.5rem !important;
}

.dialog-actions {
  padding: 0.75rem 1.5rem;
}

.achievements-label {
  font-size: 0.78rem;
  font-weight: 700;
  color: rgba(var(--v-theme-on-surface), 0.6);
  text-transform: uppercase;
  letter-spacing: 0.06em;
  margin-bottom: 0.5rem;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.achievement-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.5rem;
}

.achievement-field {
  flex: 1;
}

.achievements-empty {
  font-size: 0.8rem;
  color: rgba(var(--v-theme-on-surface), 0.35);
  font-style: italic;
  margin: 0;
}

/* ── Info Grid ── */
.info-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 1.25rem;
}

.info-card {
  border: 1px solid rgba(var(--v-theme-secondary), 0.15);
  border-radius: 12px;
  background: rgb(var(--v-theme-surface));
  display: flex;
  flex-direction: column;
}

.info-card__body {
  padding: 1.25rem;
  flex: 1;
}

.info-card__text {
  font-size: 0.9rem;
  line-height: 1.6;
  color: rgb(var(--v-theme-on-surface));
  margin-bottom: 1rem;
}

.info-card__actions {
  padding: 0.5rem 1rem;
  display: flex;
  justify-content: flex-end;
  gap: 0.25rem;
}

.tag-input-wrap {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  padding: 0.5rem;
  border: 1px solid rgba(var(--v-theme-secondary), 0.2);
  border-radius: 8px;
  min-height: 42px;
}

.tag-input {
  border: none;
  outline: none;
  background: transparent;
  color: rgb(var(--v-theme-on-surface));
  font-size: 0.85rem;
  flex: 1;
  min-width: 120px;
  padding: 0.25rem;
}

.field-label {
  font-size: 0.8rem;
  font-weight: 700;
  color: rgba(var(--v-theme-on-surface), 0.7);
  margin-bottom: 0.25rem;
}

.field-hint {
  font-size: 0.75rem;
  color: rgba(var(--v-theme-on-surface), 0.45);
  line-height: 1.3;
}

.loading-state {
  display: flex;
  justify-content: center;
  padding: 3rem;
}


/* ── Toolbar ── */
.tab-toolbar {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 1.25rem;
  flex-wrap: wrap;
}

.toolbar-search {
  max-width: 280px;
}

/* ── Table ── */
.admin-table {
  background: rgb(var(--v-theme-surface)) !important;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 12px !important;
}

.chip-wrap {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 2px;
}

.row-actions {
  display: flex;
  gap: 4px;
}

/* ── Loading ── */
.loading-state {
  display: flex;
  justify-content: center;
  padding: 4rem;
}

/* ── Information grid ── */
.info-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(340px, 1fr));
  gap: 1rem;
}

@media (max-width: 599px) {
  .info-grid {
    grid-template-columns: 1fr;
  }
}

.info-card {
  background: rgb(var(--v-theme-surface)) !important;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 12px !important;
  display: flex;
  flex-direction: column;
}

.info-card__body {
  padding: 1rem 1rem 0.75rem;
  flex: 1;
}

.info-card__text {
  font-size: 0.875rem;
  line-height: 1.65;
  color: rgba(var(--v-theme-on-surface), 0.85);
  margin: 0 0 0.75rem;
  /* Truncate long entries */
  display: -webkit-box;
  -webkit-line-clamp: 4;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.info-card__keywords {
  display: flex;
  flex-wrap: wrap;
}

.add-keyword-chip {
  cursor: pointer;
}

.info-card__actions {
  display: flex;
  gap: 0.25rem;
  padding: 0.4rem 0.5rem;
}

/* ── Chat sessions ── */
.sessions-list {}

.sessions-panels {
  border-radius: 12px !important;
  overflow: hidden;
}

.session-panel {
  background: rgb(var(--v-theme-surface)) !important;
  border-bottom: 1px solid rgba(255, 255, 255, 0.06) !important;
}

.session-header {
  font-family: 'Raleway', sans-serif;
  font-size: 0.875rem;
}

.session-header__left {
  display: flex;
  align-items: center;
  gap: 4px;
}

.session-header__right {
  margin-left: auto;
  padding-right: 1rem;
}

.session-id {
  font-family: monospace;
  font-size: 0.8rem;
  color: rgba(var(--v-theme-on-surface), 0.7);
}

.session-meta {
  font-size: 0.75rem;
  color: rgba(var(--v-theme-on-surface), 0.45);
}

/* ── Chat exchange ── */
.session-messages {
  padding: 0 !important;
}

.chat-exchange {
  padding: 1rem 1.25rem;
  border-top: 1px solid rgba(255, 255, 255, 0.05);
}

.chat-exchange--error {
  border-left: 3px solid rgb(var(--v-theme-error));
  background: rgba(var(--v-theme-error), 0.04);
}

.exchange-meta {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.5rem;
}

.exchange-time {
  font-size: 0.72rem;
  color: rgba(var(--v-theme-on-surface), 0.4);
}

.exchange-duration {
  font-size: 0.72rem;
  color: rgba(var(--v-theme-secondary), 0.6);
  font-family: monospace;
}

.exchange-label {
  font-size: 0.7rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: rgba(var(--v-theme-on-surface), 0.4);
  display: block;
  margin-bottom: 0.2rem;
}

.exchange-label--assistant {
  color: rgb(var(--v-theme-secondary));
}

.exchange-user,
.exchange-assistant {
  margin-bottom: 0.6rem;
}

.exchange-text {
  font-size: 0.875rem;
  line-height: 1.6;
  color: rgba(var(--v-theme-on-surface), 0.85);
  margin: 0;
  white-space: pre-wrap;
}

/* ── Pagination ── */
.pagination-bar {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 1rem;
  margin-top: 1rem;
}

.page-label {
  font-size: 0.85rem;
  color: rgba(var(--v-theme-on-surface), 0.5);
}

/* ── Dialogs ── */
.dialog-card {
  background: rgb(var(--v-theme-surface)) !important;
}

.dialog-title {
  display: flex;
  align-items: center;
  font-family: 'Patua One', serif;
  font-size: 1.1rem;
  padding: 1rem 1.25rem !important;
}

.dialog-body {
  padding: 1.25rem !important;
}

.dialog-actions {
  padding: 0.75rem 1rem !important;
}

.field-label {
  font-size: 0.8rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: rgba(var(--v-theme-on-surface), 0.55);
}

.field-hint {
  font-size: 0.78rem;
  color: rgba(var(--v-theme-on-surface), 0.45);
  margin: 0.2rem 0 0;
  line-height: 1.5;
}

/* ── Tag input ── */
.tag-input-wrap {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 4px;
  border: 1px solid rgba(var(--v-theme-secondary), 0.3);
  border-radius: 8px;
  padding: 0.5rem 0.75rem;
  min-height: 44px;
  background: transparent;
  cursor: text;
}

.tag-input {
  border: none;
  outline: none;
  background: transparent;
  color: rgb(var(--v-theme-on-surface));
  font-family: 'Raleway', sans-serif;
  font-size: 0.875rem;
  flex: 1;
  min-width: 120px;
}

/* ── Toggle group (Featured / Active in project dialog) ── */
.toggle-group {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 2px;
}

.toggle-label {
  font-size: 0.72rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: rgba(var(--v-theme-on-surface), 0.5);
}
</style>