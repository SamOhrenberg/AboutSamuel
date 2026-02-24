<template>
  <div>
    <div class="tab-toolbar">
      <v-btn color="secondary" variant="tonal" @click="openDialog()">
        <v-icon start>mdi-plus</v-icon>Add Work
      </v-btn>
    </div>

    <v-progress-linear v-if="loading" indeterminate color="secondary" class="mb-4" />

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
          <v-chip :color="work.hasEmbedding ? 'success' : 'warning'" size="x-small" variant="tonal"
            :title="work.hasEmbedding ? 'Embedding up to date' : 'No embedding'">
            <v-icon start size="10">{{ work.hasEmbedding ? 'mdi-brain' : 'mdi-brain-off' }}</v-icon>
            {{ work.hasEmbedding ? 'Embedded' : 'No embedding' }}
          </v-chip>
          <v-btn icon size="x-small" variant="text" color="secondary" title="Regenerate embedding"
            :loading="embeddingItemLoading === work.workExperienceId"
            @click.stop="regenerateEmbedding(work.workExperienceId)">
            <v-icon>mdi-refresh</v-icon>
          </v-btn>
          <v-btn icon size="x-small" variant="text" @click="openDialog(work)">
            <v-icon>mdi-pencil</v-icon>
          </v-btn>
          <v-btn icon size="x-small" variant="text" color="error" @click="confirmDelete(work)">
            <v-icon>mdi-delete</v-icon>
          </v-btn>
        </div>
      </div>
    </div>

    <p v-else class="admin-empty">No work experience entries yet.</p>

    <!-- Create / Edit dialog -->
    <v-dialog v-model="dialogOpen" max-width="720" persistent scrollable>
      <v-card class="dialog-card">
        <v-card-title class="dialog-title">
          {{ editingId ? 'Edit Work Experience' : 'Add Work Experience' }}
          <v-btn icon variant="text" size="small" @click="dialogOpen = false" class="ml-auto">
            <v-icon>mdi-close</v-icon>
          </v-btn>
        </v-card-title>
        <v-divider />
        <v-card-text class="dialog-body">
          <v-row dense>
            <v-col cols="12" sm="6">
              <v-text-field v-model="form.employer" label="Employer" variant="outlined" density="comfortable"
                color="secondary" />
            </v-col>
            <v-col cols="12" sm="6">
              <v-text-field v-model="form.title" label="Work Title" variant="outlined" density="comfortable"
                color="secondary" />
            </v-col>
            <v-col cols="6" sm="3">
              <v-text-field v-model="form.startYear" label="Start Year" variant="outlined" density="comfortable"
                color="secondary" placeholder="2019" />
            </v-col>
            <v-col cols="6" sm="3">
              <v-text-field v-model="form.endYear" label="End Year" variant="outlined" density="comfortable"
                color="secondary" placeholder="Present" />
            </v-col>
            <v-col cols="12" sm="6">
              <v-switch v-model="form.isActive" label="Active" color="secondary" density="comfortable" hide-details />
            </v-col>
            <v-col cols="12">
              <v-textarea v-model="form.summary" label="Summary (optional)" variant="outlined" density="comfortable"
                color="secondary" rows="2" auto-grow />
            </v-col>
            <v-col cols="12">
              <div class="achievements-label">
                Achievements
                <v-btn size="x-small" variant="text" color="secondary" @click="addAchievement">+ Add</v-btn>
              </div>
              <div v-for="(_, i) in form.achievements" :key="i" class="achievement-row">
                <v-text-field v-model="form.achievements[i]" :label="`Achievement ${i + 1}`" variant="outlined"
                  density="compact" color="secondary" hide-details class="achievement-field" />
                <v-btn icon size="x-small" variant="text" color="error" @click="form.achievements.splice(i, 1)">
                  <v-icon>mdi-close</v-icon>
                </v-btn>
              </div>
              <p v-if="!form.achievements.length" class="achievements-empty">No achievements added yet.</p>
            </v-col>
            <v-col cols="12" sm="4">
              <v-text-field v-model.number="form.displayOrder" label="Display Order" type="number" variant="outlined"
                density="comfortable" color="secondary" hide-details />
            </v-col>
          </v-row>
        </v-card-text>
        <v-divider />
        <v-card-actions class="dialog-actions">
          <v-spacer />
          <v-btn variant="text" @click="dialogOpen = false">Cancel</v-btn>
          <v-btn color="secondary" variant="tonal" :loading="saving" @click="save">
            {{ editingId ? 'Save Changes' : 'Create' }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Delete confirmation -->
    <v-dialog v-model="deleteDialogOpen" max-width="400">
      <v-card>
        <v-card-title>Delete Work Experience</v-card-title>
        <v-card-text>
          Are you sure you want to delete <strong>{{ deletingWork?.title }}</strong>?
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteDialogOpen = false">Cancel</v-btn>
          <v-btn color="error" variant="flat" :loading="saving" @click="deleteWork">Delete</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="snackbar.show" :color="snackbar.color" :timeout="3000" location="bottom right">
      {{ snackbar.text }}
    </v-snackbar>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useAdminStore } from '@/stores/adminStore'

const adminStore = useAdminStore()

const works = ref([])
const loading = ref(false)
const saving = ref(false)
const dialogOpen = ref(false)
const deleteDialogOpen = ref(false)
const editingId = ref(null)
const deletingWork = ref(null)
const embeddingItemLoading = ref(null)
const snackbar = ref({ show: false, text: '', color: 'success' })

const emptyForm = () => ({
  employer: '', title: '', startYear: '', endYear: '',
  summary: '', achievements: [], displayOrder: 0, isActive: true,
})
const form = ref(emptyForm())

function notify(text, color = 'success') {
  snackbar.value = { show: true, text, color }
}

async function load() {
  loading.value = true
  try {
    const res = await adminStore.apiFetch('/admin/work-experience')
    if (res.ok) works.value = await res.json()
  } catch { notify('Failed to load work experience', 'error') }
  finally { loading.value = false }
}

function openDialog(work = null) {
  editingId.value = work?.workExperienceId ?? null
  form.value = work
    ? { ...work, startYear: work.startYear ?? '', endYear: work.endYear ?? '', summary: work.summary ?? '', achievements: [...(work.achievements || [])] }
    : emptyForm()
  dialogOpen.value = true
}

function addAchievement() { form.value.achievements.push('') }

async function save() {
  saving.value = true
  try {
    const payload = { ...form.value, startYear: form.value.startYear || null, endYear: form.value.endYear || null, achievements: form.value.achievements.filter(a => a.trim()) }
    const isEdit = !!editingId.value
    const res = await adminStore.apiFetch(
      isEdit ? `/admin/work-experience/${editingId.value}` : '/admin/work-experience',
      { method: isEdit ? 'PUT' : 'POST', body: JSON.stringify(payload) }
    )
    if (!res.ok) throw new Error()
    await load()
    dialogOpen.value = false
    notify(isEdit ? 'Work updated' : 'Work created')
  } catch { notify('Save failed', 'error') }
  finally { saving.value = false }
}

function confirmDelete(work) {
  deletingWork.value = work
  deleteDialogOpen.value = true
}

async function deleteWork() {
  saving.value = true
  try {
    const res = await adminStore.apiFetch(`/admin/work-experience/${deletingWork.value.workExperienceId}`, { method: 'DELETE' })
    if (!res.ok) throw new Error()
    await load()
    deleteDialogOpen.value = false
    notify('Work deleted')
  } catch { notify('Delete failed', 'error') }
  finally { saving.value = false }
}

async function regenerateEmbedding(id) {
  embeddingItemLoading.value = id
  try {
    const res = await adminStore.apiFetch(`/admin/generate-embeddings/work-experience/${id}`, { method: 'POST' })
    if (!res.ok) throw new Error()
    await load()
    notify('Embedding regenerated')
  } catch { notify('Failed to regenerate embedding', 'error') }
  finally { embeddingItemLoading.value = null }
}

// Expose load so parent can trigger after global embed regeneration
defineExpose({ load })
onMounted(load)
</script>

<style scoped>
.works-list { display: flex; flex-direction: column; gap: 0.5rem; }

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
.work-row:hover { border-color: rgba(var(--v-theme-secondary), 0.35); }
.work-row--inactive { opacity: 0.55; }

.work-row__info { display: flex; align-items: center; gap: 0.75rem; flex-wrap: wrap; flex: 1; min-width: 0; }
.work-row__title { font-size: 0.875rem; font-weight: 700; color: rgb(var(--v-theme-on-surface)); }
.work-row__employer { font-size: 0.8rem; color: rgba(var(--v-theme-on-surface), 0.6); }
.work-row__years { font-size: 0.75rem; color: rgb(var(--v-theme-secondary)); font-weight: 600; }
.work-row__meta { display: flex; align-items: center; gap: 0.5rem; flex-shrink: 0; }
.work-row__achievements { font-size: 0.72rem; color: rgba(var(--v-theme-on-surface), 0.4); }

.admin-empty { color: rgba(var(--v-theme-on-surface), 0.45); font-size: 0.875rem; padding: 1rem 0; }

.dialog-card { background: rgb(var(--v-theme-surface)); }
.dialog-title { display: flex; align-items: center; font-family: 'Patua One', serif; font-size: 1.1rem; padding: 1rem 1.25rem !important; }
.dialog-body { padding: 1.25rem !important; }
.dialog-actions { padding: 0.75rem 1rem !important; }

.achievements-label { font-size: 0.78rem; font-weight: 700; color: rgba(var(--v-theme-on-surface), 0.6); text-transform: uppercase; letter-spacing: 0.06em; margin-bottom: 0.5rem; display: flex; align-items: center; gap: 0.5rem; }
.achievement-row { display: flex; align-items: center; gap: 0.5rem; margin-bottom: 0.5rem; }
.achievement-field { flex: 1; }
.achievements-empty { font-size: 0.8rem; color: rgba(var(--v-theme-on-surface), 0.35); font-style: italic; margin: 0; }

.tab-toolbar { display: flex; align-items: center; gap: 0.75rem; margin-bottom: 1.5rem; flex-wrap: wrap; }
</style>