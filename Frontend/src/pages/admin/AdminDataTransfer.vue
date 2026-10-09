<template>
  <div>
    <div v-if="loading" class="loading-state"><v-progress-circular indeterminate color="secondary" size="24" /></div>

    <template v-else-if="info">
      <!-- ── Export ── -->
      <section class="block">
        <h2 class="block-title">Export from this site</h2>
        <p class="hint">Downloads a snapshot of the selected data as a JSON file. Nothing on this site changes.</p>

        <div v-for="g in info.groups" :key="g.key" class="group-row">
          <v-checkbox v-model="exportGroups" :value="g.key" hide-details density="compact" color="secondary">
            <template #label>
              <div>
                <span class="group-label">{{ g.label }}</span>
                <span class="muted"> · {{ rowSummary(g) }}</span>
                <div class="muted">{{ g.description }}</div>
                <div v-if="g.warning" class="warn">{{ g.warning }}</div>
              </div>
            </template>
          </v-checkbox>
        </div>

        <v-btn color="secondary" variant="tonal" class="mt-3" :disabled="!exportGroups.length" :loading="exporting"
          @click="doExport">
          <v-icon start>mdi-download</v-icon>Download export
        </v-btn>
      </section>

      <!-- ── Import ── -->
      <section class="block">
        <h2 class="block-title">Import into this site</h2>

        <v-alert v-if="!info.allowImport" type="info" variant="tonal" density="compact">
          Import is turned off here, which is what you want on production. To allow it, set
          <code>DataTransfer__AllowImport=true</code> on the dev API.
        </v-alert>

        <template v-else>
          <v-alert type="warning" variant="tonal" density="compact" class="mb-3">
            Importing <strong>replaces</strong> everything in the groups you tick with the contents of the file.
            It runs in one transaction, so a failure leaves this site unchanged.
          </v-alert>

          <v-btn variant="tonal" color="secondary" size="small" @click="fileInput.click()">
            <v-icon start>mdi-file-upload</v-icon>{{ file ? 'Choose a different file' : 'Choose export file' }}
          </v-btn>
          <input ref="fileInput" type="file" accept="application/json,.json" class="sr-only" @change="onFileChosen" />

          <v-alert v-if="fileError" type="error" variant="tonal" density="compact" class="mt-3">{{ fileError }}</v-alert>

          <template v-if="preview">
            <p class="hint mt-3">
              {{ file.name }} · exported {{ formatDateTime(preview.exportedAt) }}
            </p>

            <div v-for="g in info.groups" :key="g.key" class="group-row">
              <v-checkbox v-model="importGroups" :value="g.key" :disabled="!preview.groups[g.key]" hide-details
                density="compact" color="secondary">
                <template #label>
                  <div>
                    <span class="group-label">{{ g.label }}</span>
                    <span class="muted"> · {{ preview.groups[g.key] ? `${preview.groups[g.key].rows} rows in file, ${currentRows(g)} here will be replaced` : 'not in this file' }}</span>
                    <div v-if="g.warning" class="warn">{{ g.warning }}</div>
                  </div>
                </template>
              </v-checkbox>
            </div>

            <v-text-field v-model="confirm" label="Type OVERWRITE to confirm" variant="outlined" density="compact"
              hide-details class="confirm-field" autocomplete="off" />
            <v-btn color="error" variant="flat" class="mt-3" :disabled="!canImport" :loading="importing"
              @click="doImport">
              <v-icon start>mdi-database-import</v-icon>Replace data on this site
            </v-btn>
          </template>

          <v-alert v-if="result" :type="result.ok ? 'success' : 'error'" variant="tonal" density="compact" class="mt-3">
            {{ result.text }}
          </v-alert>
        </template>
      </section>
    </template>

    <v-alert v-else type="error" variant="tonal" density="compact">Couldn't load data transfer info.</v-alert>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useAdminStore } from '@/stores/adminStore'

const adminStore = useAdminStore()
const emit = defineEmits(['changed'])

const loading = ref(true)
const info = ref(null)

const exportGroups = ref([])
const exporting = ref(false)

const fileInput = ref(null)
const file = ref(null)
const fileError = ref('')
const preview = ref(null)   // { exportedAt, groups: { key: { rows } } } for groups fully present in the file
const importGroups = ref([])
const confirm = ref('')
const importing = ref(false)
const result = ref(null)

const canImport = computed(() =>
  importGroups.value.length > 0 && confirm.value === 'OVERWRITE' && !importing.value)

function formatDateTime(v) { return v ? new Date(v).toLocaleString() : '' }
function rowSummary(g) { return g.tables.map(t => `${t.rows} ${t.name}`).join(', ') }
function currentRows(g) { return g.tables.reduce((n, t) => n + t.rows, 0) }

async function load() {
  loading.value = true
  try {
    const res = await adminStore.apiFetch('/admin/data-transfer/info')
    if (!res.ok) throw new Error()
    info.value = await res.json()
    exportGroups.value = info.value.groups.filter(g => g.defaultSelected).map(g => g.key)
  } catch { info.value = null }
  finally { loading.value = false }
}

async function doExport() {
  exporting.value = true
  try {
    const res = await adminStore.apiFetch(
      `/admin/data-transfer/export?groups=${encodeURIComponent(exportGroups.value.join(','))}`)
    if (!res.ok) throw new Error(await res.text())
    const blob = await res.blob()
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `aboutsamuel-export-${new Date().toISOString().slice(0, 16).replace(/[-:T]/g, '')}.json`
    a.click()
    URL.revokeObjectURL(url)
  } catch (e) {
    fileError.value = ''
    result.value = { ok: false, text: `Export failed. ${e.message ?? ''}` }
  } finally { exporting.value = false }
}

async function onFileChosen(e) {
  const chosen = e.target.files?.[0]
  e.target.value = ''
  if (!chosen) return
  fileError.value = ''
  result.value = null
  preview.value = null
  importGroups.value = []
  confirm.value = ''

  try {
    const data = JSON.parse(await chosen.text())
    if (data.format !== 1 || !data.tables) throw new Error()
    const groups = {}
    for (const g of info.value.groups) {
      // A group is importable only when every one of its tables is in the file
      if (g.tables.every(t => Array.isArray(data.tables[t.name]))) {
        groups[g.key] = { rows: g.tables.reduce((n, t) => n + data.tables[t.name].length, 0) }
      }
    }
    file.value = chosen
    preview.value = { exportedAt: data.exportedAt, groups }
    importGroups.value = info.value.groups.filter(g => g.defaultSelected && groups[g.key]).map(g => g.key)
  } catch {
    fileError.value = "That isn't an AboutSamuel export file."
  }
}

async function doImport() {
  importing.value = true
  result.value = null
  try {
    const form = new FormData()
    form.append('file', file.value)
    form.append('groups', importGroups.value.join(','))
    form.append('confirm', confirm.value)
    const res = await adminStore.apiFetch('/admin/data-transfer/import', { method: 'POST', body: form })
    const body = await res.json().catch(() => ({}))
    if (!res.ok) {
      result.value = { ok: false, text: body.message ?? 'Import failed.' }
      return
    }
    const skipped = body.skipped?.length ? ` Skipped (not fully in the file): ${body.skipped.join(', ')}.` : ''
    result.value = { ok: true, text: `Replaced: ${body.imported.join(', ')}.${skipped}` }
    confirm.value = ''
    emit('changed')
    await load()
  } catch (e) {
    result.value = { ok: false, text: e.message ?? 'Import failed.' }
  } finally { importing.value = false }
}

onMounted(load)
</script>

<style scoped>
.block { margin-bottom: 2rem; max-width: 720px; }
.block-title { font-family: 'Patua One', serif; font-size: 1.1rem; margin-bottom: 0.4rem; }
.hint, .muted { font-size: 0.8rem; color: rgba(var(--v-theme-on-surface), 0.6); }
.group-row { margin-left: -0.5rem; }
.group-label { font-weight: 600; }
.warn { font-size: 0.8rem; color: rgb(var(--v-theme-warning)); }
.confirm-field { max-width: 280px; margin-top: 0.75rem; }
.loading-state { display: flex; justify-content: center; padding: 2rem; }
.sr-only { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); }
</style>
