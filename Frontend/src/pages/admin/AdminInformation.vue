<template>
  <div>
    <div class="tab-toolbar">
      <v-btn color="secondary" variant="tonal" @click="openDialog()">
        <v-icon start>mdi-plus</v-icon>New Entry
      </v-btn>
      <v-text-field v-model="search" placeholder="Search information..." prepend-inner-icon="mdi-magnify"
        variant="outlined" density="compact" hide-details class="toolbar-search" clearable />
    </div>

    <div v-if="loading" class="loading-state">
      <v-progress-circular indeterminate color="secondary" />
    </div>

    <div v-else class="info-grid">
      <v-card v-for="item in filteredInfo" :key="item.informationId" class="info-card" elevation="0">
        <div class="info-card__body">
          <p class="info-card__text">{{ item.text || '(no text)' }}</p>
          <div class="info-card__keywords">
            <v-chip v-for="kw in item.keywords" :key="kw" size="x-small" variant="tonal" color="secondary"
              closable @click:close="deleteKeyword(item, kw)" class="mr-1 mb-1">
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
          <v-chip :color="item.hasEmbedding ? 'success' : 'warning'" size="x-small" variant="tonal" class="mr-auto">
            <v-icon start size="10">{{ item.hasEmbedding ? 'mdi-brain' : 'mdi-brain-off' }}</v-icon>
            {{ item.hasEmbedding ? 'Embedded' : 'No embedding' }}
          </v-chip>
          <v-btn size="x-small" variant="text" color="secondary"
            :loading="embeddingItemLoading === item.informationId"
            @click="regenerateEmbedding(item.informationId)">
            <v-icon start size="14">mdi-refresh</v-icon>Sync
          </v-btn>
          <v-btn size="x-small" variant="text" color="secondary" @click="openDialog(item)">
            <v-icon start size="14">mdi-pencil</v-icon>Edit
          </v-btn>
          <v-btn size="x-small" variant="text" color="error" @click="deleteInfo(item)">
            <v-icon start size="14">mdi-delete</v-icon>Delete
          </v-btn>
        </div>
      </v-card>
    </div>

    <!-- Create / Edit dialog -->
    <v-dialog v-model="dialogOpen" max-width="600" scrollable>
      <v-card class="dialog-card">
        <v-card-title class="dialog-title">
          {{ editingInfo ? 'Edit Information' : 'New Information Entry' }}
          <v-btn icon variant="text" size="small" @click="dialogOpen = false" class="ml-auto">
            <v-icon>mdi-close</v-icon>
          </v-btn>
        </v-card-title>
        <v-divider />
        <v-card-text class="dialog-body">
          <p class="field-hint mb-3">This text is used by SamuelLM's RAG pipeline to answer questions about Samuel.</p>
          <v-textarea v-model="form.text" label="Information Text" variant="outlined" rows="8" color="secondary" auto-grow />
          <label class="field-label mt-4 d-block">Keywords</label>
          <p class="field-hint mb-2">Keywords are used for fuzzy matching — auto-generated but editable.</p>
          <div class="tag-input-wrap">
            <v-chip v-for="(kw, i) in form.keywords" :key="kw" size="small" closable class="mr-1 mb-1"
              @click:close="form.keywords.splice(i, 1)">{{ kw }}</v-chip>
            <input v-model="kwInput" class="tag-input" placeholder="Add keyword, press Enter"
              @keydown.enter.prevent="addKeyword" @keydown.comma.prevent="addKeyword" />
          </div>
        </v-card-text>
        <v-divider />
        <v-card-actions class="dialog-actions">
          <v-spacer />
          <v-btn variant="text" @click="dialogOpen = false">Cancel</v-btn>
          <v-btn color="secondary" variant="tonal" :loading="saving" @click="save">
            {{ editingInfo ? 'Save Changes' : 'Create Entry' }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Quick add keyword dialog -->
    <v-dialog v-model="addKeywordDialog" max-width="400">
      <v-card class="dialog-card">
        <v-card-title class="dialog-title">Add Keyword</v-card-title>
        <v-card-text>
          <v-text-field v-model="quickKw" label="Keyword" variant="outlined" density="compact"
            color="secondary" autofocus @keydown.enter="saveQuickKeyword" />
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
import { ref, computed, onMounted } from 'vue'
import { useAdminStore } from '@/stores/adminStore'

const adminStore = useAdminStore()

const information = ref([])
const loading = ref(false)
const saving = ref(false)
const search = ref('')
const dialogOpen = ref(false)
const editingInfo = ref(null)
const kwInput = ref('')
const addKeywordDialog = ref(false)
const quickKw = ref('')
const kwTargetItem = ref(null)
const embeddingItemLoading = ref(null)
const snackbar = ref({ show: false, text: '', color: 'success' })

const form = ref({ text: '', keywords: [] })

const filteredInfo = computed(() => {
  if (!search.value) return information.value
  const q = search.value.toLowerCase()
  return information.value.filter(i =>
    i.text?.toLowerCase().includes(q) || i.keywords.some(k => k.toLowerCase().includes(q))
  )
})

function notify(text, color = 'success') { snackbar.value = { show: true, text, color } }

async function load() {
  loading.value = true
  try {
    const res = await adminStore.apiFetch('/admin/information')
    if (res.ok) information.value = await res.json()
  } catch { notify('Failed to load information', 'error') }
  finally { loading.value = false }
}

function openDialog(item = null) {
  editingInfo.value = item
  form.value = item ? { text: item.text ?? '', keywords: [...item.keywords] } : { text: '', keywords: [] }
  kwInput.value = ''
  dialogOpen.value = true
}

function addKeyword() {
  const val = kwInput.value.trim().replace(/,$/, '')
  if (val && !form.value.keywords.includes(val)) form.value.keywords.push(val)
  kwInput.value = ''
}

async function save() {
  saving.value = true
  try {
    const isEdit = !!editingInfo.value
    const res = await adminStore.apiFetch(
      isEdit ? `/admin/information/${editingInfo.value.informationId}` : '/admin/information',
      { method: isEdit ? 'PUT' : 'POST', body: JSON.stringify(form.value) }
    )
    if (!res.ok) throw new Error()
    await load()
    dialogOpen.value = false
    notify(isEdit ? 'Entry updated' : 'Entry created')
  } catch { notify('Save failed', 'error') }
  finally { saving.value = false }
}

async function deleteInfo(item) {
  if (!confirm('Delete this information entry? This cannot be undone.')) return
  try {
    const res = await adminStore.apiFetch(`/admin/information/${item.informationId}`, { method: 'DELETE' })
    if (!res.ok) throw new Error()
    await load()
    notify('Entry deleted')
  } catch { notify('Delete failed', 'error') }
}

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
    await load()
    addKeywordDialog.value = false
    notify('Keyword added')
  } catch { notify('Failed to add keyword', 'error') }
  finally { saving.value = false }
}

async function deleteKeyword(item, keyword) {
  try {
    const updated = { text: item.text, keywords: item.keywords.filter(k => k !== keyword) }
    const res = await adminStore.apiFetch(`/admin/information/${item.informationId}`, {
      method: 'PUT', body: JSON.stringify(updated)
    })
    if (!res.ok) throw new Error()
    await load()
    notify('Keyword removed')
  } catch { notify('Failed to remove keyword', 'error') }
}

async function regenerateEmbedding(id) {
  embeddingItemLoading.value = id
  try {
    const res = await adminStore.apiFetch(`/admin/generate-embeddings/information/${id}`, { method: 'POST' })
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

.info-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(340px, 1fr)); gap: 1rem; }
@media (max-width: 599px) { .info-grid { grid-template-columns: 1fr; } }

.info-card { background: rgb(var(--v-theme-surface)) !important; border: 1px solid rgba(255,255,255,0.08); border-radius: 12px !important; display: flex; flex-direction: column; }
.info-card__body { padding: 1rem 1rem 0.75rem; flex: 1; }
.info-card__text { font-size: 0.875rem; line-height: 1.65; color: rgba(var(--v-theme-on-surface), 0.85); margin: 0 0 0.75rem; display: -webkit-box; -webkit-line-clamp: 4; -webkit-box-orient: vertical; overflow: hidden; }
.info-card__keywords { display: flex; flex-wrap: wrap; }
.info-card__actions { display: flex; gap: 0.25rem; padding: 0.4rem 0.5rem; }
.add-keyword-chip { cursor: pointer; }

.dialog-card { background: rgb(var(--v-theme-surface)); }
.dialog-title { display: flex; align-items: center; font-family: 'Patua One', serif; font-size: 1.1rem; padding: 1rem 1.25rem !important; }
.dialog-body { padding: 1.25rem !important; }
.dialog-actions { padding: 0.75rem 1rem !important; }

.field-label { font-size: 0.8rem; font-weight: 600; text-transform: uppercase; letter-spacing: 0.04em; color: rgba(var(--v-theme-on-surface), 0.55); }
.field-hint { font-size: 0.78rem; color: rgba(var(--v-theme-on-surface), 0.45); margin: 0.2rem 0 0; line-height: 1.5; }

.tag-input-wrap { display: flex; flex-wrap: wrap; align-items: center; gap: 4px; border: 1px solid rgba(var(--v-theme-secondary), 0.3); border-radius: 8px; padding: 0.5rem 0.75rem; min-height: 44px; }
.tag-input { border: none; outline: none; background: transparent; color: rgb(var(--v-theme-on-surface)); font-family: 'Raleway', sans-serif; font-size: 0.875rem; flex: 1; min-width: 120px; }
</style>