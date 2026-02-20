<template>
  <div class="admin-section">

    <!-- ── Header ─────────────────────────────────────────────────────────── -->
    <div class="admin-section__header">
      <h2 class="admin-section__title">Work Experience</h2>
      <v-btn color="secondary" variant="flat" size="small" @click="openDialog()">
        + Add Work
      </v-btn>
    </div>

    <!-- ── Works list ──────────────────────────────────────────────────────── -->
    <v-progress-linear v-if="loading" indeterminate color="secondary" />

    <div v-else-if="works.length" class="works-list">
      <div
        v-for="work in works"
        :key="work.workExperienceId"
        class="work-row"
        :class="{ 'work-row--inactive': !work.isActive }"
      >
        <div class="work-row__info">
          <span class="work-row__title">{{ work.title }}</span>
          <span class="work-row__employer">{{ work.employer }}</span>
          <span class="work-row__years">
            {{ work.startYear }}{{ work.endYear ? '–' + work.endYear : '–Present' }}
          </span>
          <v-chip
            v-if="!work.isActive"
            size="x-small"
            color="warning"
            variant="tonal"
          >Inactive</v-chip>
        </div>
        <div class="work-row__meta">
          <span class="work-row__achievements">
            {{ work.achievements.length }} achievement{{ work.achievements.length !== 1 ? 's' : '' }}
          </span>
          <v-btn icon size="x-small" variant="text" @click="openDialog(work)">
            <v-icon>mdi-pencil</v-icon>
          </v-btn>
          <v-btn icon size="x-small" variant="text" color="error" @click="confirmDelete(work)">
            <v-icon>mdi-delete</v-icon>
          </v-btn>
        </div>
      </div>
    </div>

    <p v-else class="admin-section__empty">No work experience entries yet.</p>

    <!-- ── Create / Edit dialog ───────────────────────────────────────────── -->
    <v-dialog v-model="dialogOpen" max-width="720" persistent scrollable>
      <v-card>
        <v-card-title class="dialog-title">
          {{ editingId ? 'Edit Work Experience' : 'Add Work Experience' }}
        </v-card-title>

        <v-card-text>
          <v-row dense>

            <v-col cols="12" sm="6">
              <v-text-field
                v-model="form.employer"
                label="Employer"
                variant="outlined"
                density="comfortable"
                color="secondary"
                :rules="[required]"
              />
            </v-col>

            <v-col cols="12" sm="6">
              <v-text-field
                v-model="form.title"
                label="Work Title"
                variant="outlined"
                density="comfortable"
                color="secondary"
                :rules="[required]"
              />
            </v-col>

            <v-col cols="6" sm="3">
              <v-text-field
                v-model="form.startYear"
                label="Start Year"
                variant="outlined"
                density="comfortable"
                color="secondary"
                placeholder="2019"
              />
            </v-col>

            <v-col cols="6" sm="3">
              <v-text-field
                v-model="form.endYear"
                label="End Year"
                variant="outlined"
                density="comfortable"
                color="secondary"
                placeholder="2025 or leave blank for Present"
              />
            </v-col>

            <v-col cols="12" sm="6">
              <v-switch
                v-model="form.isActive"
                label="Active"
                color="secondary"
                density="comfortable"
                hide-details
              />
            </v-col>

            <v-col cols="12">
              <v-textarea
                v-model="form.summary"
                label="Summary (optional)"
                variant="outlined"
                density="comfortable"
                color="secondary"
                rows="2"
                auto-grow
                hint="A brief paragraph describing the role and its context"
                persistent-hint
              />
            </v-col>

            <!-- Achievements -->
            <v-col cols="12">
              <div class="achievements-label">
                Achievements
                <v-btn
                  size="x-small"
                  variant="text"
                  color="secondary"
                  @click="addAchievement"
                >+ Add</v-btn>
              </div>

              <div
                v-for="(achievement, i) in form.achievements"
                :key="i"
                class="achievement-row"
              >
                <v-text-field
                  v-model="form.achievements[i]"
                  :label="`Achievement ${i + 1}`"
                  variant="outlined"
                  density="compact"
                  color="secondary"
                  hide-details
                  class="achievement-field"
                />
                <v-btn
                  icon
                  size="x-small"
                  variant="text"
                  color="error"
                  @click="removeAchievement(i)"
                >
                  <v-icon>mdi-close</v-icon>
                </v-btn>
              </div>

              <p v-if="!form.achievements.length" class="achievements-empty">
                No achievements added yet. Use "+ Add" to add bullet points.
              </p>
            </v-col>

            <v-col cols="12" sm="4">
              <v-text-field
                v-model.number="form.displayOrder"
                label="Display Order"
                type="number"
                variant="outlined"
                density="comfortable"
                color="secondary"
                hide-details
              />
            </v-col>

          </v-row>
        </v-card-text>

        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="closeDialog">Cancel</v-btn>
          <v-btn
            color="secondary"
            variant="flat"
            :loading="saving"
            @click="save"
          >
            {{ editingId ? 'Save Changes' : 'Create' }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- ── Delete confirmation ────────────────────────────────────────────── -->
    <v-dialog v-model="deleteDialogOpen" max-width="400">
      <v-card>
        <v-card-title>Delete Work Experience</v-card-title>
        <v-card-text>
          Are you sure you want to delete <strong>{{ deletingWork?.title }}</strong>
          at {{ deletingWork?.employer }}? This cannot be undone.
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteDialogOpen = false">Cancel</v-btn>
          <v-btn color="error" variant="flat" :loading="deleting" @click="deleteWork">
            Delete
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import axios from 'axios'

const API = `${import.meta.env.VITE_API_URL}/admin/workexperience`

// ── State ────────────────────────────────────────────────────────────────────
const works            = ref([])
const loading         = ref(false)
const saving          = ref(false)
const deleting        = ref(false)
const dialogOpen      = ref(false)
const deleteDialogOpen = ref(false)
const editingId       = ref(null)
const deletingWork     = ref(null)

const emptyForm = () => ({
  employer:     '',
  title:        '',
  startYear:    '',
  endYear:      '',
  summary:      '',
  achievements: [],
  displayOrder: 0,
  isActive:     true,
})

const form = ref(emptyForm())

// ── Validation ───────────────────────────────────────────────────────────────
const required = (v) => !!v || 'Required'

// ── Lifecycle ────────────────────────────────────────────────────────────────
onMounted(fetchWorks)

// ── Data fetching ────────────────────────────────────────────────────────────
async function fetchWorks() {
  loading.value = true
  try {
    const { data } = await axios.get(API)
    works.value = data
  } catch (err) {
    console.error('Failed to fetch works:', err)
  } finally {
    loading.value = false
  }
}

// ── Dialog management ────────────────────────────────────────────────────────
function openDialog(work = null) {
  if (work) {
    editingId.value = work.workExperienceId
    form.value = {
      employer:     work.employer,
      title:        work.title,
      startYear:    work.startYear ?? '',
      endYear:      work.endYear   ?? '',
      summary:      work.summary   ?? '',
      achievements: [...work.achievements],
      displayOrder: work.displayOrder,
      isActive:     work.isActive,
    }
  } else {
    editingId.value = null
    form.value = emptyForm()
  }
  dialogOpen.value = true
}

function closeDialog() {
  dialogOpen.value = false
  editingId.value  = null
  form.value       = emptyForm()
}

// ── Achievements helpers ─────────────────────────────────────────────────────
function addAchievement() {
  form.value.achievements.push('')
}

function removeAchievement(index) {
  form.value.achievements.splice(index, 1)
}

// ── Save ─────────────────────────────────────────────────────────────────────
async function save() {
  saving.value = true
  try {
    const payload = {
      ...form.value,
      startYear: form.value.startYear || null,
      endYear:   form.value.endYear   || null,
      achievements: form.value.achievements.filter(a => a.trim() !== ''),
    }

    if (editingId.value) {
      await axios.put(`${API}/${editingId.value}`, payload)
    } else {
      await axios.post(API, payload)
    }

    await fetchWorks()
    closeDialog()
  } catch (err) {
    console.error('Failed to save work:', err)
  } finally {
    saving.value = false
  }
}

// ── Delete ───────────────────────────────────────────────────────────────────
function confirmDelete(work) {
  deletingWork.value    = work
  deleteDialogOpen.value = true
}

async function deleteWork() {
  deleting.value = true
  try {
    await axios.delete(`${API}/${deletingWork.value.workExperienceId}`)
    await fetchWorks()
    deleteDialogOpen.value = false
    deletingWork.value      = null
  } catch (err) {
    console.error('Failed to delete work:', err)
  } finally {
    deleting.value = false
  }
}
</script>

<style scoped>
.admin-section__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 1rem;
}

.admin-section__title {
  font-size: 1.1rem;
  font-weight: 700;
  color: rgb(var(--v-theme-on-surface));
}

.admin-section__empty {
  color: rgba(var(--v-theme-on-surface), 0.45);
  font-size: 0.875rem;
  padding: 1rem 0;
}

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

.dialog-title {
  font-size: 1.1rem;
  font-weight: 700;
  padding: 1.25rem 1.5rem 0.5rem;
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

.achievement-field { flex: 1; }

.achievements-empty {
  font-size: 0.8rem;
  color: rgba(var(--v-theme-on-surface), 0.35);
  font-style: italic;
  margin: 0;
}
</style>
