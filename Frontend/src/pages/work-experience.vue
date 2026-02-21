<template>
  <div class="we-page">

    <!-- ── Page header ─────────────────────────────────────────────────── -->
    <div class="we-header page-header-animate">
      <h1 class="we-title">Work Experience</h1>
      <p class="we-subtitle">
        The roles, teams, and challenges that shaped how I build software.
      </p>
    </div>

    <!-- ── Stats bar ───────────────────────────────────────────────────── -->
    <div v-if="!store.loading && stats" class="stats-bar page-header-animate" aria-label="Career summary statistics">
      <div class="stat-item" v-for="stat in stats" :key="stat.label">
        <span class="stat-item__value">{{ stat.value }}</span>
        <span class="stat-item__label">{{ stat.label }}</span>
      </div>
    </div>

    <!-- ── Skeleton ─────────────────────────────────────────────────────── -->
    <div v-if="store.loading" class="timeline" aria-busy="true" aria-label="Loading work experience">
      <div v-for="n in 3" :key="n" class="timeline-item"
        :class="n % 2 === 0 ? 'timeline-item--right' : 'timeline-item--left'">
        <div class="timeline-dot timeline-dot--skeleton" aria-hidden="true" />
        <div class="timeline-card skeleton-card">
          <div class="skeleton-line" style="width: 55%; height: 20px; margin-bottom: 0.5rem" />
          <div class="skeleton-line" style="width: 38%; height: 13px; margin-bottom: 0.3rem" />
          <div class="skeleton-line" style="width: 25%; height: 13px; margin-bottom: 1.25rem" />
          <div class="skeleton-line" style="width: 100%; height: 11px; margin-bottom: 0.4rem" />
          <div class="skeleton-line" style="width: 100%; height: 11px; margin-bottom: 0.4rem" />
          <div class="skeleton-line" style="width: 70%; height: 11px" />
        </div>
      </div>
    </div>

    <!-- ── Error ────────────────────────────────────────────────────────── -->
    <v-alert v-else-if="store.error" type="error" variant="tonal" class="mx-6 mt-4">
      {{ store.error }}
    </v-alert>

    <!-- ── Timeline ─────────────────────────────────────────────────────── -->
    <div v-else class="timeline" role="list" aria-label="Work experience timeline">

      <!-- Spine line — grows as entries are revealed -->
      <div class="timeline-spine" aria-hidden="true">
        <div class="timeline-spine__fill" :style="{ height: spineHeight }" />
      </div>

      <div v-for="(job, i) in store.work" :key="job.workExperienceId" class="timeline-item" :class="[
        i % 2 === 0 ? 'timeline-item--left' : 'timeline-item--right',
        'card-animate',
        { 'card-animate--visible': visibleCards.has(job.workExperienceId) }
      ]" :data-job-id="job.workExperienceId" :style="{ transitionDelay: `${Math.min(i * 60, 240)}ms` }"
        role="listitem">
        <!-- Spine dot -->
        <div class="timeline-dot" :class="{ 'timeline-dot--active': visibleCards.has(job.workExperienceId) }"
          aria-hidden="true">
          <div class="timeline-dot__ring" />
        </div>

        <!-- Card -->
        <article class="timeline-card" :aria-label="`${job.title} at ${job.employer}`">

          <!-- Year badge -->
          <div class="timeline-card__year-badge" aria-hidden="true">
            {{ job.startYear ?? '?' }}{{ job.endYear ? '–' + job.endYear : '–Present' }}
          </div>

          <div class="timeline-card__header">
            <h2 class="timeline-card__title">{{ job.title }}</h2>
            <p class="timeline-card__employer">
              <v-icon size="14" class="mr-1" aria-hidden="true">mdi-domain</v-icon>
              {{ job.employer }}
            </p>
            <p class="timeline-card__years">
              <v-icon size="13" class="mr-1" aria-hidden="true">mdi-calendar-range</v-icon>
              {{ job.startYear ?? '?' }}
              {{ job.endYear ? '– ' + job.endYear : '– Present' }}
              <span v-if="durationLabel(job)" class="timeline-card__duration">
                · {{ durationLabel(job) }}
              </span>
            </p>
          </div>

          <p v-if="job.summary" class="timeline-card__summary">{{ job.summary }}</p>

          <!-- Achievements -->
          <div v-if="job.achievements?.length" class="timeline-card__achievements">
            <button class="achievements-toggle" :aria-expanded="expandedJobs.has(job.workExperienceId)"
              :aria-controls="`achievements-${job.workExperienceId}`" @click="toggleAchievements(job.workExperienceId)">
              <v-icon size="15" class="mr-1" aria-hidden="true">
                {{ expandedJobs.has(job.workExperienceId) ? 'mdi-chevron-up' : 'mdi-chevron-down' }}
              </v-icon>
              {{ expandedJobs.has(job.workExperienceId) ? 'Hide' : 'Show' }}
              {{ job.achievements.length }} achievement{{ job.achievements.length !== 1 ? 's' : '' }}
            </button>

            <div :id="`achievements-${job.workExperienceId}`" class="achievements-list"
              :class="{ 'achievements-list--open': expandedJobs.has(job.workExperienceId) }">
              <ul>
                <li v-for="(achievement, ai) in job.achievements" :key="ai" class="achievement-item"
                  :style="{ transitionDelay: expandedJobs.has(job.workExperienceId) ? `${ai * 50}ms` : '0ms' }"
                  :class="{ 'achievement-item--visible': expandedJobs.has(job.workExperienceId) }">
                  <span class="achievement-item__bullet" aria-hidden="true">◈</span>
                  {{ achievement }}
                </li>
              </ul>
            </div>
          </div>

          <!-- Linked projects -->
          <div v-if="projectsByJob.get(job.workExperienceId)?.length" class="timeline-card__projects">
            <div class="timeline-card__projects-label" aria-label="Projects from this role">
              <v-icon size="13" aria-hidden="true" class="mr-1">mdi-briefcase-outline</v-icon>
              Projects
            </div>
            <div class="timeline-card__project-chips">
              <button v-for="project in projectsByJob.get(job.workExperienceId)" :key="project.projectId"
                class="project-chip" :class="{ 'project-chip--featured': project.isFeatured }"
                :aria-label="`View project: ${project.title}`" @click="navigateToProject(project)">
                <v-icon v-if="project.isFeatured" size="11" class="mr-1" aria-hidden="true">
                  mdi-star
                </v-icon>
                {{ project.title }}
              </button>
            </div>
          </div>

          <!-- Current role badge -->
          <div v-if="!job.endYear" class="current-badge" aria-label="Current role">
            <span class="current-badge__dot" aria-hidden="true" />
            Current Role
          </div>

        </article>
      </div>

      <!-- Timeline cap -->
      <div class="timeline-cap" aria-hidden="true">
        <v-icon size="20" color="secondary">mdi-map-marker</v-icon>
      </div>

    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onBeforeUnmount, nextTick } from 'vue'
import { useWorkExperienceStore } from '@/stores/workExperienceStore'
import { useProjectStore } from '@/stores/projectStore'
import { useRouter } from 'vue-router'

const store = useWorkExperienceStore()
const projectStore = useProjectStore()
const router = useRouter()

// ── Projects by job ────────────────────────────────────────────────────
const projectsByJob = computed(() => {
  const map = new Map()
  for (const p of projectStore.projects) {
    if (!p.workExperienceId) continue
    if (!map.has(p.workExperienceId)) map.set(p.workExperienceId, [])
    map.get(p.workExperienceId).push(p)
  }
  return map
})

function navigateToProject(project) {
  router.push(`/projects?highlight=${encodeURIComponent(project.title)}`)
}

// ── Stats ──────────────────────────────────────────────────────────────
const stats = computed(() => {
  if (!store.work.length) return null

  const totalAchievements = store.work.reduce((s, j) => s + (j.achievements?.length ?? 0), 0)

  // Calculate total years of experience
  const allYears = store.work.map(j => {
    const start = parseInt(j.startYear ?? '0') || 0
    const end = parseInt(j.endYear ?? String(new Date().getFullYear())) || new Date().getFullYear()
    return { start, end }
  })
  const minStart = Math.min(...allYears.map(y => y.start).filter(y => y > 0))
  const totalYears = new Date().getFullYear() - minStart

  return [
    { label: 'Years Experience', value: `${totalYears}+` },
    { label: 'Employers', value: store.work.length },
    { label: 'Achievements', value: totalAchievements },
  ]
})

// ── Duration label ─────────────────────────────────────────────────────
function durationLabel(job) {
  const start = parseInt(job.startYear ?? '0')
  if (!start) return null
  const end = parseInt(job.endYear ?? String(new Date().getFullYear()))
  const years = end - start
  if (years <= 0) return null
  return years === 1 ? '1 yr' : `${years} yrs`
}

// ── Expanded achievements ──────────────────────────────────────────────
const expandedJobs = ref(new Set())

function toggleAchievements(id) {
  const s = new Set(expandedJobs.value)
  if (s.has(id)) s.delete(id)
  else s.add(id)
  expandedJobs.value = s
}

// ── Intersection observer ──────────────────────────────────────────────
const visibleCards = ref(new Set())
const spineHeight = ref('0px')
let observer = null

function createObserver() {
  observer = new IntersectionObserver(
    (entries) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting) {
          const id = entry.target.dataset.jobId
          if (id) {
            visibleCards.value = new Set([...visibleCards.value, id])
            // Grow the spine to cover all visible cards
            nextTick(updateSpineHeight)
            observer.unobserve(entry.target)
          }
        }
      })
    },
    { threshold: 0.15, rootMargin: '0px 0px 40px 0px' }
  )
}

function observeCards() {
  nextTick(() => {
    document.querySelectorAll('[data-job-id]').forEach(el => observer?.observe(el))
  })
}

function updateSpineHeight() {
  const items = [...document.querySelectorAll('[data-job-id]')]
  const visible = items.filter(el => visibleCards.value.has(el.dataset.jobId))
  if (!visible.length) return

  const spineEl = document.querySelector('.timeline-spine')
  if (!spineEl) return

  const spineTop = spineEl.getBoundingClientRect().top + window.scrollY
  const lastCard = visible[visible.length - 1]
  const lastDot = lastCard.querySelector('.timeline-dot')
  if (!lastDot) return

  const dotCenter = lastDot.getBoundingClientRect().top + window.scrollY + 10
  spineHeight.value = `${Math.max(dotCenter - spineTop, 0)}px`
}

onMounted(async () => {
  createObserver()
  await Promise.all([
    store.work.length ? Promise.resolve() : store.fetchWork(),
    projectStore.projects.length ? Promise.resolve() : projectStore.fetchProjects(),
  ])
  observeCards()
})

onBeforeUnmount(() => {
  observer?.disconnect()
})


</script>

<style scoped>
/* ── Base ─────────────────────────────────────────────────────────────── */
.we-page {
  padding: 0 0 6rem;
  max-width: 1100px;
  margin: 0 auto;
  font-family: 'Raleway', sans-serif;
}

/* ── Header ───────────────────────────────────────────────────────────── */
.we-header {
  padding: 3rem 2rem 1.5rem;
}

.we-title {
  font-family: 'Patua One', serif;
  font-size: clamp(2rem, 4vw, 3rem);
  font-weight: 700;
  margin: 0 0 0.5rem;
  color: rgb(var(--v-theme-on-background));
  line-height: 1.1;
}

.we-subtitle {
  font-size: 1rem;
  color: rgba(var(--v-theme-on-background), 0.6);
  margin: 0;
  max-width: 520px;
}

/* ── Stats bar ────────────────────────────────────────────────────────── */
.stats-bar {
  display: flex;
  gap: 0;
  margin: 0 2rem 2.5rem;
  background: rgb(var(--v-theme-surface));
  border: 1px solid rgba(var(--v-theme-secondary), 0.2);
  border-radius: 12px;
  overflow: hidden;
  animation-delay: 0.08s;
}

.stat-item {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 1.1rem 1rem;
  border-right: 1px solid rgba(var(--v-theme-secondary), 0.12);
  gap: 0.2rem;
}

.stat-item:last-child {
  border-right: none;
}

.stat-item__value {
  font-family: 'Patua One', serif;
  font-size: 1.6rem;
  color: rgb(var(--v-theme-secondary));
  line-height: 1;
}

.stat-item__label {
  font-size: 0.68rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: rgba(var(--v-theme-on-surface), 0.45);
}

/* ── Header entrance ──────────────────────────────────────────────────── */
@keyframes fade-up {
  from {
    opacity: 0;
    transform: translateY(16px);
  }

  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.page-header-animate {
  animation: fade-up 0.5s ease both;
}

@media (prefers-reduced-motion: reduce) {
  .page-header-animate {
    animation: none;
  }
}

/* ── Timeline wrapper ─────────────────────────────────────────────────── */
.timeline {
  position: relative;
  padding: 0 2rem 2rem;
  display: flex;
  flex-direction: column;
  gap: 3rem;
}

/* ── Spine ────────────────────────────────────────────────────────────── */
.timeline-spine {
  position: absolute;
  left: 50%;
  top: 0;
  bottom: 0;
  transform: translateX(-50%);
  width: 2px;
  background: rgba(var(--v-theme-secondary), 0.1);
  border-radius: 2px;
  pointer-events: none;
}

.timeline-spine__fill {
  width: 100%;
  background: linear-gradient(to bottom,
      rgb(var(--v-theme-secondary)),
      rgba(var(--v-theme-secondary), 0.4));
  border-radius: 2px;
  transition: height 0.6s cubic-bezier(0.4, 0, 0.2, 1);
}

/* ── Timeline items ───────────────────────────────────────────────────── */
.timeline-item {
  display: flex;
  align-items: flex-start;
  position: relative;
  width: 100%;
}

.timeline-item--left {
  flex-direction: row;
  padding-right: calc(50% + 2rem);
}

.timeline-item--right {
  flex-direction: row-reverse;
  padding-left: calc(50% + 2rem);
}

/* ── Spine dot ────────────────────────────────────────────────────────── */
.timeline-dot {
  position: absolute;
  left: 50%;
  top: 1.4rem;
  transform: translateX(-50%);
  width: 14px;
  height: 14px;
  border-radius: 50%;
  background: rgb(var(--v-theme-surface));
  border: 2px solid rgba(var(--v-theme-secondary), 0.3);
  z-index: 2;
  transition: border-color 0.3s ease, box-shadow 0.3s ease, transform 0.3s ease;
  flex-shrink: 0;
}

.timeline-dot__ring {
  position: absolute;
  inset: -5px;
  border-radius: 50%;
  border: 1.5px solid transparent;
  transition: border-color 0.3s ease, opacity 0.3s ease;
  opacity: 0;
}

.timeline-dot--active {
  border-color: rgb(var(--v-theme-secondary));
  box-shadow: 0 0 0 3px rgba(var(--v-theme-secondary), 0.15),
    0 0 12px rgba(var(--v-theme-secondary), 0.3);
  transform: translateX(-50%) scale(1.2);
  background: rgb(var(--v-theme-secondary));
}

.timeline-dot--active .timeline-dot__ring {
  border-color: rgba(var(--v-theme-secondary), 0.3);
  opacity: 1;
}

.timeline-dot--skeleton {
  border-color: rgba(255, 255, 255, 0.08);
  background: rgba(255, 255, 255, 0.04);
}

/* ── Card entrance animation ──────────────────────────────────────────── */
.card-animate {
  opacity: 0;
  transition: opacity 0.5s ease, transform 0.5s ease;
}

.timeline-item--left.card-animate {
  transform: translateX(-20px);
}

.timeline-item--right.card-animate {
  transform: translateX(20px);
}

.card-animate--visible {
  opacity: 1;
  transform: translateX(0) !important;
}

@media (prefers-reduced-motion: reduce) {
  .card-animate {
    opacity: 1;
    transform: none !important;
    transition: none;
  }
}

/* ── Timeline card ────────────────────────────────────────────────────── */
.timeline-card {
  flex: 1;
  background: rgb(var(--v-theme-surface));
  border: 1px solid rgba(255, 255, 255, 0.07);
  border-radius: 14px;
  padding: 1.5rem;
  position: relative;
  transition: box-shadow 0.2s ease, border-color 0.2s ease, transform 0.2s ease;
}

.timeline-card:hover {
  border-color: rgba(var(--v-theme-secondary), 0.3);
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.25),
    0 0 0 1px rgba(var(--v-theme-secondary), 0.1);
  transform: translateY(-2px);
}

/* Pointer arrow toward spine — left card points right */
.timeline-item--left .timeline-card::after {
  content: '';
  position: absolute;
  right: -8px;
  top: 1.8rem;
  width: 0;
  height: 0;
  border-top: 7px solid transparent;
  border-bottom: 7px solid transparent;
  border-left: 8px solid rgb(var(--v-theme-surface));
  filter: drop-shadow(1px 0 0 rgba(255, 255, 255, 0.07));
}

/* Right card points left */
.timeline-item--right .timeline-card::after {
  content: '';
  position: absolute;
  left: -8px;
  top: 1.8rem;
  width: 0;
  height: 0;
  border-top: 7px solid transparent;
  border-bottom: 7px solid transparent;
  border-right: 8px solid rgb(var(--v-theme-surface));
  filter: drop-shadow(-1px 0 0 rgba(255, 255, 255, 0.07));
}

/* ── Year badge ───────────────────────────────────────────────────────── */
.timeline-card__year-badge {
  display: inline-flex;
  align-items: center;
  font-size: 0.68rem;
  font-weight: 700;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: rgb(var(--v-theme-secondary));
  background: rgba(var(--v-theme-secondary), 0.08);
  border: 1px solid rgba(var(--v-theme-secondary), 0.2);
  border-radius: 20px;
  padding: 0.2rem 0.65rem;
  margin-bottom: 0.85rem;
}

/* ── Card header ──────────────────────────────────────────────────────── */
.timeline-card__header {
  margin-bottom: 0.75rem;
}

.timeline-card__title {
  font-family: 'Patua One', serif;
  font-size: 1.15rem;
  font-weight: 700;
  color: rgb(var(--v-theme-on-surface));
  margin: 0 0 0.3rem;
  line-height: 1.25;
}

.timeline-card__employer {
  font-size: 0.82rem;
  color: rgba(var(--v-theme-on-surface), 0.65);
  margin: 0 0 0.2rem;
  display: flex;
  align-items: center;
}

.timeline-card__years {
  font-size: 0.76rem;
  color: rgba(var(--v-theme-on-surface), 0.45);
  margin: 0;
  display: flex;
  align-items: center;
}

.timeline-card__duration {
  color: rgba(var(--v-theme-secondary), 0.7);
  margin-left: 0.1rem;
}

/* ── Summary ──────────────────────────────────────────────────────────── */
.timeline-card__summary {
  font-size: 0.875rem;
  line-height: 1.7;
  color: rgba(var(--v-theme-on-surface), 0.75);
  margin: 0 0 1rem;
}

/* ── Achievements ─────────────────────────────────────────────────────── */
.timeline-card__achievements {
  margin-top: 0.5rem;
}

.achievements-toggle {
  display: inline-flex;
  align-items: center;
  background: none;
  border: 1px solid rgba(var(--v-theme-secondary), 0.25);
  border-radius: 6px;
  padding: 0.3rem 0.7rem;
  font-size: 0.75rem;
  font-weight: 700;
  font-family: 'Raleway', sans-serif;
  color: rgb(var(--v-theme-secondary));
  cursor: pointer;
  transition: background 0.15s, border-color 0.15s;
  outline: none;
  letter-spacing: 0.02em;
  margin-bottom: 0.5rem;
}

.achievements-toggle:hover {
  background: rgba(var(--v-theme-secondary), 0.08);
  border-color: rgba(var(--v-theme-secondary), 0.5);
}

.achievements-toggle:focus-visible {
  outline: 2px solid rgb(var(--v-theme-secondary));
  outline-offset: 2px;
}

.achievements-list {
  max-height: 0;
  overflow: hidden;
  transition: max-height 0.4s cubic-bezier(0.4, 0, 0.2, 1);
}

.achievements-list--open {
  max-height: 800px;
}

.achievements-list ul {
  list-style: none;
  padding: 0.25rem 0 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.achievement-item {
  display: flex;
  align-items: flex-start;
  gap: 0.6rem;
  font-size: 0.84rem;
  line-height: 1.6;
  color: rgba(var(--v-theme-on-surface), 0.8);
  opacity: 0;
  transform: translateX(-8px);
  transition: opacity 0.3s ease, transform 0.3s ease;
}

.achievement-item--visible {
  opacity: 1;
  transform: translateX(0);
}

.achievement-item__bullet {
  color: rgb(var(--v-theme-secondary));
  font-size: 0.7rem;
  margin-top: 0.3rem;
  flex-shrink: 0;
}

/* ── Current role badge ───────────────────────────────────────────────── */
.current-badge {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  margin-top: 1rem;
  font-size: 0.72rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.07em;
  color: rgb(var(--v-theme-success));
}

.current-badge__dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: rgb(var(--v-theme-success));
  animation: pulse-dot 2s ease-in-out infinite;
  flex-shrink: 0;
}

@keyframes pulse-dot {

  0%,
  100% {
    box-shadow: 0 0 0 0 rgba(var(--v-theme-success), 0.5);
  }

  50% {
    box-shadow: 0 0 0 5px rgba(var(--v-theme-success), 0);
  }
}

@media (prefers-reduced-motion: reduce) {
  .current-badge__dot {
    animation: none;
  }
}

/* ── Timeline cap ─────────────────────────────────────────────────────── */
.timeline-cap {
  display: flex;
  justify-content: center;
  margin-top: 1rem;
  opacity: 0.5;
}

/* ── Skeleton ─────────────────────────────────────────────────────────── */
@keyframes shimmer {
  0% {
    background-position: -600px 0;
  }

  100% {
    background-position: 600px 0;
  }
}

.skeleton-line {
  border-radius: 4px;
  background: linear-gradient(90deg,
      rgba(255, 255, 255, 0.04) 25%,
      rgba(255, 255, 255, 0.1) 50%,
      rgba(255, 255, 255, 0.04) 75%);
  background-size: 1200px 100%;
  animation: shimmer 1.8s infinite linear;
}

.skeleton-card {
  padding: 1.5rem;
}

/* ── Responsive ───────────────────────────────────────────────────────── */
@media (max-width: 768px) {
  .we-header {
    padding: 2rem 1.25rem 1.25rem;
  }

  .stats-bar {
    margin: 0 1.25rem 2rem;
  }

  .timeline {
    padding: 0 1.25rem 2rem;
    gap: 2rem;
  }

  /* Single column on mobile — spine moves to left edge */
  .timeline-spine {
    left: 1.75rem;
    transform: none;
  }

  .timeline-item--left,
  .timeline-item--right {
    flex-direction: row;
    padding-right: 0;
    padding-left: 3.5rem;
  }

  .timeline-dot {
    left: 1.75rem;
    transform: translateX(-50%);
  }

  .timeline-dot--active {
    transform: translateX(-50%) scale(1.2);
  }

  /* Both arrows point left on mobile */
  .timeline-item--left .timeline-card::after,
  .timeline-item--right .timeline-card::after {
    left: -8px;
    right: auto;
    border-left: none;
    border-right: 8px solid rgb(var(--v-theme-surface));
    border-top: 7px solid transparent;
    border-bottom: 7px solid transparent;
    filter: drop-shadow(-1px 0 0 rgba(255, 255, 255, 0.07));
  }

  /* Slide all cards from left on mobile */
  .timeline-item--left.card-animate,
  .timeline-item--right.card-animate {
    transform: translateX(-12px);
  }

  .timeline-cap {
    justify-content: flex-start;
    padding-left: 1.1rem;
  }

  .stat-item__value {
    font-size: 1.25rem;
  }
}

@media (max-width: 479px) {
  .stats-bar {
    flex-wrap: wrap;
  }

  .stat-item {
    flex: 1 1 33%;
    border-bottom: 1px solid rgba(var(--v-theme-secondary), 0.12);
  }
}

/* ── Linked project chips ─────────────────────────────────────────────── */
.timeline-card__projects {
  margin-top: 1rem;
  padding-top: 0.85rem;
  border-top: 1px solid rgba(var(--v-theme-secondary), 0.1);
}

.timeline-card__projects-label {
  font-size: 0.65rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: rgba(var(--v-theme-on-surface), 0.35);
  display: flex;
  align-items: center;
  margin-bottom: 0.5rem;
}

.timeline-card__project-chips {
  display: flex;
  flex-wrap: wrap;
  gap: 0.35rem;
}

.project-chip {
  display: inline-flex;
  align-items: center;
  padding: 0.22rem 0.65rem;
  border-radius: 20px;
  font-size: 0.7rem;
  font-weight: 600;
  font-family: 'Raleway', sans-serif;
  background: rgba(var(--v-theme-secondary), 0.07);
  color: rgba(var(--v-theme-secondary), 0.8);
  border: 1px solid rgba(var(--v-theme-secondary), 0.18);
  cursor: pointer;
  transition: background 0.15s, border-color 0.15s,
              color 0.15s, transform 0.12s ease;
  outline: none;
  letter-spacing: 0.02em;
}

.project-chip:hover {
  background: rgba(var(--v-theme-secondary), 0.15);
  border-color: rgba(var(--v-theme-secondary), 0.4);
  color: rgb(var(--v-theme-secondary));
  transform: translateY(-1px);
}

.project-chip:focus-visible {
  outline: 2px solid rgb(var(--v-theme-secondary));
  outline-offset: 2px;
}

/* Featured projects get a subtle gold accent */
.project-chip--featured {
  background: rgba(var(--v-theme-yellow, var(--v-theme-secondary)), 0.08);
  border-color: rgba(var(--v-theme-yellow, var(--v-theme-secondary)), 0.25);
  color: rgba(var(--v-theme-yellow, var(--v-theme-secondary)), 0.85);
}

.project-chip--featured:hover {
  background: rgba(var(--v-theme-yellow, var(--v-theme-secondary)), 0.15);
  border-color: rgba(var(--v-theme-yellow, var(--v-theme-secondary)), 0.45);
  color: rgb(var(--v-theme-yellow, var(--v-theme-secondary)));
}
</style>