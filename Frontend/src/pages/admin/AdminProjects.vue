<template>
  <div>
    <div class="tab-toolbar">
      <v-btn color="secondary" variant="tonal" @click="openDialog()">
        <v-icon start>mdi-plus</v-icon>New Project
      </v-btn>
      <v-text-field v-model="search" placeholder="Search projects..." prepend-inner-icon="mdi-magnify"
        variant="outlined" density="compact" hide-details class="toolbar-search" clearable />
      <v-chip-group v-model="filter" class="ml-2">
        <v-chip value="all" size="small" filter>All</v-chip>
        <v-chip value="active" size="small" filter>Active</v-chip>
        <v-chip value="inactive" size="small" filter>Inactive</v-chip>
        <v-chip value="featured" size="small" filter>Featured</v-chip>
      </v-chip-group>
    </div>

    <div v-if="loading" class="loading-state">
      <v-progress-circular indeterminate color="secondary" />
    </div>

    <v-data-table v-else :headers="headers" :items="filteredProjects" :search="search"
      item-value="projectId" class="admin-table" hover>
      <template #item.isActive="{ item }">
        <v-chip :color="item.isActive ? 'success' : 'error'" size="x-small" variant="tonal">
          {{ item.isActive ? 'Active' : 'Inactive' }}
        </v-chip>
      </template>
      <template #item.isFeatured="{ item }">
        <v-icon :color="item.isFeatured ? 'yellow' : 'surface-variant'" size="18">
          {{ item.isFeatured ? 'mdi-star' : 'mdi-star-outline' }}
        </v-icon>
      </template>
      <template #item.techStack="{ item }">
        <div class="chip-wrap">
          <v-chip v-for="t in item.techStack.slice(0, 3)" :key="t" size="x-small" variant="tonal" color="secondary" class="mr-1">{{ t }}</v-chip>
          <span v-if="item.techStack.length > 3" class="text-caption text-medium-emphasis">+{{ item.techStack.length - 3 }}</span>
        </div>
      </template>
      <template #item.hasEmbedding="{ item }">
        <v-icon :color="item.hasEmbedding ? 'success' : 'warning'" size="18">
          {{ item.hasEmbedding ? 'mdi-brain' : 'mdi-brain-off' }}
        </v-icon>
      </template>
      <template #item.workExperienceId="{ item }">
        <span class="text-body-2">{{ works.find(w => w.workExperienceId === item.workExperienceId)?.employer ?? '—' }}</span>
      </template>
      <template #item.actions="{ item }">
        <div class="row-actions">
          <v-btn icon size="x-small" variant="text" @click="openDialog(item)" title="Edit">
            <v-icon>mdi-pencil</v-icon>
          </v-btn>
          <v-btn icon size="x-small" variant="text" color="secondary" title="Regenerate embedding"
            :loading="embeddingItemLoading === item.projectId"
            @click="regenerateEmbedding(item.projectId)">
            <v-icon>mdi-refresh</v-icon>
          </v-btn>
          <v-btn icon size="x-small" variant="text" :color="item.isActive ? 'error' : 'success'"
            @click="toggleActive(item)" :title="item.isActive ? 'Deactivate' : 'Restore'">
            <v-icon>{{ item.isActive ? 'mdi-eye-off' : 'mdi-eye' }}</v-icon>
          </v-btn>
        </div>
      </template>
    </v-data-table>

    <!-- Create / Edit dialog -->
    <v-dialog v-model="dialogOpen" max-width="900" scrollable>
      <v-card class="dialog-card">
        <v-card-title class="dialog-title">
          {{ editingProject ? 'Edit Project' : 'New Project' }}
          <v-btn icon variant="text" size="small" @click="dialogOpen = false" class="ml-auto">
            <v-icon>mdi-close</v-icon>
          </v-btn>
        </v-card-title>
        <v-divider />
        <v-card-text class="dialog-body">
          <v-row>
            <v-col cols="12">
              <v-text-field v-model="form.title" label="Title *" variant="outlined" density="comfortable" color="secondary" />
            </v-col>
            <v-col cols="12" md="6">
              <v-autocomplete v-model="form.workExperienceId" :items="workOptions" item-title="title" item-value="value"
                label="Work Experience (optional)" variant="outlined" density="comfortable" color="secondary" clearable>
                <template #item="{ props, item }">
                  <v-list-item v-bind="props" :subtitle="item.raw.subtitle" />
                </template>
                <template #prepend-inner>
                  <v-icon :color="form.workExperienceId ? 'secondary' : 'surface-variant'" size="18" class="mr-1">mdi-briefcase-outline</v-icon>
                </template>
              </v-autocomplete>
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field v-model="form.role" label="Role *" variant="outlined" density="comfortable" color="secondary" />
            </v-col>
            <v-col cols="6" md="3">
              <v-text-field v-model="form.startYear" label="Start Year" variant="outlined" density="comfortable" color="secondary" />
            </v-col>
            <v-col cols="6" md="3">
              <v-text-field v-model="form.endYear" label="End Year" variant="outlined" density="comfortable" color="secondary" placeholder="Present" />
            </v-col>
            <v-col cols="6" md="3">
              <v-text-field v-model.number="form.displayOrder" label="Display Order" type="number" variant="outlined" density="comfortable" color="secondary" />
            </v-col>
            <v-col cols="6" md="3" class="d-flex align-center justify-center" style="gap:2rem;padding-bottom:0.5rem">
              <div class="toggle-group">
                <v-switch v-model="form.isFeatured" color="secondary" density="compact" hide-details inset />
                <span class="toggle-label">Featured</span>
              </div>
              <div class="toggle-group">
                <v-switch v-model="form.isActive" color="success" density="compact" hide-details inset />
                <span class="toggle-label">Active</span>
              </div>
            </v-col>
            <v-col cols="12">
              <v-textarea v-model="form.summary" label="Summary *" variant="outlined" density="comfortable" rows="4" color="secondary" auto-grow />
            </v-col>
            <v-col cols="12">
              <v-textarea v-model="form.detail" label="Detail (optional)" variant="outlined" density="comfortable" rows="4" color="secondary" auto-grow />
            </v-col>
            <v-col cols="12">
              <v-textarea v-model="form.impactStatement" label="Impact Statement (optional)" variant="outlined" density="comfortable" rows="2" color="secondary" auto-grow
                hint="A single punchy sentence quantifying the outcome" persistent-hint />
            </v-col>
            <v-col cols="12">
              <label class="field-label">Tech Stack</label>
              <p class="field-hint mb-2">Press Enter or comma after each item.</p>
              <div class="tag-input-wrap">
                <v-chip v-for="(t, i) in form.techStack" :key="t" size="small" closable class="mr-1 mb-1" @click:close="form.techStack.splice(i, 1)">{{ t }}</v-chip>
                <input v-model="techInput" class="tag-input" placeholder="Add tech, press Enter"
                  @keydown.enter.prevent="addTech" @keydown.comma.prevent="addTech" />
              </div>
            </v-col>
          </v-row>
        </v-card-text>
        <v-divider />
        <v-card-actions class="dialog-actions">
          <v-spacer />
          <v-btn variant="text" @click="dialogOpen = false">Cancel</v-btn>
          <v-btn color="secondary" variant="tonal" :loading="saving" @click="save">
            {{ editingProject ? 'Save Changes' : 'Create Project' }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-snackbar v-model="snackbar.show" :color="snackbar.color" :timeout="3000" location="bottom right">
      {{ snackbar.text }}
    </v-snackbar>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useAdminStore } from '@/stores/adminStore'

const adminStore = useAdminStore()

const projects = ref([])
const works = ref([])
const loading = ref(false)
const saving = ref(false)
const search = ref('')
const filter = ref('all')
const dialogOpen = ref(false)
const editingProject = ref(null)
const techInput = ref('')
const embeddingItemLoading = ref(null)
const snackbar = ref({ show: false, text: '', color: 'success' })

const emptyForm = () => ({ title: '', workExperienceId: null, role: '', summary: '', detail: '', impactStatement: '', techStack: [], displayOrder: 0, isFeatured: false, isActive: true, startYear: '', endYear: '' })
const form = ref(emptyForm())

const headers = [
  { title: 'Title', key: 'title', sortable: true },
  { title: 'Employer', key: 'workExperienceId', sortable: false },
  { title: 'Role', key: 'role', sortable: false },
  { title: 'Order', key: 'displayOrder', sortable: true, width: 80 },
  { title: 'Featured', key: 'isFeatured', sortable: true, width: 80 },
  { title: 'Status', key: 'isActive', sortable: true, width: 100 },
  { title: 'AI', key: 'hasEmbedding', sortable: false, width: 80 },
  { title: 'Stack', key: 'techStack', sortable: false },
  { title: '', key: 'actions', sortable: false, width: 110 },
]

const workOptions = computed(() => works.value.map(w => ({
  title: `${w.employer} — ${w.title}`,
  value: w.workExperienceId,
  subtitle: `${w.startYear ?? '?'}${w.endYear ? '–' + w.endYear : '–Present'}`,
})))

const filteredProjects = computed(() => {
  let list = projects.value
  if (filter.value === 'active') list = list.filter(p => p.isActive)
  if (filter.value === 'inactive') list = list.filter(p => !p.isActive)
  if (filter.value === 'featured') list = list.filter(p => p.isFeatured)
  return list
})

function notify(text, color = 'success') { snackbar.value = { show: true, text, color } }

async function load() {
  loading.value = true
  try {
    const [pRes, wRes] = await Promise.all([
      adminStore.apiFetch('/admin/projects'),
      adminStore.apiFetch('/admin/work-experience'),
    ])
    if (pRes.ok) projects.value = await pRes.json()
    if (wRes.ok) works.value = await wRes.json()
  } catch { notify('Failed to load projects', 'error') }
  finally { loading.value = false }
}

function openDialog(project = null) {
  editingProject.value = project
  form.value = project ? { ...project, techStack: [...project.techStack] } : emptyForm()
  techInput.value = ''
  dialogOpen.value = true
}

function addTech() {
  const val = techInput.value.trim().replace(/,$/, '')
  if (val && !form.value.techStack.includes(val)) form.value.techStack.push(val)
  techInput.value = ''
}

async function save() {
  saving.value = true
  try {
    const isEdit = !!editingProject.value
    const res = await adminStore.apiFetch(
      isEdit ? `/admin/projects/${editingProject.value.projectId}` : '/admin/projects',
      { method: isEdit ? 'PUT' : 'POST', body: JSON.stringify(form.value) }
    )
    if (!res.ok) throw new Error()
    await load()
    dialogOpen.value = false
    notify(isEdit ? 'Project updated' : 'Project created')
  } catch { notify('Save failed', 'error') }
  finally { saving.value = false }
}

async function toggleActive(project) {
  try {
    const res = await adminStore.apiFetch(
      project.isActive ? `/admin/projects/${project.projectId}` : `/admin/projects/${project.projectId}/restore`,
      { method: project.isActive ? 'DELETE' : 'PATCH' }
    )
    if (!res.ok) throw new Error()
    await load()
    notify(project.isActive ? 'Project deactivated' : 'Project restored')
  } catch { notify('Action failed', 'error') }
}

async function regenerateEmbedding(id) {
  embeddingItemLoading.value = id
  try {
    const res = await adminStore.apiFetch(`/admin/generate-embeddings/project/${id}`, { method: 'POST' })
    if (!res.ok) throw new Error()
    await load()
    notify('Embedding regenerated')
  } catch { notify('Failed to regenerate embedding', 'error') }
  finally { embeddingItemLoading.value = null }
}

defineExpose({ load })
onMounted(load)
</script>

<style scoped>
.tab-toolbar { display: flex; align-items: center; gap: 0.75rem; margin-bottom: 1.5rem; flex-wrap: wrap; }
.toolbar-search { max-width: 280px; }
.loading-state { display: flex; justify-content: center; padding: 4rem; }
.admin-table { background: rgb(var(--v-theme-surface)) !important; border: 1px solid rgba(255,255,255,0.08); border-radius: 12px !important; }
.chip-wrap { display: flex; flex-wrap: wrap; align-items: center; gap: 2px; }
.row-actions { display: flex; gap: 4px; }

.dialog-card { background: rgb(var(--v-theme-surface)); }
.dialog-title { display: flex; align-items: center; font-family: 'Patua One', serif; font-size: 1.1rem; padding: 1rem 1.25rem !important; }
.dialog-body { padding: 1.25rem !important; }
.dialog-actions { padding: 0.75rem 1rem !important; }

.field-label { font-size: 0.8rem; font-weight: 600; text-transform: uppercase; letter-spacing: 0.04em; color: rgba(var(--v-theme-on-surface), 0.55); }
.field-hint { font-size: 0.78rem; color: rgba(var(--v-theme-on-surface), 0.45); margin: 0.2rem 0 0; line-height: 1.5; }

.tag-input-wrap { display: flex; flex-wrap: wrap; align-items: center; gap: 4px; border: 1px solid rgba(var(--v-theme-secondary), 0.3); border-radius: 8px; padding: 0.5rem 0.75rem; min-height: 44px; }
.tag-input { border: none; outline: none; background: transparent; color: rgb(var(--v-theme-on-surface)); font-family: 'Raleway', sans-serif; font-size: 0.875rem; flex: 1; min-width: 120px; }

.toggle-group { display: flex; flex-direction: column; align-items: center; gap: 2px; }
.toggle-label { font-size: 0.72rem; font-weight: 600; text-transform: uppercase; letter-spacing: 0.05em; color: rgba(var(--v-theme-on-surface), 0.5); }
</style>