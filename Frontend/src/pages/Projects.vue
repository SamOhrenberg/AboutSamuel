<template>
  <div class="projects-page">
    <!-- Page header -->
    <div class="projects-header">
      <h1 class="projects-title">Projects</h1>
      <p class="projects-subtitle">
        A selection of the work I'm most proud of — from enterprise integrations to personal builds.
      </p>
    </div>

    <!-- Tech filter chips -->
    <div v-if="!store.loading && store.projects.length" class="filter-bar">
      <v-chip
        class="filter-chip"
        :color="selectedTag === null ? 'secondary' : undefined"
        :variant="selectedTag === null ? 'flat' : 'outlined'"
        size="small"
        clickable
        @click="selectedTag = null"
      >
        All
      </v-chip>
      <v-chip
        v-for="tag in allTags"
        :key="tag"
        class="filter-chip"
        :color="selectedTag === tag ? 'secondary' : undefined"
        :variant="selectedTag === tag ? 'flat' : 'outlined'"
        size="small"
        clickable
        @click="selectedTag = selectedTag === tag ? null : tag"
      >
        {{ tag }}
      </v-chip>
    </div>

    <!-- Loading -->
    <div v-if="store.loading" class="loading-state">
      <v-progress-circular indeterminate color="secondary" size="48" />
      <p class="loading-text">Loading projects...</p>
    </div>

    <!-- Error -->
    <v-alert v-else-if="store.error" type="error" variant="tonal" class="mx-6 mb-6">
      {{ store.error }}
    </v-alert>

    <!-- Projects grid -->
    <div v-else class="projects-grid">
      <v-card
        v-for="project in filteredProjects"
        :key="project.projectId"
        class="project-card"
        :class="{ 'project-card--featured': project.isFeatured }"
        elevation="0"
      >
        <!-- Featured badge -->
        <v-chip
          v-if="project.isFeatured"
          color="yellow"
          size="x-small"
          class="featured-badge"
        >
          ★ Featured
        </v-chip>

        <div class="project-card__body">
          <div class="project-card__header">
            <h2 class="project-card__title">{{ project.title }}</h2>
            <p class="project-card__meta">
              {{ project.employer }}
              <span v-if="project.startYear" class="project-card__dates">
                · {{ project.startYear }}{{ project.endYear ? '–' + project.endYear : '–Present' }}
              </span>
            </p>
            <p v-if="project.role" class="project-card__role">{{ project.role }}</p>
          </div>

          <p class="project-card__summary">{{ project.summary }}</p>

          <!-- Tech stack -->
          <div class="project-card__stack">
            <v-chip
              v-for="tech in project.techStack"
              :key="tech"
              size="x-small"
              variant="tonal"
              color="secondary"
              class="stack-chip"
            >
              {{ tech }}
            </v-chip>
          </div>
        </div>

        <!-- Detail expand -->
        <template v-if="project.detail">
          <v-divider class="mx-4" />
          <v-card-actions class="project-card__actions">
            <v-btn
              variant="text"
              color="secondary"
              size="small"
              :append-icon="expandedId === project.projectId ? 'mdi-chevron-up' : 'mdi-chevron-down'"
              @click="toggle(project.projectId)"
            >
              {{ expandedId === project.projectId ? 'Less detail' : 'More detail' }}
            </v-btn>
          </v-card-actions>

          <v-expand-transition>
            <div v-if="expandedId === project.projectId">
              <v-divider />
              <div class="project-card__detail">
                <p>{{ project.detail }}</p>
              </div>
            </div>
          </v-expand-transition>
        </template>
      </v-card>

      <!-- Empty filtered state -->
      <div v-if="filteredProjects.length === 0" class="empty-state">
        <v-icon size="48" color="secondary" class="mb-3">mdi-filter-off-outline</v-icon>
        <p class="empty-state__text">No projects match that filter.</p>
        <v-btn variant="text" color="secondary" @click="selectedTag = null">Clear filter</v-btn>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useProjectStore } from '@/stores/projectStore'

const store = useProjectStore()
const expandedId = ref(null)
const selectedTag = ref(null)

onMounted(() => {
  if (!store.projects.length) store.fetchProjects()
})

function toggle(id) {
  expandedId.value = expandedId.value === id ? null : id
}

const allTags = computed(() => {
  const tags = new Set()
  store.projects.forEach((p) => p.techStack?.forEach((t) => tags.add(t)))
  return [...tags].sort()
})

const filteredProjects = computed(() => {
  if (!selectedTag.value) return store.projects
  return store.projects.filter((p) => p.techStack?.includes(selectedTag.value))
})
</script>

<style scoped>
.projects-page {
  padding: 3rem 2rem 4rem;
  max-width: 1200px;
  margin: 0 auto;
  font-family: 'Raleway', sans-serif;
}

/* ── Header ── */
.projects-header {
  margin-bottom: 2.5rem;
}

.projects-title {
  font-family: 'Patua One', serif;
  font-size: clamp(2rem, 4vw, 3rem);
  font-weight: 700;
  margin: 0 0 0.5rem;
  color: rgb(var(--v-theme-on-background));
}

.projects-subtitle {
  font-size: 1rem;
  color: rgba(var(--v-theme-on-background), 0.65);
  margin: 0;
  max-width: 560px;
}

/* ── Filter bar ── */
.filter-bar {
  display: flex;
  flex-wrap: wrap;
  gap: 0.4rem;
  margin-bottom: 2rem;
}

.filter-chip {
  cursor: pointer;
}

/* ── Loading ── */
.loading-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 5rem 0;
  gap: 1rem;
}

.loading-text {
  color: rgba(var(--v-theme-on-background), 0.6);
  font-size: 0.9rem;
}

/* ── Grid ── */
.projects-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
  gap: 1.5rem;
  align-items: start;
}

@media (max-width: 599px) {
  .projects-grid {
    grid-template-columns: 1fr;
  }
  .projects-page {
    padding: 2rem 1rem 3rem;
  }
}

/* ── Card ── */
.project-card {
  background-color: rgb(var(--v-theme-surface)) !important;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 12px !important;
  transition: transform 0.15s ease, box-shadow 0.15s ease, border-color 0.15s ease;
  position: relative;
  overflow: visible !important;
}

.project-card:hover {
  transform: translateY(-3px);
  box-shadow: 0 8px 28px rgba(0, 0, 0, 0.35) !important;
  border-color: rgba(var(--v-theme-secondary), 0.4);
}

.project-card--featured {
  border-color: rgba(var(--v-theme-secondary), 0.5);
  box-shadow: 0 0 0 1px rgba(var(--v-theme-secondary), 0.2) !important;
}

/* ── Featured badge ── */
.featured-badge {
  position: absolute;
  top: 12px;
  right: 12px;
  z-index: 1;
  font-weight: 600;
  letter-spacing: 0.03em;
}

/* ── Card body ── */
.project-card__body {
  padding: 1.5rem 1.5rem 1rem;
}

.project-card--featured .project-card__body {
  padding-top: 2.75rem; /* make room for the badge */
}

.project-card__header {
  margin-bottom: 0.75rem;
}

.project-card__title {
  font-family: 'Patua One', serif;
  font-size: 1.15rem;
  font-weight: 600;
  line-height: 1.3;
  margin: 0 0 0.25rem;
  color: rgb(var(--v-theme-on-surface));
}

.project-card__meta {
  font-size: 0.8rem;
  color: rgba(var(--v-theme-on-surface), 0.55);
  margin: 0 0 0.2rem;
}

.project-card__dates {
  opacity: 0.8;
}

.project-card__role {
  font-size: 0.8rem;
  color: rgb(var(--v-theme-secondary));
  font-weight: 600;
  margin: 0;
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.project-card__summary {
  font-size: 0.875rem;
  line-height: 1.65;
  color: rgba(var(--v-theme-on-surface), 0.8);
  margin: 0 0 1rem;
}

/* ── Tech stack chips ── */
.project-card__stack {
  display: flex;
  flex-wrap: wrap;
  gap: 0.3rem;
}

.stack-chip {
  font-size: 0.7rem !important;
}

/* ── Actions ── */
.project-card__actions {
  padding: 0.25rem 0.75rem !important;
}

/* ── Detail expand ── */
.project-card__detail {
  padding: 1rem 1.5rem 1.25rem;
  font-size: 0.875rem;
  line-height: 1.65;
  color: rgba(var(--v-theme-on-surface), 0.75);
}

/* ── Empty state ── */
.empty-state {
  grid-column: 1 / -1;
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 5rem 0;
  color: rgba(var(--v-theme-on-background), 0.5);
}

.empty-state__text {
  font-size: 1rem;
  margin-bottom: 0.5rem;
}
</style>