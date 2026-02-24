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
      <!-- Saving indicator -->
      <Transition name="fade">
        <span v-if="reorderSaving" class="reorder-saving">
          <v-progress-circular indeterminate size="14" width="2" color="secondary" class="mr-1" />
          Saving order...
        </span>
        <span v-else-if="reorderSaved" class="reorder-saved">
          <v-icon size="14" color="success" class="mr-1">mdi-check</v-icon>
          Order saved
        </span>
      </Transition>
    </div>

    <!-- Drag hint — only show when filter is 'all' and no search -->
    <p v-if="isDraggable" class="drag-hint">
      <v-icon size="13" class="mr-1">mdi-drag</v-icon>
      Drag rows to reorder. Order is saved automatically.
    </p>

    <div v-if="loading" class="loading-state">
      <v-progress-circular indeterminate color="secondary" />
    </div>

    <template v-else>
      <!-- ── Featured table ── -->
      <div class="section-header section-header--featured">
        <div class="section-header__left">
          <v-icon size="13" color="yellow-darken-1" class="mr-1">mdi-star</v-icon>
          <span class="section-label section-label--featured">Featured</span>
          <span class="section-count section-count--featured">{{ featuredProjects.length }}</span>
        </div>
        <span class="section-hint">Always shown first — drag to reorder within this group</span>
      </div>

      <v-data-table :headers="activeHeaders" :items="featuredProjects" item-value="projectId"
        class="admin-table admin-table--featured mb-6" hover hide-default-footer :items-per-page="-1">
        <template #item="{ item }">
          <tr :class="{
            'drag-over': dragOverId === item.projectId,
            'dragging': draggingId === item.projectId,
          }" @dragover.prevent="onDragOver(item)" @dragleave="onDragLeave" @drop.prevent="onDrop(item)">
            <td v-if="isDraggable" class="drag-cell" draggable="true" @dragstart="onDragStart($event, item)"
              @dragend="onDragEnd">
              <v-icon size="18" class="drag-handle">mdi-drag-vertical</v-icon>
            </td>
            <td>{{ item.title }}</td>
            <td>
              <span class="text-body-2">
                {{item.workExperienceIds?.length
                  ? item.workExperienceIds.map(id => works.find(w => w.workExperienceId ===
                    id)?.employer).filter(Boolean).join(', ')
                  : '—'}}
              </span>
            </td>
            <td>{{ item.role }}</td>
            <td>{{ item.displayOrder }}</td>
            <td><v-icon color="yellow" size="18">mdi-star</v-icon></td>
            <td><v-chip :color="item.isActive ? 'success' : 'error'" size="x-small" variant="tonal">{{ item.isActive ?
              'Active' : 'Inactive' }}</v-chip></td>
            <td><v-icon :color="item.hasEmbedding ? 'success' : 'warning'" size="18">{{ item.hasEmbedding ? 'mdi-brain'
              : 'mdi-brain-off' }}</v-icon></td>
            <td>
              <div class="chip-wrap">
                <v-chip v-for="t in item.techStack.slice(0, 3)" :key="t" size="x-small" variant="tonal"
                  color="secondary" class="mr-1">{{ t }}</v-chip>
                <span v-if="item.techStack.length > 3" class="text-caption text-medium-emphasis">+{{
                  item.techStack.length - 3 }}</span>
              </div>
            </td>
            <td>
              <div class="row-actions">
                <v-btn icon size="x-small" variant="text" @click="openDialog(item)"
                  title="Edit"><v-icon>mdi-pencil</v-icon></v-btn>
                <v-btn icon size="x-small" variant="text" color="secondary"
                  :loading="embeddingItemLoading === item.projectId"
                  @click="regenerateEmbedding(item.projectId)"><v-icon>mdi-refresh</v-icon></v-btn>
                <v-btn icon size="x-small" variant="text" color="error" @click="toggleActive(item)"
                  :title="item.isActive ? 'Deactivate' : 'Restore'"><v-icon>{{ item.isActive ? 'mdi-eye-off' : 'mdi-eye'
                  }}</v-icon></v-btn>
              </div>
            </td>
          </tr>
        </template>
        <template #no-data>
          <div class="empty-state">No featured projects yet — mark a project as featured to pin it here.</div>
        </template>
      </v-data-table>

      <!-- ── Non-featured table ── -->
      <div class="section-header section-header--standard">
        <div class="section-header__left">
          <v-icon size="13" class="mr-1"
            style="color: rgba(var(--v-theme-on-surface), 0.35)">mdi-view-grid-outline</v-icon>
          <span class="section-label section-label--standard">Other Projects</span>
          <span class="section-count section-count--standard">{{ standardProjects.length }}</span>
        </div>
        <span class="section-hint">Drag to reorder within this group</span>
      </div>

      <v-data-table :headers="activeHeaders" :items="standardProjects" :search="search" item-value="projectId"
        class="admin-table" hover :items-per-page="15">
        <template #item="{ item }">
          <tr :class="{
            'drag-over': dragOverId === item.projectId,
            'dragging': draggingId === item.projectId,
          }" @dragover.prevent="onDragOver(item)" @dragleave="onDragLeave" @drop.prevent="onDrop(item)">
            <td v-if="isDraggable" class="drag-cell" draggable="true" @dragstart="onDragStart($event, item)"
              @dragend="onDragEnd">
              <v-icon size="18" class="drag-handle">mdi-drag-vertical</v-icon>
            </td>
            <td>{{ item.title }}</td>
            <td><span class="text-body-2">{{works.find(w => w.workExperienceId === item.workExperienceId)?.employer ??
              '—'}}</span></td>
            <td>{{ item.role }}</td>
            <td>{{ item.displayOrder }}</td>
            <td><v-icon color="surface-variant" size="18">mdi-star-outline</v-icon></td>
            <td><v-chip :color="item.isActive ? 'success' : 'error'" size="x-small" variant="tonal">{{ item.isActive ?
              'Active' : 'Inactive' }}</v-chip></td>
            <td><v-icon :color="item.hasEmbedding ? 'success' : 'warning'" size="18">{{ item.hasEmbedding ? 'mdi-brain'
              : 'mdi-brain-off' }}</v-icon></td>
            <td>
              <div class="chip-wrap">
                <v-chip v-for="t in item.techStack.slice(0, 3)" :key="t" size="x-small" variant="tonal"
                  color="secondary" class="mr-1">{{ t }}</v-chip>
                <span v-if="item.techStack.length > 3" class="text-caption text-medium-emphasis">+{{
                  item.techStack.length - 3 }}</span>
              </div>
            </td>
            <td>
              <div class="row-actions">
                <v-btn icon size="x-small" variant="text" @click="openDialog(item)"
                  title="Edit"><v-icon>mdi-pencil</v-icon></v-btn>
                <v-btn icon size="x-small" variant="text" color="secondary"
                  :loading="embeddingItemLoading === item.projectId"
                  @click="regenerateEmbedding(item.projectId)"><v-icon>mdi-refresh</v-icon></v-btn>
                <v-btn icon size="x-small" variant="text" :color="item.isActive ? 'error' : 'success'"
                  @click="toggleActive(item)" :title="item.isActive ? 'Deactivate' : 'Restore'"><v-icon>{{ item.isActive
                    ? 'mdi-eye-off' : 'mdi-eye' }}</v-icon></v-btn>
              </div>
            </td>
          </tr>
        </template>
      </v-data-table>
    </template>

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
              <v-text-field v-model="form.title" label="Title *" variant="outlined" density="comfortable"
                color="secondary" />
            </v-col>
            <v-col cols="12" md="6">
              <v-autocomplete v-model="form.workExperienceIds" :items="workOptions" item-title="title"
                item-value="value" label="Work Experience (optional)" variant="outlined" density="comfortable"
                color="secondary" multiple chips closable-chips clearable>
                <template #item="{ props, item }">
                  <v-list-item v-bind="props" :subtitle="item.raw.subtitle" />
                </template>
                <template #prepend-inner>
                  <v-icon :color="form.workExperienceIds?.length ? 'secondary' : 'surface-variant'" size="18"
                    class="mr-1">mdi-briefcase-outline</v-icon>
                </template>
              </v-autocomplete>
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field v-model="form.role" label="Role *" variant="outlined" density="comfortable"
                color="secondary" />
            </v-col>
            <v-col cols="6" md="3">
              <v-text-field v-model="form.startYear" label="Start Year" variant="outlined" density="comfortable"
                color="secondary" />
            </v-col>
            <v-col cols="6" md="3">
              <v-text-field v-model="form.endYear" label="End Year" variant="outlined" density="comfortable"
                color="secondary" placeholder="Present" />
            </v-col>
            <v-col cols="6" md="3">
              <v-text-field v-model.number="form.displayOrder" label="Display Order" type="number" variant="outlined"
                density="comfortable" color="secondary" />
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
              <v-textarea v-model="form.summary" label="Summary *" variant="outlined" density="comfortable" rows="4"
                color="secondary" auto-grow />
            </v-col>
            <v-col cols="12">
              <v-textarea v-model="form.detail" label="Detail (optional)" variant="outlined" density="comfortable"
                rows="4" color="secondary" auto-grow />
            </v-col>
            <v-col cols="12">
              <v-textarea v-model="form.impactStatement" label="Impact Statement (optional)" variant="outlined"
                density="comfortable" rows="2" color="secondary" auto-grow
                hint="A single punchy sentence quantifying the outcome" persistent-hint />
            </v-col>
            <v-col cols="12">
              <label class="field-label">Tech Stack</label>
              <p class="field-hint mb-2">Press Enter or comma after each item.</p>
              <div class="tag-input-wrap">
                <v-chip v-for="(t, i) in form.techStack" :key="t" size="small" closable class="mr-1 mb-1"
                  @click:close="form.techStack.splice(i, 1)">{{ t }}</v-chip>
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

// ── Drag state ────────────────────────────────────────────
const draggingId = ref(null)
const draggingItem = ref(null)
const dragOverId = ref(null)
const reorderSaving = ref(false)
const reorderSaved = ref(false)
let savedTimeout = null

// Drag only active when showing all projects unfiltered
const isDraggable = computed(() => filter.value === 'all' && !search.value)

const emptyForm = () => ({
  title: '', workExperienceIds: [], role: '', summary: '', detail: '',
  impactStatement: '', techStack: [], displayOrder: 0, isFeatured: false,
  isActive: true, startYear: '', endYear: ''
})
const form = ref(emptyForm())

const baseHeaders = [
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

const dragHeader = { title: '', key: 'drag', sortable: false, width: 40 }

const activeHeaders = computed(() =>
  isDraggable.value ? [dragHeader, ...baseHeaders] : baseHeaders
)

const workOptions = computed(() => works.value.map(w => ({
  title: `${w.employer} — ${w.title}`,
  value: w.workExperienceId,
  subtitle: `${w.startYear ?? '?'}${w.endYear ? '–' + w.endYear : '–Present'}`,
})))

const featuredProjects = computed(() => {
  let list = projects.value.filter(p => p.isFeatured).sort((a, b) => a.displayOrder - b.displayOrder)
  if (filter.value === 'inactive') return []
  if (filter.value === 'active') list = list.filter(p => p.isActive)
  return list
})

const standardProjects = computed(() => {
  let list = projects.value.filter(p => !p.isFeatured).sort((a, b) => a.displayOrder - b.displayOrder)
  if (filter.value === 'featured') return []
  if (filter.value === 'active') list = list.filter(p => p.isActive)
  if (filter.value === 'inactive') list = list.filter(p => !p.isActive)
  return list
})

// Keep filteredProjects for drag operations that need the full ordered list
const filteredProjects = computed(() => [...featuredProjects.value, ...standardProjects.value])

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

// ── Drag handlers ─────────────────────────────────────────
function onDragStart(event, item) {
  draggingId.value = item.projectId
  draggingItem.value = item
  event.dataTransfer.effectAllowed = 'move'
  // Invisible ghost so the row highlight is the only visual feedback
  const ghost = document.createElement('div')
  ghost.style.cssText = 'position:absolute;top:-9999px;width:1px;height:1px'
  document.body.appendChild(ghost)
  event.dataTransfer.setDragImage(ghost, 0, 0)
  setTimeout(() => document.body.removeChild(ghost), 0)
}

function onDragOver(item) {
  if (!draggingItem.value || item.projectId === draggingId.value) return
  dragOverId.value = item.projectId
}

function onDragLeave() {
  dragOverId.value = null
}

function onDragEnd() {
  draggingId.value = null
  draggingItem.value = null
  dragOverId.value = null
}

async function onDrop(targetItem) {
  if (!draggingItem.value || draggingItem.value.projectId === targetItem.projectId) {
    onDragEnd()
    return
  }

  // Prevent dragging across the featured / non-featured boundary
  if (draggingItem.value.isFeatured !== targetItem.isFeatured) {
    onDragEnd()
    notify('Featured projects are always pinned to the top', 'warning')
    return
  }

  // Reorder within the correct group only
  const group = draggingItem.value.isFeatured ? [...featuredProjects.value] : [...standardProjects.value]
  const fromIndex = group.findIndex(p => p.projectId === draggingItem.value.projectId)
  const toIndex = group.findIndex(p => p.projectId === targetItem.projectId)
  const [moved] = group.splice(fromIndex, 1)
  group.splice(toIndex, 0, moved)

  // Reassign displayOrder sequentially within the group
  const updates = group.map((p, i) => ({ ...p, displayOrder: i + 1 }))

  // Optimistic update
  projects.value = projects.value.map(p => updates.find(u => u.projectId === p.projectId) ?? p)

  onDragEnd()

  reorderSaving.value = true
  reorderSaved.value = false
  clearTimeout(savedTimeout)

  try {
    await Promise.all(
      updates.map(p =>
        adminStore.apiFetch(`/admin/projects/${p.projectId}`, {
          method: 'PUT',
          body: JSON.stringify(p),
        })
      )
    )
    reorderSaved.value = true
    savedTimeout = setTimeout(() => { reorderSaved.value = false }, 2500)
  } catch {
    notify('Failed to save order', 'error')
    await load()
  } finally {
    reorderSaving.value = false
  }
}

// ── CRUD ──────────────────────────────────────────────────
function openDialog(project = null) {
  editingProject.value = project
  form.value = project
    ? { ...project, techStack: [...project.techStack], workExperienceIds: [...(project.workExperienceIds ?? [])] }
    : emptyForm()
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
.tab-toolbar {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 0.75rem;
  flex-wrap: wrap;
}

.toolbar-search {
  max-width: 280px;
}

.loading-state {
  display: flex;
  justify-content: center;
  padding: 4rem;
}

.drag-hint {
  font-size: 0.72rem;
  color: rgba(var(--v-theme-on-surface), 0.35);
  margin-bottom: 0.75rem;
  display: flex;
  align-items: center;
}

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

/* ── Drag handle ── */
.drag-cell {
  width: 40px;
  padding: 0 8px !important;
  cursor: grab;
  user-select: none;
}

.drag-cell:active {
  cursor: grabbing;
}

.drag-handle {
  color: rgba(var(--v-theme-on-surface), 0.2) !important;
  transition: color 0.15s ease;
}

tr:hover .drag-handle {
  color: rgba(var(--v-theme-secondary), 0.6) !important;
}

/* ── Drag row states ── */
tr.drag-over td {
  background: rgba(var(--v-theme-secondary), 0.08) !important;
  border-top: 2px solid rgb(var(--v-theme-secondary)) !important;
}

tr.dragging {
  opacity: 0.35;
}

/* ── Reorder status ── */
.reorder-saving,
.reorder-saved {
  display: flex;
  align-items: center;
  font-size: 0.75rem;
  font-family: 'Raleway', sans-serif;
}

.reorder-saving {
  color: rgba(var(--v-theme-secondary), 0.7);
}

.reorder-saved {
  color: rgb(var(--v-theme-success));
}

.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.3s ease;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

/* ── Dialog ── */
.dialog-card {
  background: rgb(var(--v-theme-surface));
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

.tag-input-wrap {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 4px;
  border: 1px solid rgba(var(--v-theme-secondary), 0.3);
  border-radius: 8px;
  padding: 0.5rem 0.75rem;
  min-height: 44px;
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

/* ── Section headers ── */
.section-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.5rem 0.75rem;
  border-radius: 8px 8px 0 0;
  margin-bottom: -1px;
  border: 1px solid transparent;
}

.section-header__left {
  display: flex;
  align-items: center;
  gap: 0.4rem;
}

.section-header--featured {
  background: linear-gradient(90deg, rgba(255, 196, 0, 0.07) 0%, transparent 70%);
  border-color: rgba(255, 196, 0, 0.18);
  border-bottom: none;
}

.section-header--standard {
  background: rgba(var(--v-theme-on-surface), 0.02);
  border-color: rgba(255, 255, 255, 0.07);
  border-bottom: none;
}

.section-label {
  font-size: 0.72rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.1em;
}

.section-label--featured {
  color: rgba(255, 196, 0, 0.8);
}

.section-label--standard {
  color: rgba(var(--v-theme-on-surface), 0.35);
}

.section-count {
  font-size: 0.65rem;
  font-weight: 700;
  padding: 0.1rem 0.45rem;
  border-radius: 10px;
  line-height: 1.6;
}

.section-count--featured {
  background: rgba(255, 196, 0, 0.12);
  color: rgba(255, 196, 0, 0.75);
}

.section-count--standard {
  background: rgba(var(--v-theme-on-surface), 0.06);
  color: rgba(var(--v-theme-on-surface), 0.3);
}

.section-hint {
  font-size: 0.68rem;
  color: rgba(var(--v-theme-on-surface), 0.25);
  font-style: italic;
}

.admin-table--featured {
  border-color: rgba(255, 196, 0, 0.18) !important;
  border-radius: 0 0 12px 12px !important;
}

.empty-state {
  padding: 1.5rem;
  text-align: center;
  font-size: 0.825rem;
  color: rgba(var(--v-theme-on-surface), 0.35);
  font-style: italic;
}

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