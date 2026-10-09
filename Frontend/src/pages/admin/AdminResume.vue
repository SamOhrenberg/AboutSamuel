<template>
  <div>
    <!-- ── Official resume ── -->
    <section class="block">
      <h2 class="block-title">Official resume</h2>

      <div v-if="loadingVersions" class="loading-state loading-state--small"><v-progress-circular indeterminate color="secondary" size="24" /></div>

      <div v-else class="current-card">
        <template v-if="current">
          <v-icon size="34" color="error">mdi-file-pdf-box</v-icon>
          <div class="current-card__meta">
            <span class="current-card__name">{{ current.fileName }}</span>
            <span class="muted">{{ formatSize(current.sizeBytes) }} · uploaded {{ formatDateTime(current.uploadedAt) }}</span>
          </div>
        </template>
        <span v-else class="muted">No resume uploaded yet.</span>

        <div class="current-card__actions">
          <v-btn v-if="current" :href="officialUrl" target="_blank" rel="noopener" variant="text" size="small">
            <v-icon start>mdi-open-in-new</v-icon>View
          </v-btn>
          <v-btn variant="tonal" color="secondary" size="small" :loading="uploading" @click="fileInput.click()">
            <v-icon start>mdi-upload</v-icon>Upload new version
          </v-btn>
          <input ref="fileInput" type="file" accept="application/pdf,.pdf" class="sr-only" @change="onFileChosen" />
        </div>
      </div>
      <p class="hint">Uploading replaces the resume used for the site download, the chatbot, and recruiter replies right away. Analyzing is separate and optional.</p>

      <v-expansion-panels v-if="versions.length > 1" variant="accordion" class="mt-3">
        <v-expansion-panel title="Version history">
          <v-expansion-panel-text>
            <div v-for="v in versions" :key="v.resumeFileId" class="version-row">
              <span class="version-row__name">{{ v.fileName }}</span>
              <span class="muted">{{ formatSize(v.sizeBytes) }} · {{ formatDateTime(v.uploadedAt) }}</span>
              <v-chip v-if="v.isCurrent" size="x-small" color="success" label>Current</v-chip>
              <v-chip v-else-if="v.analyzed" size="x-small" label>Analyzed</v-chip>
              <v-btn v-if="!v.isCurrent" size="x-small" variant="text" :loading="activating === v.resumeFileId"
                @click="activate(v)">Make current</v-btn>
            </div>
          </v-expansion-panel-text>
        </v-expansion-panel>
      </v-expansion-panels>
    </section>

    <!-- ── Analysis ── -->
    <section class="block">
      <div class="block-head">
        <h2 class="block-title">Suggested site changes</h2>
        <v-btn color="secondary" variant="tonal" size="small" :disabled="!current || running" :loading="running"
          @click="analyze">
          <v-icon start>mdi-creation</v-icon>Analyze resume
        </v-btn>
      </div>
      <p class="hint">Compares the current resume with your work experience, projects, and information. Nothing changes until you approve a suggestion.</p>

      <ol v-if="steps.length" class="steps" aria-live="polite">
        <li v-for="s in steps" :key="s.key" :class="`step step--${s.state}`">
          <v-progress-circular v-if="s.state === 'active'" indeterminate size="14" width="2" color="secondary" />
          <v-icon v-else size="16">{{ s.state === 'done' ? 'mdi-check-circle' : 'mdi-circle-outline' }}</v-icon>
          {{ s.label }}
        </li>
      </ol>
      <v-alert v-if="analysisError" type="error" variant="tonal" density="compact" class="mb-3">{{ analysisError }}</v-alert>

      <div v-if="analyses.length" class="analysis-bar">
        <v-select v-model="selectedId" :items="analysisItems" item-title="title" item-value="value" density="compact"
          variant="outlined" hide-details label="Analysis" class="analysis-select" />
        <v-chip-group v-model="filter" mandatory selected-class="text-secondary">
          <v-chip value="pending" size="small" filter>Pending<span class="chip-count">{{ counts.pending }}</span></v-chip>
          <v-chip value="all" size="small" filter>All<span class="chip-count">{{ suggestions.length }}</span></v-chip>
        </v-chip-group>
      </div>

      <div v-if="loadingDetail" class="loading-state loading-state--small"><v-progress-circular indeterminate color="secondary" size="24" /></div>

      <template v-else-if="detail">
        <p v-if="detail.analysis.status === 'failed'" class="hint">This run failed: {{ detail.analysis.error }}</p>
        <p v-else class="hint">{{ detail.analysis.summary }} Model: {{ detail.analysis.model }}.</p>

        <div v-if="!shown.length" class="empty-state">
          {{ suggestions.length ? 'No pending suggestions.' : 'Nothing to suggest.' }}
        </div>

        <v-expansion-panels v-model="openId" variant="accordion" class="suggestion-panels">
          <v-expansion-panel v-for="s in shown" :key="s.resumeSuggestionId" :value="s.resumeSuggestionId" class="suggestion-panel">
            <v-expansion-panel-title>
              <div class="suggestion-head">
                <div class="suggestion-head__main">
                  <span class="suggestion-title">{{ s.label }}</span>
                  <span class="muted">{{ ENTITY[s.entityType] }} · {{ fieldSummary(s) }}</span>
                </div>
                <div class="suggestion-head__side">
                  <v-chip size="x-small" label :color="s.action === 'add' ? 'info' : 'secondary'">{{ s.action === 'add' ? 'Add' : 'Update' }}</v-chip>
                  <v-chip v-if="s.status !== 'pending'" size="x-small" label :color="s.status === 'applied' ? 'success' : 'default'">{{ s.status }}</v-chip>
                </div>
              </div>
            </v-expansion-panel-title>

            <v-expansion-panel-text>
              <p class="rationale">{{ s.rationale }}</p>
              <blockquote class="evidence"><span class="evidence__label">From the resume</span>{{ s.evidence }}</blockquote>

              <div v-for="(change, field) in s.changes" :key="field" class="change">
                <div class="change__field">{{ FIELD[field] ?? field }}</div>

                <!-- Editing -->
                <template v-if="editingId === s.resumeSuggestionId">
                  <v-textarea v-if="isList(change.to)" v-model="drafts[field]" auto-grow rows="3" variant="outlined"
                    density="compact" hide-details />
                  <v-textarea v-else-if="isLong(field)" v-model="drafts[field]" auto-grow rows="3" variant="outlined"
                    density="compact" hide-details />
                  <v-text-field v-else v-model="drafts[field]" variant="outlined" density="compact" hide-details />
                  <p v-if="isList(change.to)" class="field-hint">One per line.</p>
                </template>

                <!-- Diff -->
                <template v-else>
                  <template v-if="isList(change.to)">
                    <ul class="diff-list">
                      <li v-for="item in listDiff(change).added" :key="'a' + item" class="diff-add">+ {{ item }}</li>
                      <li v-for="item in listDiff(change).removed" :key="'r' + item" class="diff-remove">− {{ item }}</li>
                    </ul>
                    <p v-if="listDiff(change).kept" class="field-hint">{{ listDiff(change).kept }} existing {{ listDiff(change).kept === 1 ? 'entry stays' : 'entries stay' }} as they are.</p>
                  </template>
                  <template v-else>
                    <p v-if="change.from !== null && change.from !== ''" class="diff-remove diff-text">{{ change.from }}</p>
                    <p class="diff-add diff-text">{{ change.to ?? '(none)' }}</p>
                  </template>
                </template>
              </div>

              <div v-if="s.status === 'pending'" class="suggestion-actions">
                <template v-if="editingId === s.resumeSuggestionId">
                  <v-btn variant="text" size="small" @click="stopEditing">Cancel edit</v-btn>
                </template>
                <v-btn v-else variant="text" size="small" @click="startEditing(s)"><v-icon start>mdi-pencil</v-icon>Edit</v-btn>
                <v-btn variant="tonal" color="error" size="small" :loading="busy === s.resumeSuggestionId + 'reject'"
                  :disabled="!!busy" @click="reject(s)">Reject</v-btn>
                <v-btn variant="flat" color="secondary" size="small" :loading="busy === s.resumeSuggestionId + 'approve'"
                  :disabled="!!busy" @click="approve(s)">
                  {{ editingId === s.resumeSuggestionId ? 'Approve with edits' : 'Approve' }}
                </v-btn>
              </div>
            </v-expansion-panel-text>
          </v-expansion-panel>
        </v-expansion-panels>
      </template>

      <div v-else-if="!loadingDetail && !running" class="empty-state">No analysis has been run yet.</div>
    </section>

    <v-snackbar v-model="snackbar.show" :color="snackbar.color" :timeout="4000" location="bottom right">
      {{ snackbar.text }}
    </v-snackbar>
  </div>
</template>

<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { useAdminStore } from '@/stores/adminStore'
import { formatDateTime } from '@/utilities/dateUtils'

const API_BASE = import.meta.env.VITE_API_URL
const officialUrl = `${API_BASE}/resume/official`

const ENTITY = { WorkExperience: 'Work experience', Project: 'Project', Information: 'Information' }
const FIELD = {
  employer: 'Employer', title: 'Title', startYear: 'Start year', endYear: 'End year', summary: 'Summary',
  achievements: 'Achievements', role: 'Role', detail: 'Detail', impactStatement: 'Impact statement',
  techStack: 'Tech stack', text: 'Text', keywords: 'Keywords',
}
const LONG_FIELDS = ['summary', 'detail', 'impactStatement', 'text']
const STEP_LABELS = {
  load_resume: 'Reading the resume PDF',
  load_site_data: 'Loading the site data',
  compare: 'Comparing work, projects, and information',
  validate: 'Checking every suggestion against the resume',
}

const adminStore = useAdminStore()
const snackbar = ref({ show: false, text: '', color: 'success' })
const notify = (text, color = 'success') => { snackbar.value = { show: true, text, color } }

// ── Versions ──
const versions = ref([])
const loadingVersions = ref(false)
const uploading = ref(false)
const activating = ref(null)
const fileInput = ref(null)
const current = computed(() => versions.value.find(v => v.isCurrent) ?? null)

async function loadVersions() {
  loadingVersions.value = true
  try {
    const res = await adminStore.apiFetch('/admin/resume')
    if (res.ok) versions.value = await res.json()
  } catch { notify('Failed to load resume versions', 'error') }
  finally { loadingVersions.value = false }
}

async function onFileChosen(event) {
  const file = event.target.files?.[0]
  event.target.value = ''  // so choosing the same file again still fires
  if (!file) return
  uploading.value = true
  try {
    const body = new FormData()
    body.append('file', file)
    const res = await adminStore.apiFetch('/admin/resume', { method: 'POST', body })
    if (!res.ok) { notify((await res.text()).replace(/^"|"$/g, '') || 'Upload failed', 'error'); return }
    notify('Resume updated')
    await loadVersions()
  } catch { notify('Upload failed', 'error') }
  finally { uploading.value = false }
}

async function activate(v) {
  activating.value = v.resumeFileId
  try {
    const res = await adminStore.apiFetch(`/admin/resume/${v.resumeFileId}/activate`, { method: 'POST' })
    if (!res.ok) throw new Error()
    notify('That version is now the official resume')
    await loadVersions()
  } catch { notify('Failed to switch versions', 'error') }
  finally { activating.value = null }
}

// ── Analyses ──
const analyses = ref([])
const selectedId = ref(null)
const detail = ref(null)
const loadingDetail = ref(false)
const filter = ref('pending')
const openId = ref(null)

const suggestions = computed(() => detail.value?.suggestions ?? [])
const counts = computed(() => ({ pending: suggestions.value.filter(s => s.status === 'pending').length }))
const shown = computed(() => filter.value === 'pending' ? suggestions.value.filter(s => s.status === 'pending') : suggestions.value)
const analysisItems = computed(() => analyses.value.map(a => ({
  value: a.resumeAnalysisId,
  title: `${formatDateTime(a.startedAt)} · ${a.status === 'completed' ? `${a.pending} pending` : a.status}`,
})))

async function loadAnalyses(selectLatest = false) {
  try {
    const res = await adminStore.apiFetch('/admin/resume/analyses')
    if (!res.ok) return
    analyses.value = await res.json()
    if (selectLatest || !selectedId.value) selectedId.value = analyses.value[0]?.resumeAnalysisId ?? null
  } catch { notify('Failed to load analyses', 'error') }
}

async function loadDetail() {
  if (!selectedId.value) { detail.value = null; return }
  loadingDetail.value = true
  stopEditing()
  try {
    const res = await adminStore.apiFetch(`/admin/resume/analyses/${selectedId.value}`)
    if (res.ok) detail.value = await res.json()
  } catch { notify('Failed to load suggestions', 'error') }
  finally { loadingDetail.value = false }
}

watch(selectedId, loadDetail)

// ── Running an analysis ──
const running = ref(false)
const steps = ref([])
const analysisError = ref('')

async function analyze() {
  running.value = true
  analysisError.value = ''
  steps.value = Object.entries(STEP_LABELS).map(([key, label]) => ({ key, label, state: 'waiting' }))
  try {
    const res = await adminStore.apiFetch('/admin/resume/analyze', { method: 'POST', body: JSON.stringify({}) })
    if (!res.ok) throw new Error()
    const reader = res.body.getReader()
    const decoder = new TextDecoder()
    let buffer = ''
    while (true) {
      const { done, value } = await reader.read()
      if (done) break
      buffer += decoder.decode(value, { stream: true })
      const events = buffer.split('\n\n')
      buffer = events.pop() ?? ''
      for (const line of events) {
        if (!line.startsWith('data: ')) continue
        let ev
        try { ev = JSON.parse(line.slice(6)) } catch { continue }
        if (ev.step) {
          steps.value.forEach(s => {
            if (s.state === 'active') s.state = 'done'
            if (s.key === ev.step) s.state = 'active'
          })
        }
        if (ev.result) steps.value.forEach(s => { s.state = 'done' })
        if (ev.error) analysisError.value = ev.error
      }
    }
  } catch {
    analysisError.value = analysisError.value || 'The analysis was interrupted.'
  } finally {
    running.value = false
    await loadAnalyses(true)
    if (selectedId.value) await loadDetail()
    if (!analysisError.value) {
      filter.value = 'pending'
      setTimeout(() => { steps.value = [] }, 1500)
    }
  }
}

// ── Reviewing ──
const editingId = ref(null)
const drafts = ref({})
const busy = ref(null)
const isList = v => Array.isArray(v)
const isLong = f => LONG_FIELDS.includes(f)

function fieldSummary(s) {
  const fields = Object.keys(s.changes).map(f => (FIELD[f] ?? f).toLowerCase())
  return fields.length > 3 ? `${fields.slice(0, 3).join(', ')} +${fields.length - 3}` : fields.join(', ')
}

function listDiff(change) {
  const key = x => x.trim().toLowerCase()
  const from = change.from ?? [], to = change.to ?? []
  const fromKeys = new Set(from.map(key)), toKeys = new Set(to.map(key))
  return {
    added: to.filter(x => !fromKeys.has(key(x))),
    removed: from.filter(x => !toKeys.has(key(x))),
    kept: from.filter(x => toKeys.has(key(x))).length,
  }
}

function startEditing(s) {
  editingId.value = s.resumeSuggestionId
  drafts.value = Object.fromEntries(Object.entries(s.changes).map(([f, c]) =>
    [f, isList(c.to) ? c.to.join('\n') : (c.to ?? '')]))
}
function stopEditing() { editingId.value = null; drafts.value = {} }

// Only the fields whose edited value differs from what was proposed
function overridesFor(s) {
  if (editingId.value !== s.resumeSuggestionId) return null
  const overrides = {}
  for (const [field, change] of Object.entries(s.changes)) {
    const draft = drafts.value[field]
    if (isList(change.to)) {
      const list = String(draft ?? '').split('\n').map(x => x.trim()).filter(Boolean)
      if (JSON.stringify(list) !== JSON.stringify(change.to)) overrides[field] = list
    } else {
      const text = String(draft ?? '').trim()
      if (text !== (change.to ?? '')) overrides[field] = text === '' ? null : text
    }
  }
  return Object.keys(overrides).length ? overrides : null
}

async function decide(s, action, body) {
  busy.value = s.resumeSuggestionId + action
  try {
    const res = await adminStore.apiFetch(`/admin/resume/suggestions/${s.resumeSuggestionId}/${action}`, {
      method: 'POST', body: JSON.stringify(body ?? {}),
    })
    const data = res.headers.get('content-type')?.includes('json') ? await res.json() : null
    if (!res.ok) { notify(data?.message ?? 'That failed', 'error'); return false }
    return data ?? true
  } catch { notify('That failed', 'error'); return false }
  finally { busy.value = null }
}

async function approve(s) {
  const result = await decide(s, 'approve', { overrides: overridesFor(s) })
  if (!result) return
  s.status = 'applied'
  stopEditing()
  notify(result.embeddingGenerated
    ? 'Applied. Embedding rebuilt.'
    : 'Applied, but the embedding failed. Run Generate Embeddings.', result.embeddingGenerated ? 'success' : 'warning')
  loadAnalyses()
}

async function reject(s) {
  if (!(await decide(s, 'reject'))) return
  s.status = 'rejected'
  notify('Rejected')
  loadAnalyses()
}

// ── Formatting ──
function formatSize(bytes) {
  return bytes >= 1048576 ? `${(bytes / 1048576).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`
}

onMounted(async () => {
  await Promise.all([loadVersions(), loadAnalyses(true)])
  if (selectedId.value) await loadDetail()
})
</script>

<style scoped>
.block { margin-bottom: 2rem; }
.block-head { display: flex; align-items: center; justify-content: space-between; gap: 1rem; flex-wrap: wrap; }
.block-title { font-size: 0.78rem; font-weight: 700; text-transform: uppercase; letter-spacing: 0.08em; color: rgba(var(--v-theme-on-surface), 0.6); margin: 0 0 0.6rem; }
.hint { font-size: 0.78rem; color: rgba(var(--v-theme-on-surface), 0.55); margin: 0.4rem 0 0.8rem; }
.muted { font-size: 0.76rem; color: rgba(var(--v-theme-on-surface), 0.55); }
.loading-state { display: flex; justify-content: center; padding: 4rem; }
.loading-state--small { padding: 1rem; }
.empty-state { text-align: center; padding: 2.5rem; color: rgba(var(--v-theme-on-surface), 0.5); }
.sr-only { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); white-space: nowrap; }
.chip-count { margin-left: 0.4rem; font-weight: 700; opacity: .75; }

.current-card { display: flex; align-items: center; gap: 0.9rem; padding: 0.8rem 1rem; border-radius: 10px; background: rgb(var(--v-theme-surface)); border: 1px solid rgba(255,255,255,0.08); flex-wrap: wrap; }
.current-card__meta { display: flex; flex-direction: column; min-width: 0; flex: 1; }
.current-card__name { font-weight: 600; font-size: 0.9rem; overflow: hidden; text-overflow: ellipsis; }
.current-card__actions { display: flex; align-items: center; gap: 0.4rem; flex-wrap: wrap; }
.version-row { display: flex; align-items: center; gap: 0.75rem; padding: 0.35rem 0; font-size: 0.82rem; flex-wrap: wrap; }
.version-row__name { font-weight: 600; }

.steps { list-style: none; padding: 0; margin: 0 0 1rem; display: flex; flex-direction: column; gap: 0.35rem; }
.step { display: flex; align-items: center; gap: 0.5rem; font-size: 0.82rem; color: rgba(var(--v-theme-on-surface), 0.45); }
.step--active { color: rgb(var(--v-theme-on-surface)); font-weight: 600; }
.step--done { color: rgba(var(--v-theme-on-surface), 0.75); }
.step--done .v-icon { color: rgb(var(--v-theme-success)); }

.analysis-bar { display: flex; align-items: center; gap: 1rem; flex-wrap: wrap; margin-bottom: 0.75rem; }
.analysis-select { max-width: 340px; }

.suggestion-panels { border-radius: 12px !important; overflow: hidden; }
.suggestion-panel { background: rgb(var(--v-theme-surface)) !important; border-bottom: 1px solid rgba(255,255,255,0.06) !important; }
.suggestion-head { display: flex; align-items: center; justify-content: space-between; gap: 1rem; width: 100%; padding-right: 0.5rem; }
.suggestion-head__main { display: flex; flex-direction: column; gap: 0.15rem; min-width: 0; }
.suggestion-head__side { display: flex; gap: 0.35rem; flex-shrink: 0; }
.suggestion-title { font-size: 0.9rem; font-weight: 600; }

.rationale { font-size: 0.85rem; line-height: 1.55; margin: 0 0 0.75rem; }
.evidence { margin: 0 0 1rem; padding: 0.6rem 0.8rem; border-left: 3px solid rgba(var(--v-theme-secondary), 0.6); background: rgba(var(--v-theme-secondary), 0.06); font-size: 0.8rem; line-height: 1.55; border-radius: 0 6px 6px 0; }
.evidence__label { display: block; font-size: 0.66rem; font-weight: 700; text-transform: uppercase; letter-spacing: 0.06em; color: rgba(var(--v-theme-on-surface), 0.5); margin-bottom: 0.2rem; }

.change { margin-bottom: 0.9rem; }
.change__field { font-size: 0.7rem; font-weight: 700; text-transform: uppercase; letter-spacing: 0.06em; color: rgba(var(--v-theme-on-surface), 0.5); margin-bottom: 0.25rem; }
.diff-list { list-style: none; padding: 0; margin: 0; display: flex; flex-direction: column; gap: 0.3rem; }
.diff-text { margin: 0 0 0.25rem; white-space: pre-wrap; }
.diff-add, .diff-remove { font-size: 0.82rem; line-height: 1.5; padding: 0.25rem 0.55rem; border-radius: 6px; }
.diff-add { background: rgba(var(--v-theme-success), 0.12); border-left: 3px solid rgb(var(--v-theme-success)); }
.diff-remove { background: rgba(var(--v-theme-error), 0.1); border-left: 3px solid rgb(var(--v-theme-error)); text-decoration: line-through; text-decoration-color: rgba(var(--v-theme-error), 0.6); }
.field-hint { font-size: 0.72rem; color: rgba(var(--v-theme-on-surface), 0.5); margin: 0.3rem 0 0 0.2rem; }

.suggestion-actions { display: flex; justify-content: flex-end; gap: 0.5rem; flex-wrap: wrap; margin-top: 0.5rem; }

@media (max-width: 700px) {
  .suggestion-head__side { display: none; }
}
</style>
