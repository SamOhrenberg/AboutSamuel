<template>
  <div>
    <div class="tab-toolbar">
      <v-btn variant="tonal" color="secondary" size="small" :loading="starting" :disabled="anyRunning" @click="startRun">
        <v-icon start>mdi-shield-bug</v-icon>Run tests
      </v-btn>
      <span class="toolbar-note">
        Attacks SamuelLM with tricky questions (false premises, made-up numbers, prompt injection...)
        and has a judge model grade every answer against the portfolio. About a minute per run.
      </span>
      <v-spacer />
      <v-btn variant="text" size="small" color="secondary" @click="loadRuns">
        <v-icon start>mdi-refresh</v-icon>Refresh
      </v-btn>
    </div>

    <div v-if="loading && !runs.length" class="loading-state">
      <v-progress-circular indeterminate color="secondary" />
    </div>

    <p v-else-if="!runs.length" class="empty-state">No runs yet. Hit <strong>Run tests</strong> to get a baseline.</p>

    <v-expansion-panels v-else v-model="openRunId" variant="accordion" class="runs-panels">
      <v-expansion-panel v-for="run in runs" :key="run.adversarialRunId" :value="run.adversarialRunId" class="run-panel">
        <v-expansion-panel-title class="run-header">
          <div class="run-header__left">
            <v-icon :color="statusColor(run)" size="18" class="mr-2">{{ statusIcon(run) }}</v-icon>
            <span class="run-date">{{ formatDateTime(run.startedAt) }}</span>
            <v-chip v-if="run.status === 'completed'" size="small" :color="rateColor(run.passRate)" variant="tonal" class="ml-3">
              {{ run.passed }}/{{ run.scored }} passed · {{ pct(run.passRate) }}
            </v-chip>
            <v-chip v-else-if="run.status === 'failed'" size="small" color="error" variant="tonal" class="ml-3">Failed</v-chip>
            <span v-else class="run-progress-text ml-3">{{ progressLabel(run) }}</span>
          </div>
          <div class="run-header__right">
            <span class="run-meta">judge: {{ run.judgeModel }}</span>
          </div>
        </v-expansion-panel-title>

        <v-expansion-panel-text>
          <!-- Running -->
          <div v-if="run.status === 'running'" class="run-running">
            <v-progress-linear :model-value="progressValue(run)" color="secondary" height="6" rounded />
            <p class="run-running__label">{{ progressLabel(run) }}</p>
          </div>

          <!-- Failed -->
          <p v-else-if="run.status === 'failed'" class="run-error">{{ run.error }}</p>

          <!-- Completed -->
          <template v-else>
            <div v-if="!details[run.adversarialRunId]" class="loading-state loading-state--small">
              <v-progress-circular indeterminate color="secondary" size="24" />
            </div>
            <template v-else>
              <div class="category-row">
                <div v-for="(c, key) in run.summary" :key="key" class="category-chip"
                  :class="{ 'category-chip--fail': c.fail > 0 }">
                  <span class="category-chip__name">{{ CATEGORY_LABELS[key] ?? key }}</span>
                  <span class="category-chip__score">{{ c.pass }}/{{ c.pass + c.fail }}</span>
                </div>
              </div>

              <div v-for="c in failingCases(run)" :key="c.caseKey" class="case-card"
                :class="`case-card--${c.verdict === 'fail' ? c.severity : 'other'}`">
                <div class="case-meta">
                  <span class="case-key">{{ c.caseKey }}</span>
                  <span class="case-category">{{ CATEGORY_LABELS[c.category] ?? c.category }}</span>
                  <v-chip size="x-small" :color="c.verdict === 'fail' ? severityColor(c.severity) : 'default'" variant="tonal">
                    {{ c.verdict === 'fail' ? `${c.severity} · ${label(c.failureType)}` : label(c.verdict) }}
                  </v-chip>
                  <v-chip v-if="c.source === 'generated'" size="x-small" variant="outlined">generated</v-chip>
                </div>

                <div v-for="(turn, i) in c.history" :key="i" class="exchange">
                  <span class="exchange-label">{{ turn.role === 'user' ? 'Visitor (earlier)' : 'SamuelLM (earlier)' }}</span>
                  <p class="exchange-text exchange-text--muted">{{ turn.content }}</p>
                </div>
                <div class="exchange">
                  <span class="exchange-label">Visitor</span>
                  <p class="exchange-text">{{ c.prompt }}</p>
                  <p v-if="c.plantedClaim" class="planted">Planted: {{ c.plantedClaim }}</p>
                </div>
                <div class="exchange">
                  <span class="exchange-label exchange-label--assistant">SamuelLM</span>
                  <p v-if="c.blockedByContentFilter" class="exchange-text exchange-text--muted">
                    (Blocked by Azure's content filter before SamuelLM saw it)
                  </p>
                  <p v-else class="exchange-text">{{ c.answer }}</p>
                  <p v-if="c.toolCalls.length" class="tool-calls">
                    Tools: {{ c.toolCalls.map(t => t.name).join(', ') }}
                  </p>
                </div>
                <div class="exchange">
                  <span class="exchange-label exchange-label--judge">Judge</span>
                  <p class="exchange-text">{{ c.explanation }}</p>
                  <ul v-if="unsupported(c).length" class="unsupported-list">
                    <li v-for="claim in unsupported(c)" :key="claim.claim">
                      <strong>Unsupported:</strong> {{ claim.claim }}
                      <span class="claim-note">({{ claim.note }})</span>
                    </li>
                  </ul>
                </div>
              </div>

              <v-btn variant="text" size="small" color="secondary" class="mt-2"
                @click="showPassing[run.adversarialRunId] = !showPassing[run.adversarialRunId]">
                <v-icon start>{{ showPassing[run.adversarialRunId] ? 'mdi-chevron-up' : 'mdi-chevron-down' }}</v-icon>
                {{ showPassing[run.adversarialRunId] ? 'Hide' : 'Show' }} {{ passingCases(run).length }} passing cases
              </v-btn>
              <div v-if="showPassing[run.adversarialRunId]" class="passing-list">
                <div v-for="c in passingCases(run)" :key="c.caseKey" class="passing-row">
                  <span class="case-key">{{ c.caseKey }}</span>
                  <span class="passing-row__q">{{ c.prompt }}</span>
                  <span class="passing-row__a">
                    {{ c.blockedByContentFilter ? '(blocked by Azure)' : truncate(c.answer, 140) }}
                  </span>
                </div>
              </div>
            </template>
          </template>
        </v-expansion-panel-text>
      </v-expansion-panel>
    </v-expansion-panels>

    <v-snackbar v-model="snackbar.show" :color="snackbar.color" :timeout="3000" location="bottom right">
      {{ snackbar.text }}
    </v-snackbar>
  </div>
</template>

<script setup>
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useAdminStore } from '@/stores/adminStore'

const CATEGORY_LABELS = {
  false_premise: 'False premise',
  twisted_fact: 'Twisted fact',
  fabricated_numbers: 'Made-up numbers',
  prompt_injection: 'Prompt injection',
  private_info: 'Private info',
  off_topic: 'Off topic',
  control: 'Control',
}

const POLL_MS = 3000

const adminStore = useAdminStore()

const runs = ref([])
const details = ref({})       // runId -> run with cases
const showPassing = ref({})   // runId -> bool
const openRunId = ref(null)
const loading = ref(false)
const starting = ref(false)
const snackbar = ref({ show: false, text: '', color: 'success' })
let pollTimer = null

const anyRunning = computed(() => runs.value.some(r => r.status === 'running'))

function notify(text, color = 'success') { snackbar.value = { show: true, text, color } }

async function loadRuns() {
  loading.value = true
  try {
    const res = await adminStore.apiFetch('/admin/adversarial/runs')
    if (res.ok) runs.value = await res.json()
  } catch { notify('Failed to load test runs', 'error') }
  finally { loading.value = false }
  schedulePoll()
  // A run that finished while open needs its details now
  const open = runs.value.find(r => r.adversarialRunId === openRunId.value)
  if (open?.status === 'completed' && !details.value[open.adversarialRunId]) loadDetail(open.adversarialRunId)
}

async function loadDetail(runId) {
  try {
    const res = await adminStore.apiFetch(`/admin/adversarial/runs/${runId}`)
    if (res.ok) details.value = { ...details.value, [runId]: await res.json() }
  } catch { notify('Failed to load run details', 'error') }
}

async function startRun() {
  starting.value = true
  try {
    const res = await adminStore.apiFetch('/admin/adversarial/runs', { method: 'POST' })
    const body = await res.json().catch(() => ({}))
    if (res.status === 202) {
      notify('Test run started')
      openRunId.value = body.runId
    } else {
      notify(body.message ?? 'Could not start a run', 'error')
    }
  } catch { notify('Could not start a run', 'error') }
  finally {
    starting.value = false
    loadRuns()
  }
}

// Only poll while something is running, so an idle admin tab costs nothing
function schedulePoll() {
  clearTimeout(pollTimer)
  if (anyRunning.value) pollTimer = setTimeout(loadRuns, POLL_MS)
}

watch(openRunId, id => {
  const run = runs.value.find(r => r.adversarialRunId === id)
  if (run?.status === 'completed' && !details.value[id]) loadDetail(id)
})

const casesOf = run => details.value[run.adversarialRunId]?.cases ?? []
const failingCases = run => casesOf(run).filter(c => c.verdict !== 'pass')
const passingCases = run => casesOf(run).filter(c => c.verdict === 'pass')
// Only claims about Samuel count. Runs from before about_samuel existed don't have it,
// so undefined still shows.
const unsupported = c => (c.claims ?? []).filter(claim => !claim.supported && claim.about_samuel !== false)

function progressValue(run) {
  if (!run.totalCases) return 0
  return ((run.casesAnswered + run.casesJudged) / (run.totalCases * 2)) * 100
}

function progressLabel(run) {
  if (!run.totalCases) return 'Building test cases...'
  if (run.casesAnswered < run.totalCases) return `SamuelLM answering ${run.casesAnswered}/${run.totalCases}`
  return `Judging ${run.casesJudged}/${run.totalCases}`
}

const statusIcon = run => ({ running: 'mdi-progress-clock', failed: 'mdi-alert-circle' }[run.status] ?? 'mdi-shield-check')
const statusColor = run => run.status === 'failed' ? 'error' : run.status === 'running' ? 'secondary' : rateColor(run.passRate)
const rateColor = rate => rate == null ? 'default' : rate >= 0.9 ? 'success' : rate >= 0.7 ? 'warning' : 'error'
const severityColor = s => ({ high: 'error', medium: 'warning', low: 'info' }[s] ?? 'default')
const pct = rate => rate == null ? 'n/a' : `${Math.round(rate * 100)}%`
const label = s => s.replaceAll('_', ' ')
const truncate = (s, n) => s.length > n ? `${s.slice(0, n)}...` : s

function formatDateTime(iso) {
  return new Date(iso).toLocaleString('en-US', { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit', hour12: true })
}

defineExpose({ load: loadRuns })
onMounted(loadRuns)
onBeforeUnmount(() => clearTimeout(pollTimer))
</script>

<style scoped>
.tab-toolbar { display: flex; align-items: center; gap: 0.75rem; margin-bottom: 1.25rem; flex-wrap: wrap; }
.toolbar-note { font-size: 0.75rem; color: rgba(var(--v-theme-on-surface), 0.5); max-width: 520px; line-height: 1.5; }
.loading-state { display: flex; justify-content: center; padding: 4rem; }
.loading-state--small { padding: 1.5rem; }
.empty-state { text-align: center; padding: 3rem; color: rgba(var(--v-theme-on-surface), 0.5); }

.runs-panels { border-radius: 12px !important; overflow: hidden; }
.run-panel { background: rgb(var(--v-theme-surface)) !important; border-bottom: 1px solid rgba(255,255,255,0.06) !important; }
.run-header { font-family: 'Raleway', sans-serif; font-size: 0.875rem; }
.run-header__left { display: flex; align-items: center; flex-wrap: wrap; gap: 4px; }
.run-header__right { margin-left: auto; padding-right: 1rem; }
.run-date { font-weight: 600; }
.run-meta { font-size: 0.75rem; font-family: monospace; color: rgba(var(--v-theme-on-surface), 0.45); }
.run-progress-text { font-size: 0.78rem; color: rgb(var(--v-theme-secondary)); }

.run-running { padding: 0.5rem 0; }
.run-running__label { font-size: 0.8rem; margin: 0.5rem 0 0; color: rgba(var(--v-theme-on-surface), 0.6); }
.run-error { color: rgb(var(--v-theme-error)); font-size: 0.85rem; margin: 0; }

.category-row { display: flex; flex-wrap: wrap; gap: 0.5rem; margin-bottom: 1rem; }
.category-chip {
  display: flex; align-items: center; gap: 0.5rem;
  padding: 0.3rem 0.7rem; border-radius: 8px; font-size: 0.78rem;
  border: 1px solid rgba(var(--v-theme-success), 0.35); background: rgba(var(--v-theme-success), 0.06);
}
.category-chip--fail { border-color: rgba(var(--v-theme-error), 0.4); background: rgba(var(--v-theme-error), 0.07); }
.category-chip__name { color: rgba(var(--v-theme-on-surface), 0.75); }
.category-chip__score { font-weight: 700; font-variant-numeric: tabular-nums; }

.case-card {
  padding: 0.9rem 1rem; margin-bottom: 0.75rem; border-radius: 8px;
  border-left: 3px solid rgba(var(--v-theme-on-surface), 0.25); background: rgba(var(--v-theme-on-surface), 0.03);
}
.case-card--high   { border-left-color: rgb(var(--v-theme-error)); background: rgba(var(--v-theme-error), 0.04); }
.case-card--medium { border-left-color: rgb(var(--v-theme-warning)); background: rgba(var(--v-theme-warning), 0.04); }
.case-card--low    { border-left-color: rgb(var(--v-theme-info)); }
.case-meta { display: flex; align-items: center; flex-wrap: wrap; gap: 0.5rem; margin-bottom: 0.6rem; }
.case-key { font-family: monospace; font-size: 0.78rem; color: rgb(var(--v-theme-secondary)); }
.case-category { font-size: 0.75rem; color: rgba(var(--v-theme-on-surface), 0.55); }

.exchange { margin-bottom: 0.55rem; }
.exchange-label { font-size: 0.68rem; font-weight: 700; text-transform: uppercase; letter-spacing: 0.06em; color: rgba(var(--v-theme-on-surface), 0.4); display: block; margin-bottom: 0.15rem; }
.exchange-label--assistant { color: rgb(var(--v-theme-secondary)); }
.exchange-label--judge { color: rgb(var(--v-theme-warning)); }
.exchange-text { font-size: 0.85rem; line-height: 1.6; color: rgba(var(--v-theme-on-surface), 0.85); margin: 0; white-space: pre-wrap; }
.exchange-text--muted { color: rgba(var(--v-theme-on-surface), 0.5); font-style: italic; }
.planted, .tool-calls { font-size: 0.75rem; margin: 0.25rem 0 0; color: rgba(var(--v-theme-on-surface), 0.5); }
.unsupported-list { margin: 0.4rem 0 0; padding-left: 1.1rem; font-size: 0.8rem; line-height: 1.6; color: rgba(var(--v-theme-on-surface), 0.8); }
.claim-note { color: rgba(var(--v-theme-on-surface), 0.45); }

.passing-list { display: flex; flex-direction: column; gap: 0.4rem; margin-top: 0.5rem; }
.passing-row { display: grid; grid-template-columns: 9rem 1fr 1fr; gap: 0.75rem; font-size: 0.78rem; padding: 0.4rem 0; border-top: 1px solid rgba(255,255,255,0.05); }
.passing-row__q { color: rgba(var(--v-theme-on-surface), 0.8); }
.passing-row__a { color: rgba(var(--v-theme-on-surface), 0.5); }
@media (max-width: 700px) {
  .passing-row { grid-template-columns: 1fr; gap: 0.2rem; }
}
</style>
