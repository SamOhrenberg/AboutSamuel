<template>
  <div>
    <v-tabs v-model="view" color="secondary" density="compact" class="mb-4">
      <v-tab value="queue"><v-icon start>mdi-inbox-arrow-down</v-icon>Review queue</v-tab>
      <v-tab value="preferences"><v-icon start>mdi-tune</v-icon>Preferences</v-tab>
    </v-tabs>

    <!-- ── Queue ─────────────────────────────────────────────────────────── -->
    <div v-if="view === 'queue'">
      <div class="tab-toolbar">
        <v-chip-group v-model="filter" mandatory selected-class="filter-chip--active" @update:model-value="loadPostings">
          <v-chip v-for="f in FILTERS" :key="f.value" :value="f.value" size="small" variant="outlined" filter>
            {{ f.label }}<span v-if="countFor(f.value) !== null" class="chip-count">{{ countFor(f.value) }}</span>
          </v-chip>
        </v-chip-group>
        <v-spacer />
        <span class="sync-status">
          <v-icon size="14" :color="triageColor">mdi-circle</v-icon>
          {{ triageLabel }}<template v-if="stats?.lastSyncedAt"> · last run {{ formatDateTime(stats.lastSyncedAt) }}</template>
        </span>
        <v-btn variant="text" size="small" color="secondary" @click="refresh">
          <v-icon start>mdi-refresh</v-icon>Refresh
        </v-btn>
      </div>

      <div v-if="loading && !postings.length" class="loading-state">
        <v-progress-circular indeterminate color="secondary" />
      </div>
      <p v-else-if="!postings.length" class="empty-state">Nothing here.</p>

      <v-expansion-panels v-else v-model="openId" variant="accordion" class="postings-panels">
        <v-expansion-panel v-for="p in postings" :key="p.recruiterPostingId" :value="p.recruiterPostingId" class="posting-panel">
          <v-expansion-panel-title class="posting-header">
            <div class="posting-header__main">
              <div class="posting-title">
                <v-chip size="x-small" :color="STATUS[p.reviewStatus].color" variant="tonal" class="mr-2">
                  {{ STATUS[p.reviewStatus].label }}
                </v-chip>
                <span>{{ p.jobTitle || 'Untitled role' }}</span>
                <span class="posting-company">@ {{ p.hiringCompany || 'undisclosed client' }}</span>
                <v-chip v-if="p.engaged" size="x-small" color="success" variant="tonal" class="ml-2">
                  <v-icon start size="12">mdi-forum</v-icon>In conversation
                </v-chip>
              </div>
              <div class="posting-meta">
                {{ ARRANGEMENT[p.workArrangement] }}{{ p.location ? ` · ${p.location}` : '' }}
                · {{ EMPLOYMENT[p.employmentType] || p.employmentType }}{{ p.contractTerms !== 'unknown' ? ` (${p.contractTerms.toUpperCase()})` : '' }}
                · {{ p.annualPay ? `$${Math.round(p.annualPay).toLocaleString()}/yr` : 'pay not stated' }}
              </div>
            </div>
            <div class="posting-header__side">
              <v-chip size="x-small" :color="OUTCOME[p.outcome].color" variant="outlined">{{ OUTCOME[p.outcome].label }}</v-chip>
              <span class="posting-count">{{ p.emailCount }} email{{ p.emailCount === 1 ? '' : 's' }} · {{ p.agencies.length }} agenc{{ p.agencies.length === 1 ? 'y' : 'ies' }}</span>
            </div>
          </v-expansion-panel-title>

          <v-expansion-panel-text>
            <ul class="reason-list">
              <li v-for="r in p.reasons" :key="r">{{ r }}</li>
              <li v-if="p.missing.length" class="reason-missing">Would ask for: {{ p.missing.join(', ') }}</li>
              <li v-if="!p.canReply" class="reason-missing">Came through a platform notification (LinkedIn and similar), so it can't be answered by email.</li>
            </ul>

            <div class="review-row">
              <v-btn size="small" :variant="p.reviewStatus === 'interested' ? 'flat' : 'tonal'" color="success"
                :loading="saving === p.recruiterPostingId + 'interested'" @click="setStatus(p, 'interested')">
                <v-icon start>mdi-thumb-up-outline</v-icon>Interested
              </v-btn>
              <v-btn size="small" :variant="p.reviewStatus === 'passed' ? 'flat' : 'tonal'" color="error"
                :loading="saving === p.recruiterPostingId + 'passed'" @click="setStatus(p, 'passed')">
                <v-icon start>mdi-thumb-down-outline</v-icon>Pass
              </v-btn>
              <v-btn v-if="p.reviewStatus === 'interested' || p.reviewStatus === 'passed'" size="small" variant="text"
                :loading="saving === p.recruiterPostingId + 'pending'" @click="setStatus(p, 'pending')">
                Back to review
              </v-btn>
            </div>

            <v-textarea v-model="noteDrafts[p.recruiterPostingId]" label="Notes" rows="2" auto-grow density="compact"
              variant="outlined" hide-details class="mb-1" />
            <div class="notes-actions">
              <v-btn size="x-small" variant="text" color="secondary"
                :disabled="(noteDrafts[p.recruiterPostingId] ?? '') === (p.notes ?? '')"
                :loading="saving === p.recruiterPostingId + 'notes'" @click="saveNotes(p)">Save notes</v-btn>
            </div>

            <div v-if="!details[p.recruiterPostingId]" class="loading-state loading-state--small">
              <v-progress-circular indeterminate color="secondary" size="22" />
            </div>
            <div v-else class="pitch-list">
              <div class="pitch-list__title">Every email about this job</div>
              <div v-for="(x, i) in details[p.recruiterPostingId].pitches" :key="i" class="pitch-row">
                <span class="pitch-date">{{ formatDate(x.receivedAt) }}</span>
                <span class="pitch-agency">
                  {{ x.recruitingAgency || x.fromAddress }}
                  <span v-if="x.recruiterName" class="pitch-recruiter">{{ x.recruiterName }}</span>
                </span>
                <span class="pitch-pay">{{ x.annualPay ? `$${Math.round(x.annualPay).toLocaleString()}` : '-' }}</span>
                <span class="pitch-subject">
                  <v-chip v-if="x.category === 'application_update'" size="x-small" color="success" variant="tonal" class="mr-1">conversation</v-chip>
                  <a :href="x.gmailUrl" target="_blank" rel="noopener noreferrer" class="gmail-link">
                    {{ x.subject || '(no subject)' }} <v-icon size="11">mdi-open-in-new</v-icon>
                  </a>
                </span>
              </div>
            </div>

          </v-expansion-panel-text>
        </v-expansion-panel>
      </v-expansion-panels>
    </div>

    <!-- ── Preferences ───────────────────────────────────────────────────── -->
    <div v-else-if="settings" class="prefs">
      <section class="prefs-section">
        <h3 class="prefs-title">Triage</h3>
        <v-switch v-model="settings.triageEnabled" color="secondary" hide-details density="compact"
          label="Check my inbox for recruiter emails" />
        <v-switch v-model="settings.shadowMode" color="secondary" hide-details density="compact"
          label="Shadow mode: only label and draft, never send" />
        <p class="prefs-hint">
          Leave shadow mode on until the drafts look right. Even with it off, nothing is ever sent for a
          possible match: those always come to you.
        </p>
      </section>

      <section class="prefs-section">
        <h3 class="prefs-title">Pay floors (per year)</h3>
        <div class="prefs-grid">
          <div v-for="f in FLOORS" :key="f.key">
            <v-text-field v-model.number="settings[f.key]" :label="f.label" type="number" prefix="$"
              density="compact" variant="outlined" hide-details />
            <p class="field-hint">about ${{ hourly(settings[f.key]) }}/hr</p>
          </div>
          <div>
            <v-text-field v-model.number="settings.hoursPerYear" label="Hours per year" type="number"
              density="compact" variant="outlined" hide-details />
            <p class="field-hint">Used to annualize hourly pay</p>
          </div>
        </div>
      </section>

      <section class="prefs-section">
        <h3 class="prefs-title">Where</h3>
        <div class="prefs-checks">
          <v-checkbox v-model="settings.allowRemote" label="Remote" color="secondary" hide-details density="compact" />
          <v-checkbox v-model="settings.allowHybrid" label="Hybrid" color="secondary" hide-details density="compact" />
          <v-checkbox v-model="settings.allowOnsite" label="Onsite" color="secondary" hide-details density="compact" />
        </div>
        <v-combobox v-model="settings.acceptableLocations" label="Acceptable towns for hybrid and onsite" multiple chips
          closable-chips density="compact" variant="outlined" class="mt-3" hide-details />
        <p class="field-hint">Type a town and press Enter. Towns not listed go to review, never an automatic decline.</p>
        <v-text-field v-model="settings.homeState" label="Home state" density="compact" variant="outlined" class="mt-3 state-field" hide-details />
      </section>

      <section class="prefs-section">
        <h3 class="prefs-title">Employment</h3>
        <p class="prefs-label">Acceptable employment types</p>
        <div class="prefs-checks">
          <v-checkbox v-for="t in EMPLOYMENT_OPTIONS" :key="t" v-model="settings.allowedEmploymentTypes" :value="t"
            :label="EMPLOYMENT[t]" color="secondary" hide-details density="compact" />
        </div>
        <p class="prefs-label">Dealbreaker contract terms</p>
        <div class="prefs-checks">
          <v-checkbox v-for="t in ['c2c', '1099', 'w2']" :key="t" v-model="settings.dealbreakerContractTerms" :value="t"
            :label="t.toUpperCase()" color="secondary" hide-details density="compact" />
        </div>
      </section>

      <section class="prefs-section">
        <h3 class="prefs-title">Anything else</h3>
        <v-textarea v-model="settings.freeTextRequirements" rows="3" auto-grow density="compact" variant="outlined" hide-details />
        <p class="field-hint">Plain English. Checked by the AI for roles that pass everything above.</p>
      </section>

      <v-btn color="secondary" variant="flat" :loading="savingSettings" @click="saveSettings">
        <v-icon start>mdi-content-save</v-icon>Save preferences
      </v-btn>
    </div>

    <v-snackbar v-model="snackbar.show" :color="snackbar.color" :timeout="3000" location="bottom right">
      {{ snackbar.text }}
    </v-snackbar>
  </div>
</template>

<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { useAdminStore } from '@/stores/adminStore'

const FILTERS = [
  { value: 'pending', label: 'Needs review' },
  { value: 'interested', label: 'Interested' },
  { value: 'passed', label: 'Passed' },
  { value: 'auto', label: 'Handled automatically' },
  { value: 'all', label: 'All' },
]
const STATUS = {
  pending: { label: 'Needs review', color: 'warning' },
  interested: { label: 'Interested', color: 'success' },
  passed: { label: 'Passed', color: 'error' },
  auto: { label: 'Auto', color: 'default' },
}
const OUTCOME = {
  review: { label: 'Looks like a match', color: 'success' },
  ask_info: { label: 'Ask for details', color: 'info' },
  decline: { label: 'Decline', color: 'error' },
}
const ARRANGEMENT = { remote: 'Remote', hybrid: 'Hybrid', onsite: 'Onsite', unknown: 'Arrangement unknown' }
const EMPLOYMENT = {
  full_time: 'Full-time', contract_to_hire: 'Contract-to-hire', contract: 'Contract', part_time: 'Part-time',
  unknown: 'Type unknown',
}
const EMPLOYMENT_OPTIONS = ['full_time', 'contract_to_hire', 'contract', 'part_time']
const FLOORS = [
  { key: 'remotePayFloor', label: 'Remote' },
  { key: 'hybridPayFloor', label: 'Hybrid' },
  { key: 'onsitePayFloor', label: 'Onsite' },
]

const adminStore = useAdminStore()
const view = ref('queue')
const filter = ref('pending')
const postings = ref([])
const details = ref({})
const noteDrafts = ref({})
const openId = ref(null)
const stats = ref(null)
const settings = ref(null)
const loading = ref(false)
const saving = ref(null)
const savingSettings = ref(false)
const snackbar = ref({ show: false, text: '', color: 'success' })

const notify = (text, color = 'success') => { snackbar.value = { show: true, text, color } }

const triageLabel = computed(() => {
  if (!stats.value) return ''
  if (!stats.value.triageEnabled) return 'Triage off'
  return stats.value.shadowMode ? 'Shadow mode' : 'Live'
})
const triageColor = computed(() => !stats.value?.triageEnabled ? 'grey' : stats.value.shadowMode ? 'warning' : 'success')

function countFor(value) {
  if (!stats.value) return null
  if (value === 'all') return Object.values(stats.value.byReviewStatus).reduce((a, b) => a + b, 0)
  return stats.value.byReviewStatus[value] ?? 0
}

async function loadStats() {
  try {
    const res = await adminStore.apiFetch('/admin/recruiters/stats')
    if (res.ok) stats.value = await res.json()
  } catch { /* the queue still works without the counts */ }
}

async function loadPostings() {
  loading.value = true
  openId.value = null
  try {
    const params = filter.value === 'all' ? '' : `?reviewStatus=${filter.value}`
    const res = await adminStore.apiFetch(`/admin/recruiters/postings${params}`)
    if (res.ok) {
      postings.value = await res.json()
      noteDrafts.value = Object.fromEntries(postings.value.map(p => [p.recruiterPostingId, p.notes ?? '']))
    }
  } catch { notify('Failed to load postings', 'error') }
  finally { loading.value = false }
}

async function loadDetail(id) {
  try {
    const res = await adminStore.apiFetch(`/admin/recruiters/postings/${id}`)
    if (res.ok) details.value = { ...details.value, [id]: await res.json() }
  } catch { notify('Failed to load the emails for that posting', 'error') }
}

watch(openId, id => { if (id && !details.value[id]) loadDetail(id) })

async function review(p, body, savingKey) {
  saving.value = p.recruiterPostingId + savingKey
  try {
    const res = await adminStore.apiFetch(`/admin/recruiters/postings/${p.recruiterPostingId}`, {
      method: 'PATCH', body: JSON.stringify(body),
    })
    if (!res.ok) throw new Error()
    return true
  } catch {
    notify('Save failed', 'error')
    return false
  } finally { saving.value = null }
}

async function setStatus(p, status) {
  if (await review(p, { reviewStatus: status }, status)) {
    p.reviewStatus = status
    notify(status === 'pending' ? 'Back in the review queue' : `Marked ${status}`)
    loadStats()
  }
}

async function saveNotes(p) {
  const notes = noteDrafts.value[p.recruiterPostingId] ?? ''
  if (await review(p, { notes }, 'notes')) {
    p.notes = notes || null
    notify('Notes saved')
  }
}

async function loadSettings() {
  try {
    const res = await adminStore.apiFetch('/admin/recruiters/settings')
    if (res.ok) settings.value = await res.json()
  } catch { notify('Failed to load preferences', 'error') }
}

async function saveSettings() {
  savingSettings.value = true
  try {
    const res = await adminStore.apiFetch('/admin/recruiters/settings', {
      method: 'PUT', body: JSON.stringify(settings.value),
    })
    const body = await res.json().catch(() => ({}))
    if (!res.ok) { notify(body.message ?? 'Save failed', 'error'); return }
    settings.value = body
    notify('Preferences saved')
    loadStats()
  } catch { notify('Save failed', 'error') }
  finally { savingSettings.value = false }
}

const hourly = annual => settings.value?.hoursPerYear ? (annual / settings.value.hoursPerYear).toFixed(2) : '?'

function refresh() {
  details.value = {}
  loadStats()
  loadPostings()
}

function formatDate(iso) {
  return new Date(iso).toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
}
function formatDateTime(iso) {
  return new Date(iso).toLocaleString('en-US', { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' })
}

watch(view, v => { if (v === 'preferences' && !settings.value) loadSettings() })

defineExpose({ load: refresh })
onMounted(refresh)
</script>

<style scoped>
.tab-toolbar { display: flex; align-items: center; gap: 0.75rem; margin-bottom: 1rem; flex-wrap: wrap; }
.chip-count { margin-left: 0.4rem; font-weight: 700; opacity: .75; }
.sync-status { font-size: 0.75rem; color: rgba(var(--v-theme-on-surface), 0.55); display: inline-flex; align-items: center; gap: 0.3rem; }
.loading-state { display: flex; justify-content: center; padding: 4rem; }
.loading-state--small { padding: 1rem; }
.empty-state { text-align: center; padding: 3rem; color: rgba(var(--v-theme-on-surface), 0.5); }

.postings-panels { border-radius: 12px !important; overflow: hidden; }
.posting-panel { background: rgb(var(--v-theme-surface)) !important; border-bottom: 1px solid rgba(255,255,255,0.06) !important; }
.posting-header { font-family: 'Raleway', sans-serif; }
.posting-header__main { display: flex; flex-direction: column; gap: 0.25rem; min-width: 0; flex: 1; }
.posting-header__side { display: flex; flex-direction: column; align-items: flex-end; gap: 0.3rem; padding-right: 1rem; flex-shrink: 0; }
.posting-title { font-size: 0.92rem; font-weight: 600; display: flex; align-items: center; flex-wrap: wrap; gap: 0.25rem; }
.posting-company { font-weight: 400; color: rgba(var(--v-theme-on-surface), 0.6); }
.posting-meta { font-size: 0.76rem; color: rgba(var(--v-theme-on-surface), 0.55); }
.posting-count { font-size: 0.72rem; color: rgba(var(--v-theme-on-surface), 0.45); white-space: nowrap; }

.reason-list { margin: 0 0 0.9rem; padding-left: 1.1rem; font-size: 0.85rem; line-height: 1.6; color: rgba(var(--v-theme-on-surface), 0.85); }
.reason-missing { color: rgba(var(--v-theme-on-surface), 0.6); }
.review-row { display: flex; gap: 0.5rem; flex-wrap: wrap; margin-bottom: 0.9rem; }
.notes-actions { display: flex; justify-content: flex-end; margin-bottom: 0.75rem; }

.pitch-list { border-top: 1px solid rgba(255,255,255,0.06); padding-top: 0.6rem; }
.pitch-list__title { font-size: 0.7rem; font-weight: 700; text-transform: uppercase; letter-spacing: 0.06em; color: rgba(var(--v-theme-on-surface), 0.45); margin-bottom: 0.4rem; }
.pitch-row { display: grid; grid-template-columns: 4rem 14rem 6rem 1fr; gap: 0.75rem; font-size: 0.8rem; padding: 0.35rem 0; border-bottom: 1px solid rgba(255,255,255,0.04); align-items: baseline; }
.pitch-date { color: rgba(var(--v-theme-on-surface), 0.5); font-variant-numeric: tabular-nums; }
.pitch-agency { font-weight: 600; }
.pitch-recruiter { display: block; font-weight: 400; font-size: 0.72rem; color: rgba(var(--v-theme-on-surface), 0.5); }
.pitch-pay { font-variant-numeric: tabular-nums; color: rgb(var(--v-theme-secondary)); }
.gmail-link { color: rgba(var(--v-theme-on-surface), 0.85); text-decoration: none; }
.gmail-link:hover { text-decoration: underline; color: rgb(var(--v-theme-secondary)); }

.prefs { max-width: 760px; }
.prefs-section { margin-bottom: 1.75rem; }
.prefs-title { font-size: 0.75rem; font-weight: 700; text-transform: uppercase; letter-spacing: 0.08em; color: rgba(var(--v-theme-on-surface), 0.55); margin: 0 0 0.6rem; }
.prefs-label { font-size: 0.8rem; color: rgba(var(--v-theme-on-surface), 0.65); margin: 0.6rem 0 0.1rem; }
.prefs-hint { font-size: 0.75rem; color: rgba(var(--v-theme-on-surface), 0.5); margin: 0.3rem 0 0; }
.prefs-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(160px, 1fr)); gap: 0.75rem; }
.prefs-checks { display: flex; flex-wrap: wrap; gap: 0 1rem; }
.state-field { max-width: 140px; }
.field-hint { font-size: 0.72rem; color: rgba(var(--v-theme-on-surface), 0.5); margin: 0.3rem 0 0 0.2rem; }

@media (max-width: 700px) {
  .pitch-row { grid-template-columns: 1fr; gap: 0.15rem; }
  .posting-header__side { display: none; }
}
</style>
