<template>
  <div class="projects-page">
    <div class="projects-header">
      <h1 class="projects-title">Projects</h1>
      <p class="projects-subtitle">
        A selection of the work I'm most proud of — from enterprise integrations to personal builds.
      </p>
    </div>

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

    <!-- Skeleton loaders — shown while API responds -->
    <div v-if="store.loading" class="projects-grid">
      <div v-for="n in 6" :key="n" class="skeleton-card">
        <div class="skeleton-card__body">
          <div class="skeleton-line skeleton-line--title" />
          <div class="skeleton-line skeleton-line--meta" />
          <div class="skeleton-line skeleton-line--role" />
          <div class="skeleton-line skeleton-line--text" />
          <div class="skeleton-line skeleton-line--text" />
          <div class="skeleton-line skeleton-line--text skeleton-line--short" />
          <div class="skeleton-chips">
            <div v-for="c in 3" :key="c" class="skeleton-chip" />
          </div>
        </div>
      </div>
    </div>

    <v-alert v-else-if="store.error" type="error" variant="tonal" class="mx-6 mb-6">
      {{ store.error }}
    </v-alert>

    <div v-else class="projects-grid">
      <v-card
        v-for="(project, i) in filteredProjects"
        :key="project.projectId"
        class="project-card"
        :class="{
          'project-card--featured': project.isFeatured,
          'card-animate': true,
          'card-animate--visible': visibleCards.has(project.projectId),
        }"
        :style="{ transitionDelay: `${(i % 3) * 80}ms` }"
        :data-project-id="project.projectId"
        elevation="0"
      >
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

      <div v-if="filteredProjects.length === 0" class="empty-state">
        <v-icon size="48" color="secondary" class="mb-3">mdi-filter-off-outline</v-icon>
        <p class="empty-state__text">No projects match that filter.</p>
        <v-btn variant="text" color="secondary" @click="selectedTag = null">Clear filter</v-btn>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onBeforeUnmount, nextTick } from 'vue'
import { useProjectStore } from '@/stores/projectStore'

const store = useProjectStore()
const expandedId = ref(null)
const selectedTag = ref(null)
const visibleCards = ref(new Set())

let observer = null

function createObserver() {
  if (observer) observer.disconnect()
  observer = new IntersectionObserver(
    (entries) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting) {
          const id = entry.target.dataset.projectId
          if (id) {
            visibleCards.value = new Set([...visibleCards.value, id])
            observer.unobserve(entry.target)
          }
        }
      })
    },
    { threshold: 0.05, rootMargin: '0px 0px 50px 0px' }
  )
}

function observeCards() {
  nextTick(() => {
    const cards = document.querySelectorAll('[data-project-id]')
    cards.forEach(el => observer?.observe(el))
  })
}

onMounted(async () => {
  createObserver()
  if (!store.projects.length) {
    await store.fetchProjects()
  }
  observeCards()
})

onBeforeUnmount(() => {
  observer?.disconnect()
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

.filter-bar {
  display: flex;
  flex-wrap: wrap;
  gap: 0.4rem;
  margin-bottom: 2rem;
}

.filter-chip { cursor: pointer; }

/* ── Skeleton loaders ── */
@keyframes shimmer {
  0%   { background-position: -400px 0; }
  100% { background-position:  400px 0; }
}

.skeleton-card {
  background-color: rgb(var(--v-theme-surface));
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 12px;
}

.skeleton-card__body {
  padding: 1.5rem;
}

.skeleton-line {
  border-radius: 4px;
  margin-bottom: 0.6rem;
  background: linear-gradient(
    90deg,
    rgba(255,255,255,0.05) 25%,
    rgba(255,255,255,0.12) 50%,
    rgba(255,255,255,0.05) 75%
  );
  background-size: 800px 100%;
  animation: shimmer 1.6s infinite linear;
}

.skeleton-line--title  { height: 18px; width: 70%; margin-bottom: 0.75rem; }
.skeleton-line--meta   { height: 11px; width: 45%; }
.skeleton-line--role   { height: 11px; width: 30%; margin-bottom: 0.9rem; }
.skeleton-line--text   { height: 11px; width: 100%; }
.skeleton-line--short  { width: 60%; margin-bottom: 0.9rem; }

.skeleton-chips {
  display: flex;
  gap: 0.4rem;
  margin-top: 0.5rem;
}

.skeleton-chip {
  height: 20px;
  width: 64px;
  border-radius: 10px;
  background: linear-gradient(
    90deg,
    rgba(255,255,255,0.05) 25%,
    rgba(255,255,255,0.12) 50%,
    rgba(255,255,255,0.05) 75%
  );
  background-size: 800px 100%;
  animation: shimmer 1.6s infinite linear;
}

/* ── Grid ── */
.projects-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
  gap: 1.5rem;
  align-items: start;
}

@media (max-width: 599px) {
  .projects-grid { grid-template-columns: 1fr; }
  .projects-page { padding: 2rem 1rem 3rem; }
}

/* ── Card entrance animation ── */
.card-animate {
  opacity: 0;
  transform: translateY(24px);
  transition: opacity 0.45s ease, transform 0.45s ease;
}

.card-animate--visible {
  opacity: 1;
  transform: translateY(0);
}

/* ── Card ── */
.project-card {
  background-color: rgb(var(--v-theme-surface)) !important;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 12px !important;
  transition:
    opacity 0.45s ease,
    transform 0.45s ease,
    box-shadow 0.15s ease,
    border-color 0.15s ease;
  position: relative;
  overflow: visible !important;
}

.project-card:hover {
  transform: translateY(-3px);
  box-shadow: 0 8px 28px rgba(0, 0, 0, 0.35) !important;
  border-color: rgba(var(--v-theme-secondary), 0.4);
}

/* Don't let hover override the entrance animation starting state */
.project-card.card-animate:not(.card-animate--visible):hover {
  transform: translateY(24px);
}

.project-card--featured {
  border-color: rgba(var(--v-theme-secondary), 0.5);
  box-shadow: 0 0 0 1px rgba(var(--v-theme-secondary), 0.2) !important;
}

.featured-badge {
  position: absolute;
  top: 12px;
  right: 12px;
  z-index: 1;
  font-weight: 600;
  letter-spacing: 0.03em;
}

.project-card__body {
  padding: 1.5rem 1.5rem 1rem;
}

.project-card--featured .project-card__body {
  padding-top: 2.75rem;
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

.project-card__dates { opacity: 0.8; }

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
  margin: 0.75rem 0 1rem;
}

.project-card__stack {
  display: flex;
  flex-wrap: wrap;
  gap: 0.3rem;
}

.stack-chip { font-size: 0.7rem !important; }

.project-card__actions { padding: 0.25rem 0.75rem !important; }

.project-card__detail {
  padding: 1rem 1.5rem 1.25rem;
  font-size: 0.875rem;
  line-height: 1.65;
  color: rgba(var(--v-theme-on-surface), 0.75);
}

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