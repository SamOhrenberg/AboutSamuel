<template>
  <div class="projects-page">

    <!-- ── Page header ───────────────────────────────────────────────────── -->
    <div class="projects-header page-header-animate">
      <div class="projects-header__text">
        <h1 class="projects-title">Projects</h1>
        <p class="projects-subtitle">
          A selection of the work I'm most proud of — from enterprise integrations to personal builds.
        </p>
      </div>
    </div>

    <!-- ── Tech experience summary bar ──────────────────────────────────── -->
    <div v-if="!store.loading && techExperience.length" class="tech-bar" aria-label="Technology experience summary">
      <div class="tech-bar__label">Experience by Technology</div>
      <div class="tech-bar__items">
        <div v-for="item in techExperience" :key="item.tech" class="tech-bar__item"
          :class="{ 'tech-bar__item--active': selectedTechs.includes(item.tech) }" role="button" tabindex="0"
          :aria-pressed="selectedTechs.includes(item.tech)"
          :aria-label="`${item.tech}: ${item.years} year${item.years !== 1 ? 's' : ''} across ${item.count} project${item.count !== 1 ? 's' : ''}`"
          @click="toggleTech(item.tech)" @keydown.enter.prevent="toggleTech(item.tech)"
          @keydown.space.prevent="toggleTech(item.tech)">
          <span class="tech-bar__tech">{{ item.tech }}</span>
          <span class="tech-bar__years">{{ item.years }}yr</span>
          <div class="tech-bar__fill" :style="{ width: (item.years / maxYears * 100) + '%' }" aria-hidden="true" />
        </div>
      </div>
    </div>

    <!-- ── Filter toolbar ────────────────────────────────────────────────── -->
    <div v-if="!store.loading && store.projects.length" class="filter-toolbar">
      <!-- Grouping toggle -->
      <div class="filter-toolbar__section">
        <span class="filter-toolbar__label">Group by</span>
        <div role="radiogroup" aria-label="Group projects by" class="group-toggle">
          <button v-for="opt in groupOptions" :key="opt.value" role="radio" :aria-checked="groupBy === opt.value"
            :class="['group-btn', { 'group-btn--active': groupBy === opt.value }]" @click="groupBy = opt.value">{{
              opt.label }}</button>
        </div>
      </div>

      <!-- Tech multi-select filter -->
      <div class="filter-toolbar__section filter-toolbar__section--flex" v-if="allTags.length">
        <span class="filter-toolbar__label" id="filter-label">Filter by tech</span>
        <div role="group" aria-labelledby="filter-label" class="filter-chips" ref="filterChipsEl"
          @keydown="onFilterKeydown">
          <button v-for="(tag, i) in allTags" :key="tag" role="checkbox" :aria-checked="selectedTechs.includes(tag)"
            :class="['filter-chip', { 'filter-chip--active': selectedTechs.includes(tag) }]" :data-index="i"
            :tabindex="i === focusedChipIndex ? 0 : -1" @click="toggleTech(tag)" @focus="focusedChipIndex = i">{{ tag
            }}</button>
          <button v-if="selectedTechs.length" class="filter-chip filter-chip--clear" aria-label="Clear all tech filters"
            :tabindex="focusedChipIndex === allTags.length ? 0 : -1" :data-index="allTags.length"
            @click="selectedTechs = []" @focus="focusedChipIndex = allTags.length">
            ✕ Clear
          </button>
        </div>
      </div>
    </div>

    <!-- Active filter summary -->
    <div v-if="selectedTechs.length" class="active-filters" aria-live="polite">
      <span class="active-filters__label">Showing projects with:</span>
      <span v-for="t in selectedTechs" :key="t" class="active-filter-tag">
        {{ t }}
        <button class="active-filter-tag__remove" :aria-label="`Remove ${t} filter`" @click="toggleTech(t)">✕</button>
      </span>
    </div>

    <!-- ── Skeleton loaders ───────────────────────────────────────────────── -->
    <div v-if="store.loading" class="projects-body">
      <!-- Featured strip skeleton -->
      <div class="featured-strip">
        <div class="featured-strip__title skeleton-text" style="width:180px;height:14px;margin-bottom:1rem" />
        <div class="featured-grid">
          <div v-for="n in 2" :key="n" class="featured-card skeleton-card">
            <div class="skeleton-card__body">
              <div class="skeleton-line" style="width:60%;height:20px;margin-bottom:.5rem" />
              <div class="skeleton-line" style="width:40%;height:12px;margin-bottom:.3rem" />
              <div class="skeleton-line" style="width:30%;height:12px;margin-bottom:1rem" />
              <div class="skeleton-line" style="width:100%;height:11px;margin-bottom:.4rem" />
              <div class="skeleton-line" style="width:100%;height:11px;margin-bottom:.4rem" />
              <div class="skeleton-line" style="width:70%;height:11px;margin-bottom:1rem" />
              <div class="skeleton-impact" />
              <div class="skeleton-chips">
                <div v-for="c in 4" :key="c" class="skeleton-chip" />
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Regular grid skeleton -->
      <div class="employer-group">
        <div class="skeleton-text" style="width:220px;height:16px;margin-bottom:1.25rem" />
        <div class="projects-grid">
          <div v-for="n in 4" :key="n" class="project-card skeleton-card">
            <div class="skeleton-card__body">
              <div class="skeleton-line" style="width:65%;height:16px;margin-bottom:.5rem" />
              <div class="skeleton-line" style="width:35%;height:11px;margin-bottom:.3rem" />
              <div class="skeleton-line" style="width:25%;height:11px;margin-bottom:1rem" />
              <div class="skeleton-line" style="width:100%;height:11px;margin-bottom:.35rem" />
              <div class="skeleton-line" style="width:100%;height:11px;margin-bottom:.35rem" />
              <div class="skeleton-line" style="width:55%;height:11px;margin-bottom:1rem" />
              <div class="skeleton-chips">
                <div v-for="c in 3" :key="c" class="skeleton-chip" />
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- ── Error state ────────────────────────────────────────────────────── -->
    <v-alert v-else-if="store.error" type="error" variant="tonal" class="mx-6 mb-6">
      {{ store.error }}
    </v-alert>

    <!-- ── Content ────────────────────────────────────────────────────────── -->
    <div v-else class="projects-body">

      <!-- Featured strip -->
      <div v-if="featuredProjects.length && !selectedTechs.length" class="featured-strip">
        <div class="featured-strip__header">
          <span class="featured-strip__title">
            <span class="star-icon" aria-hidden="true">★</span>
            Featured Work
          </span>
        </div>
        <div class="featured-grid">
          <div v-for="(project, i) in featuredProjects" :key="project.projectId" class="featured-card card-animate"
            :class="{ 'card-animate--visible': visibleCards.has(project.projectId) }"
            :style="{ transitionDelay: `${i === 0 ? 0 : 60 + (i - 1) * 100}ms` }" :data-project-id="project.projectId"
            role="article" :aria-label="project.title">
            <div class="featured-card__body">
              <div class="featured-card__header">
                <h2 class="featured-card__title">{{ project.title }}</h2>
                <p class="featured-card__meta">
                  <span v-if="project.employer">{{ project.employer }}</span>
                  <span v-if="project.startYear" class="project-dates">
                    {{ project.employer ? ' · ' : '' }}{{ project.startYear }}{{ project.endYear ? '–' + project.endYear
                      : '–Present' }}
                  </span>
                </p>
                <p v-if="project.role" class="featured-card__role">{{ project.role }}</p>
              </div>

              <p class="featured-card__summary">{{ project.summary }}</p>

              <div v-if="project.impactStatement" class="impact-callout impact-callout--featured">
                <span class="impact-icon" aria-hidden="true">◈</span>
                <span>{{ project.impactStatement }}</span>
              </div>

              <div class="project-card__stack">
                <span v-for="tech in project.techStack" :key="tech" class="stack-chip"
                  :class="{ 'stack-chip--highlighted': selectedTechs.includes(tech) }">{{ tech }}</span>
              </div>
            </div>

            <div class="featured-card__footer">
              <button v-if="project.detail || project.impactStatement" class="detail-btn"
                :aria-label="`View details for ${project.title}`" @click="openDetail(project)">
                View Details
                <span aria-hidden="true">→</span>
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Grouped project sections -->
      <div v-for="group in groupedProjects" :key="group.key" class="employer-group">
        <div class="employer-group__header">
          <button class="employer-group__toggle" :aria-expanded="!collapsedGroups.has(group.key)"
            :aria-controls="`group-${group.key}`" @click="toggleGroup(group.key)">
            <span class="employer-group__name">{{ group.label }}</span>
            <span class="employer-group__meta">
              {{ group.projects.length }} project{{ group.projects.length !== 1 ? 's' : '' }}
              <span v-if="groupBy === 'employer' && group.yearRange" class="employer-group__years">
                · {{ group.yearRange }}
              </span>
            </span>
            <span class="employer-group__chevron"
              :class="{ 'employer-group__chevron--collapsed': collapsedGroups.has(group.key) }"
              aria-hidden="true">›</span>
          </button>
        </div>

        <div :id="`group-${group.key}`" class="employer-group__content"
          :class="{ 'employer-group__content--collapsed': collapsedGroups.has(group.key) }">
          <div class="projects-grid">
            <div v-for="(project, i) in group.projects" :key="project.projectId" class="project-card card-animate"
              :class="{
                'card-animate--visible': visibleCards.has(project.projectId),
                'project-card--highlighted': highlightedId === project.projectId,
              }" :style="{ transitionDelay: `${Math.min(i * 45, 300)}ms` }" :data-project-id="project.projectId"
              role="article" :aria-label="project.title">
              <div class="project-card__body">
                <div class="project-card__header">
                  <h2 class="project-card__title">{{ project.title }}</h2>
                  <p class="project-card__meta">
                    <span v-if="project.employer">{{ project.employer }}</span>
                    <span v-if="project.startYear" class="project-dates">
                      {{ project.employer ? ' · ' : '' }}{{ project.startYear }}{{ project.endYear ? '–' +
                        project.endYear : '–Present' }}
                    </span>
                  </p>
                  <p v-if="project.role" class="project-card__role">{{ project.role }}</p>
                </div>

                <p class="project-card__summary">{{ project.summary }}</p>

                <div v-if="project.impactStatement" class="impact-callout">
                  <span class="impact-icon" aria-hidden="true">◈</span>
                  <span>{{ project.impactStatement }}</span>
                </div>

                <div class="project-card__stack">
                  <span v-for="tech in project.techStack" :key="tech" class="stack-chip"
                    :class="{ 'stack-chip--highlighted': selectedTechs.includes(tech) }">{{ tech }}</span>
                </div>
              </div>

              <div v-if="project.detail || project.impactStatement" class="project-card__footer">
                <button class="detail-btn detail-btn--subtle" :aria-label="`View details for ${project.title}`"
                  @click="openDetail(project)">
                  Details <span aria-hidden="true">→</span>
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Empty state -->
      <div v-if="totalFilteredCount === 0" class="empty-state" role="status">
        <div class="empty-state__icon" aria-hidden="true">⊘</div>
        <p class="empty-state__text">No projects match the selected filters.</p>
        <button class="detail-btn" @click="selectedTechs = []">Clear filters</button>
      </div>
    </div>

    <!-- ── Detail drawer ──────────────────────────────────────────────────── -->
    <Teleport to="body">
      <Transition name="drawer">
        <div v-if="detailProject" class="drawer-overlay" role="dialog"
          :aria-label="`Details for ${detailProject.title}`" aria-modal="true" @click.self="closeDetail"
          @keydown.esc="closeDetail">
          <div class="drawer" ref="drawerEl" tabindex="-1">
            <div class="drawer__header">
              <div class="drawer__title-block">
                <h2 class="drawer__title">{{ detailProject.title }}</h2>
                <p class="drawer__meta">
                  <span v-if="detailProject.employer">{{ detailProject.employer }}</span>
                  <span v-if="detailProject.startYear" class="project-dates">
                    {{ detailProject.employer ? ' · ' : '' }}{{ detailProject.startYear }}{{ detailProject.endYear ? '–'
                      + detailProject.endYear : '–Present' }}
                  </span>
                </p>
                <p v-if="detailProject.role" class="drawer__role">{{ detailProject.role }}</p>
              </div>
              <button class="drawer__close" aria-label="Close details" @click="closeDetail">✕</button>
            </div>

            <div class="drawer__body">
              <div v-if="detailProject.impactStatement" class="drawer__impact">
                <div class="drawer__section-label">Impact</div>
                <div class="impact-callout impact-callout--drawer">
                  <span class="impact-icon" aria-hidden="true">◈</span>
                  <span>{{ detailProject.impactStatement }}</span>
                </div>
              </div>

              <div v-if="detailProject.detail" class="drawer__detail">
                <div class="drawer__section-label">Overview</div>
                <div class="drawer__detail-text" v-html="renderMarkdown(detailProject.detail)" />
              </div>

              <div v-if="detailProject.techStack.length" class="drawer__stack">
                <div class="drawer__section-label">Technologies Used</div>
                <div class="drawer__stack-chips">
                  <span v-for="tech in detailProject.techStack" :key="tech" class="stack-chip stack-chip--large">{{ tech
                  }}</span>
                </div>
              </div>
            </div>
          </div>
        </div>
      </Transition>
    </Teleport>

  </div>
</template>

<script setup>
import {
  ref, computed, onMounted, onBeforeUnmount, nextTick, watch
} from 'vue'
import { marked } from 'marked'
import { useRoute } from 'vue-router'
import { useProjectStore } from '@/stores/projectStore'

const store = useRoute ? useProjectStore() : useProjectStore()
const route = useRoute()

// ── State ────────────────────────────────────────────────────────────────────
const selectedTechs = ref([])
const groupBy = ref('employer')   // 'employer' | 'year' | 'none'
const collapsedGroups = ref(new Set())
const visibleCards = ref(new Set())
const detailProject = ref(null)
const drawerEl = ref(null)
const filterChipsEl = ref(null)
const focusedChipIndex = ref(0)
const highlightedId = ref(null)

let observer = null

const groupOptions = [
  { value: 'employer', label: 'Employer' },
  { value: 'year', label: 'Period' },
  { value: 'none', label: 'All' },
]

// ── Chatbot integration: ?highlight=TechName ────────────────────────────────
onMounted(async () => {
  createObserver()
  if (!store.projects.length) await store.fetchProjects()
  observeCards()

  // Apply highlight param from chatbot redirect
  const hlParam = route.query.highlight
  if (hlParam) {
    const techQuery = decodeURIComponent(hlParam).toLowerCase()
    const matched = store.projects
      .flatMap(p => p.techStack)
      .find(t => t.toLowerCase().includes(techQuery))
    if (matched) {
      selectedTechs.value = [matched]
    } else {
      // Try matching a specific project title
      const matchedProject = store.projects.find(
        p => p.title.toLowerCase().includes(techQuery)
      )
      if (matchedProject) {
        highlightedId.value = matchedProject.projectId
        await nextTick()
        const el = document.querySelector(`[data-project-id="${matchedProject.projectId}"]`)
        el?.scrollIntoView({ behavior: 'smooth', block: 'center' })
        setTimeout(() => { highlightedId.value = null }, 3000)
      }
    }
  }
})

onBeforeUnmount(() => {
  observer?.disconnect()
})

function renderMarkdown(text) {
  if (!text) return ''
  return marked.parse(text, { breaks: true, gfm: true })
}

// ── Intersection observer for card entrance animations ──────────────────────
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
    { threshold: 0.05, rootMargin: '0px 0px 60px 0px' }
  )
}

function observeCards() {
  nextTick(() => {
    document.querySelectorAll('[data-project-id]').forEach(el => observer?.observe(el))
  })
}

watch([groupBy, selectedTechs], () => {
  nextTick(() => observeCards())
})

// ── Tech experience bar ──────────────────────────────────────────────────────
const techExperience = computed(() => {
  const map = new Map()
  for (const project of store.projects) {
    const start = parseInt(project.startYear ?? '0') || 0
    const end = parseInt(project.endYear ?? String(new Date().getFullYear())) || new Date().getFullYear()
    const years = Math.max(end - start, 1)

    for (const tech of project.techStack) {
      if (!map.has(tech)) map.set(tech, { years: 0, count: 0 })
      const entry = map.get(tech)
      entry.years = Math.max(entry.years, years)
      entry.count++
    }
  }
  return [...map.entries()]
    .map(([tech, v]) => ({ tech, ...v }))
    .sort((a, b) => b.years - a.years || b.count - a.count)
    .slice(0, 12)
})

const maxYears = computed(() => Math.max(...techExperience.value.map(t => t.years), 1))

// ── Filtering helpers ────────────────────────────────────────────────────────
const allTags = computed(() => {
  const tags = new Set()
  store.projects.forEach(p => p.techStack?.forEach(t => tags.add(t)))
  return [...tags].sort()
})

const filteredProjects = computed(() => {
  if (!selectedTechs.value.length) return store.projects
  return store.projects.filter(p =>
    selectedTechs.value.every(t => p.techStack?.includes(t))
  )
})

const featuredProjects = computed(() =>
  filteredProjects.value.filter(p => p.isFeatured)
)

const nonFeaturedProjects = computed(() =>
  filteredProjects.value.filter(p => !p.isFeatured)
)

// ── Grouping ─────────────────────────────────────────────────────────────────
const groupedProjects = computed(() => {
  const projects = nonFeaturedProjects.value

  if (groupBy.value === 'none') {
    return [{ key: 'all', label: 'All Projects', yearRange: null, projects }]
  }

  if (groupBy.value === 'employer') {
    const map = new Map()
    for (const p of projects) {
      const key = p.employer || 'Personal / Independent'
      if (!map.has(key)) map.set(key, [])
      map.get(key).push(p)
    }
    return [...map.entries()].map(([employer, projs]) => {
      const years = projs.flatMap(p => [p.startYear, p.endYear].filter(Boolean)).map(Number)
      const minY = years.length ? Math.min(...years) : null
      const maxY = years.length ? Math.max(...years) : null
      return {
        key: employer,
        label: employer,
        yearRange: minY && maxY ? (minY === maxY ? String(minY) : `${minY}–${maxY === new Date().getFullYear() ? 'Present' : maxY}`) : null,
        projects: projs,
      }
    })
  }

  if (groupBy.value === 'year') {
    const buckets = new Map([
      ['Recent (2023–Present)', []],
      ['Mid Career (2019–2022)', []],
      ['Early Career (2015–2018)', []],
      ['Other', []],
    ])
    for (const p of projects) {
      const y = parseInt(p.startYear ?? '0') || 0
      if (y >= 2023) buckets.get('Recent (2023–Present)').push(p)
      else if (y >= 2019) buckets.get('Mid Career (2019–2022)').push(p)
      else if (y >= 2015) buckets.get('Early Career (2015–2018)').push(p)
      else buckets.get('Other').push(p)
    }
    return [...buckets.entries()]
      .filter(([, projs]) => projs.length)
      .map(([label, projs]) => ({ key: label, label, yearRange: null, projects: projs }))
  }

  return []
})

const totalFilteredCount = computed(() =>
  featuredProjects.value.length + groupedProjects.value.reduce((s, g) => s + g.projects.length, 0)
)

// ── Actions ──────────────────────────────────────────────────────────────────
function toggleTech(tech) {
  const idx = selectedTechs.value.indexOf(tech)
  if (idx === -1) selectedTechs.value = [...selectedTechs.value, tech]
  else selectedTechs.value = selectedTechs.value.filter(t => t !== tech)
}

function toggleGroup(key) {
  const s = new Set(collapsedGroups.value)
  if (s.has(key)) s.delete(key)
  else s.add(key)
  collapsedGroups.value = s
}

// Detail drawer
function openDetail(project) {
  detailProject.value = project
  nextTick(() => {
    drawerEl.value?.focus()
    document.body.style.overflow = 'hidden'
  })
}

function closeDetail() {
  detailProject.value = null
  document.body.style.overflow = ''
}

// Filter chip keyboard navigation (roving tabindex)
function onFilterKeydown(e) {
  const chips = filterChipsEl.value?.querySelectorAll('[role="checkbox"], .filter-chip--clear')
  if (!chips?.length) return

  if (e.key === 'ArrowRight' || e.key === 'ArrowDown') {
    e.preventDefault()
    focusedChipIndex.value = (focusedChipIndex.value + 1) % chips.length
    chips[focusedChipIndex.value]?.focus()
  } else if (e.key === 'ArrowLeft' || e.key === 'ArrowUp') {
    e.preventDefault()
    focusedChipIndex.value = (focusedChipIndex.value - 1 + chips.length) % chips.length
    chips[focusedChipIndex.value]?.focus()
  } else if (e.key === 'Home') {
    e.preventDefault()
    focusedChipIndex.value = 0
    chips[0]?.focus()
  } else if (e.key === 'End') {
    e.preventDefault()
    focusedChipIndex.value = chips.length - 1
    chips[chips.length - 1]?.focus()
  }
}
</script>

<style scoped>
/* ── Base ────────────────────────────────────────────────────────────────── */
.projects-page {
  padding: 0 0 4rem;
  max-width: 1300px;
  margin: 0 auto;
  font-family: 'Raleway', sans-serif;
}

/* ── Page header ─────────────────────────────────────────────────────────── */
.projects-header {
  padding: 3rem 2rem 2rem;
}

.projects-title {
  font-family: 'Patua One', serif;
  font-size: clamp(2rem, 4vw, 3rem);
  font-weight: 700;
  margin: 0 0 0.5rem;
  color: rgb(var(--v-theme-on-background));
  line-height: 1.1;
}

.projects-subtitle {
  font-size: 1rem;
  color: rgba(var(--v-theme-on-background), 0.6);
  margin: 0;
  max-width: 560px;
}

/* ── Tech experience bar ─────────────────────────────────────────────────── */
.tech-bar {
  margin: 0 2rem 2rem;
  background: rgb(var(--v-theme-surface));
  border: 1px solid rgba(var(--v-theme-secondary), 0.15);
  border-radius: 12px;
  padding: 1.25rem 1.5rem;
}

.tech-bar__label {
  font-size: 0.7rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: rgba(var(--v-theme-on-surface), 0.45);
  margin-bottom: 1rem;
}

.tech-bar__items {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(120px, 1fr));
  gap: 0.5rem;
}

.tech-bar__item {
  position: relative;
  border: 1px solid rgba(var(--v-theme-secondary), 0.2);
  border-radius: 6px;
  padding: 0.4rem 0.6rem;
  cursor: pointer;
  overflow: hidden;
  transition: border-color 0.15s, background 0.15s;
  display: flex;
  justify-content: space-between;
  align-items: center;
  background: transparent;
  outline: none;
}

.tech-bar__item:focus-visible {
  outline: 2px solid rgb(var(--v-theme-secondary));
  outline-offset: 2px;
}

.tech-bar__item:hover {
  border-color: rgba(var(--v-theme-secondary), 0.5);
  background: rgba(var(--v-theme-secondary), 0.04);
}

.tech-bar__item--active {
  border-color: rgb(var(--v-theme-secondary));
  background: rgba(var(--v-theme-secondary), 0.08);
}

.tech-bar__fill {
  position: absolute;
  left: 0;
  bottom: 0;
  height: 2px;
  background: rgb(var(--v-theme-secondary));
  border-radius: 0 0 6px 6px;
  transition: width 0.4s ease;
  opacity: 0.6;
}

.tech-bar__item--active .tech-bar__fill {
  opacity: 1;
}

.tech-bar__tech {
  font-size: 0.72rem;
  font-weight: 600;
  color: rgba(var(--v-theme-on-surface), 0.85);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 75%;
}

.tech-bar__years {
  font-size: 0.65rem;
  font-weight: 700;
  color: rgb(var(--v-theme-secondary));
  opacity: 0.8;
  flex-shrink: 0;
}

/* ── Filter toolbar ──────────────────────────────────────────────────────── */
.filter-toolbar {
  padding: 0 2rem;
  margin-bottom: 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.filter-toolbar__section {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.filter-toolbar__section--flex {
  align-items: flex-start;
}

.filter-toolbar__label {
  font-size: 0.7rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: rgba(var(--v-theme-on-background), 0.4);
  white-space: nowrap;
  padding-top: 3px;
  min-width: 64px;
}

/* Group toggle */
.group-toggle {
  display: flex;
  gap: 0;
  border: 1px solid rgba(var(--v-theme-secondary), 0.2);
  border-radius: 8px;
  overflow: hidden;
}

.group-btn {
  padding: 0.3rem 0.85rem;
  font-size: 0.78rem;
  font-weight: 600;
  font-family: 'Raleway', sans-serif;
  color: rgba(var(--v-theme-on-background), 0.55);
  background: transparent;
  border: none;
  border-right: 1px solid rgba(var(--v-theme-secondary), 0.2);
  cursor: pointer;
  transition: background 0.15s, color 0.15s;
  outline: none;
}

.group-btn:last-child {
  border-right: none;
}

.group-btn:hover {
  background: rgba(var(--v-theme-secondary), 0.06);
  color: rgba(var(--v-theme-on-background), 0.8);
}

.group-btn--active {
  background: rgba(var(--v-theme-secondary), 0.15);
  color: rgb(var(--v-theme-secondary));
}

.group-btn:focus-visible {
  outline: 2px solid rgb(var(--v-theme-secondary));
  outline-offset: -2px;
}

/* Filter chips */
.filter-chips {
  display: flex;
  flex-wrap: wrap;
  gap: 0.35rem;
}

.filter-chip {
  padding: 0.25rem 0.7rem;
  border-radius: 20px;
  font-size: 0.72rem;
  font-weight: 600;
  font-family: 'Raleway', sans-serif;
  color: rgba(var(--v-theme-on-background), 0.6);
  background: transparent;
  border: 1px solid rgba(var(--v-theme-on-background), 0.15);
  cursor: pointer;
  transition: background 0.15s, border-color 0.15s, color 0.15s;
  outline: none;
  line-height: 1.4;
}

.filter-chip:hover {
  border-color: rgb(var(--v-theme-secondary));
  color: rgb(var(--v-theme-secondary));
}

.filter-chip--active {
  background: rgba(var(--v-theme-secondary), 0.15);
  border-color: rgb(var(--v-theme-secondary));
  color: rgb(var(--v-theme-secondary));
}

.filter-chip:focus-visible {
  outline: 2px solid rgb(var(--v-theme-secondary));
  outline-offset: 2px;
}

.filter-chip--clear {
  border-style: dashed;
  color: rgba(var(--v-theme-error), 0.7);
  border-color: rgba(var(--v-theme-error), 0.3);
}

.filter-chip--clear:hover {
  border-color: rgb(var(--v-theme-error));
  color: rgb(var(--v-theme-error));
  background: rgba(var(--v-theme-error), 0.06);
}

/* Active filter summary */
.active-filters {
  padding: 0 2rem 0.75rem;
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.4rem;
}

.active-filters__label {
  font-size: 0.72rem;
  color: rgba(var(--v-theme-on-background), 0.4);
}

.active-filter-tag {
  display: inline-flex;
  align-items: center;
  gap: 0.3rem;
  padding: 0.15rem 0.5rem;
  background: rgba(var(--v-theme-secondary), 0.12);
  border: 1px solid rgba(var(--v-theme-secondary), 0.3);
  border-radius: 4px;
  font-size: 0.72rem;
  font-weight: 600;
  color: rgb(var(--v-theme-secondary));
}

.active-filter-tag__remove {
  background: none;
  border: none;
  cursor: pointer;
  color: inherit;
  font-size: 0.65rem;
  padding: 0;
  line-height: 1;
  opacity: 0.7;
  transition: opacity 0.15s;
}

.active-filter-tag__remove:hover {
  opacity: 1;
}

/* ── Projects body ───────────────────────────────────────────────────────── */
.projects-body {
  padding: 0 2rem;
  display: flex;
  flex-direction: column;
  gap: 2.5rem;
}

/* ── Featured strip ──────────────────────────────────────────────────────── */
.featured-strip {
  background: linear-gradient(135deg,
      rgba(var(--v-theme-secondary), 0.06) 0%,
      rgba(var(--v-theme-secondary), 0.02) 100%);
  border: 1px solid rgba(var(--v-theme-secondary), 0.2);
  border-radius: 16px;
  padding: 1.75rem;
}

.featured-strip__header {
  margin-bottom: 1.25rem;
}

.featured-strip__title {
  font-family: 'Patua One', serif;
  font-size: 0.9rem;
  font-weight: 600;
  color: rgb(var(--v-theme-secondary));
  text-transform: uppercase;
  letter-spacing: 0.1em;
  display: flex;
  align-items: center;
  gap: 0.4rem;
}

.star-icon {
  color: rgb(var(--v-theme-yellow, var(--v-theme-secondary)));
  font-size: 1rem;
}

.featured-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(380px, 1fr));
  gap: 1.25rem;
}

.featured-card {
  background: rgb(var(--v-theme-surface));
  border: 1px solid rgba(var(--v-theme-secondary), 0.25);
  border-radius: 12px;
  display: flex;
  flex-direction: column;
  transition:
    opacity 0.45s ease,
    transform 0.45s ease,
    box-shadow 0.15s ease;
  box-shadow: 0 0 0 1px rgba(var(--v-theme-secondary), 0.08);
  position: relative;
  overflow: hidden
}

.featured-card::before {
  content: '';
  position: absolute;
  inset: 0;
  border-radius: 12px;
  background: linear-gradient(135deg,
      rgba(var(--v-theme-secondary), 0.06) 0%,
      transparent 50%,
      rgba(var(--v-theme-secondary), 0.03) 100%);
  opacity: 0;
  transition: opacity 0.3s ease;
  pointer-events: none;
}

.featured-card:hover::before {
  opacity: 1;
}

.featured-card:hover {
  box-shadow:
    0 8px 32px rgba(0, 0, 0, 0.35),
    0 0 0 1px rgba(var(--v-theme-secondary), 0.35),
    0 0 24px -4px rgba(var(--v-theme-secondary), 0.25) !important;
  transform: translateY(-5px);
}

.featured-card__body {
  padding: 1.5rem 1.5rem 1rem;
  flex: 1;
}

.featured-card__header {
  margin-bottom: 0.75rem;
}

.featured-card__title {
  font-family: 'Patua One', serif;
  font-size: 1.25rem;
  font-weight: 600;
  line-height: 1.25;
  margin: 0 0 0.3rem;
  color: rgb(var(--v-theme-on-surface));
}

.featured-card__meta {
  font-size: 0.8rem;
  color: rgba(var(--v-theme-on-surface), 0.5);
  margin: 0 0 0.2rem;
}

.featured-card__role {
  font-size: 0.78rem;
  color: rgb(var(--v-theme-secondary));
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  margin: 0;
}

.featured-card__summary {
  font-size: 0.875rem;
  line-height: 1.65;
  color: rgba(var(--v-theme-on-surface), 0.8);
  margin: 0 0 1rem;
}

.featured-card__footer {
  padding: 0.75rem 1.5rem 1.25rem;
  display: flex;
  align-items: center;
  gap: 0.75rem;
  border-top: 1px solid rgba(var(--v-theme-secondary), 0.1);
}

/* ── Employer groups ─────────────────────────────────────────────────────── */
.employer-group {}

.employer-group__header {
  margin-bottom: 1rem;
}

.employer-group__toggle {
  width: 100%;
  display: flex;
  align-items: center;
  gap: 0.75rem;
  background: none;
  border: none;
  border-bottom: 2px solid rgba(var(--v-theme-secondary), 0.2);
  padding: 0 0 0.6rem;
  cursor: pointer;
  text-align: left;
  outline: none;
  transition: border-color 0.15s;
}

.employer-group__toggle:hover {
  border-bottom-color: rgba(var(--v-theme-secondary), 0.6);
}

.employer-group__toggle:hover .employer-group__name {
  color: rgb(var(--v-theme-secondary));
  transition: color 0.2s ease;
}

.employer-group__toggle:focus-visible {
  outline: 2px solid rgb(var(--v-theme-secondary));
  outline-offset: 2px;
  border-radius: 2px;
}

.employer-group__name {
  font-family: 'Patua One', serif;
  font-size: 1.1rem;
  font-weight: 600;
  color: rgb(var(--v-theme-on-background));
  flex: 1;
}

.employer-group__meta {
  font-size: 0.75rem;
  color: rgba(var(--v-theme-on-background), 0.4);
}

.employer-group__years {
  color: rgba(var(--v-theme-secondary), 0.7);
}

.employer-group__chevron {
  font-size: 1.2rem;
  color: rgba(var(--v-theme-secondary), 0.5);
  transform: rotate(90deg);
  transition: transform 0.2s ease;
  display: inline-block;
  line-height: 1;
}

.employer-group__chevron--collapsed {
  transform: rotate(0deg);
}

.employer-group__content {
  overflow: hidden;
  transition: max-height 0.3s ease, opacity 0.3s ease;
  max-height: 5000px;
  opacity: 1;
}

.employer-group__content--collapsed {
  max-height: 0;
  opacity: 0;
  pointer-events: none;
}

/* ── Projects grid ───────────────────────────────────────────────────────── */
.projects-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(310px, 1fr));
  gap: 1.25rem;
  align-items: start;
}

/* ── Card entrance animation ─────────────────────────────────────────────── */
.card-animate {
  opacity: 0;
  transform: translateY(22px);
  transition: opacity 0.4s ease, transform 0.4s ease;
}

.card-animate--visible {
  opacity: 1;
  transform: translateY(0);
}

/* ── Project card ────────────────────────────────────────────────────────── */
.project-card {
  background: rgb(var(--v-theme-surface));
  border: 1px solid rgba(255, 255, 255, 0.07);
  border-radius: 12px;
  display: flex;
  flex-direction: column;
  transition:
    opacity 0.4s ease,
    transform 0.4s ease,
    box-shadow 0.15s ease,
    border-color 0.15s ease;
}

.project-card:hover {
  transform: translateY(-3px);
  box-shadow: 0 8px 28px rgba(0, 0, 0, 0.3);
  border-color: rgba(var(--v-theme-secondary), 0.3);
}

/* Don't let hover override entrance animation */
.project-card.card-animate:not(.card-animate--visible):hover {
  transform: translateY(22px);
}

.project-card--highlighted {
  border-color: rgb(var(--v-theme-secondary)) !important;
  box-shadow: 0 0 0 2px rgba(var(--v-theme-secondary), 0.3) !important;
  animation: highlight-pulse 3s ease forwards;
}

@keyframes highlight-pulse {
  0% {
    box-shadow: 0 0 0 4px rgba(var(--v-theme-secondary), 0.4);
  }

  60% {
    box-shadow: 0 0 0 8px rgba(var(--v-theme-secondary), 0.15);
  }

  100% {
    box-shadow: 0 0 0 1px rgba(var(--v-theme-secondary), 0.1);
  }
}

.project-card__body {
  padding: 1.25rem 1.25rem 1rem;
  flex: 1;
}

.project-card__header {
  margin-bottom: 0.6rem;
}

.project-card__title {
  font-family: 'Patua One', serif;
  font-size: 1.05rem;
  font-weight: 600;
  line-height: 1.3;
  margin: 0 0 0.2rem;
  color: rgb(var(--v-theme-on-surface));
}

.project-card__meta {
  font-size: 0.75rem;
  color: rgba(var(--v-theme-on-surface), 0.5);
  margin: 0 0 0.15rem;
}

.project-dates {
  opacity: 0.8;
}

.project-card__role {
  font-size: 0.72rem;
  color: rgb(var(--v-theme-secondary));
  font-weight: 700;
  margin: 0;
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.project-card__summary {
  font-size: 0.85rem;
  line-height: 1.65;
  color: rgba(var(--v-theme-on-surface), 0.75);
  margin: 0.6rem 0 0.85rem;
}

.project-card__footer {
  padding: 0.6rem 1.25rem 1rem;
  border-top: 1px solid rgba(255, 255, 255, 0.05);
}

/* ── Impact callout ──────────────────────────────────────────────────────── */
.impact-callout {
  display: flex;
  align-items: flex-start;
  gap: 0.5rem;
  background: rgba(var(--v-theme-secondary), 0.07);
  border-left: 3px solid rgb(var(--v-theme-secondary));
  border-radius: 0 6px 6px 0;
  padding: 0.5rem 0.75rem;
  margin-bottom: 0.85rem;
  font-size: 0.78rem;
  line-height: 1.55;
  color: rgba(var(--v-theme-on-surface), 0.85);
}

@keyframes border-pulse {

  0%,
  100% {
    border-left-color: rgba(var(--v-theme-secondary), 0.6);
  }

  50% {
    border-left-color: rgb(var(--v-theme-secondary));
  }
}

.impact-callout {
  animation: border-pulse 3s ease-in-out infinite;
}

.impact-callout--featured {
  animation: border-pulse 2.5s ease-in-out infinite;
}

/* Respect reduced motion */
@media (prefers-reduced-motion: reduce) {

  .impact-callout,
  .impact-callout--featured {
    animation: none;
  }
}

.impact-callout--featured {
  background: rgba(var(--v-theme-secondary), 0.1);
  border-left-width: 4px;
  font-size: 0.82rem;
}

.impact-callout--drawer {
  font-size: 0.9rem;
  padding: 0.75rem 1rem;
}

.impact-icon {
  color: rgb(var(--v-theme-secondary));
  font-size: 0.9rem;
  margin-top: 1px;
  flex-shrink: 0;
}

/* ── Tech stack chips ────────────────────────────────────────────────────── */
.project-card__stack,
.drawer__stack-chips {
  display: flex;
  flex-wrap: wrap;
  gap: 0.3rem;
}

.stack-chip {
  display: inline-block;
  padding: 0.18rem 0.55rem;
  border-radius: 4px;
  font-size: 0.68rem;
  font-weight: 600;
  background: rgba(var(--v-theme-secondary), 0.08);
  color: rgba(var(--v-theme-secondary), 0.8);
  border: 1px solid rgba(var(--v-theme-secondary), 0.15);
  letter-spacing: 0.02em;
  transition: background 0.15s, color 0.15s, border-color 0.15s, transform 0.12s ease;
}

.stack-chip:hover {
  transform: translateY(-1px);
  background: rgba(var(--v-theme-secondary), 0.14);
  color: rgb(var(--v-theme-secondary));
  border-color: rgba(var(--v-theme-secondary), 0.3);
}

.stack-chip--highlighted {
  background: rgba(var(--v-theme-secondary), 0.2);
  color: rgb(var(--v-theme-secondary));
  border-color: rgba(var(--v-theme-secondary), 0.4);
}

.stack-chip--large {
  font-size: 0.8rem;
  padding: 0.3rem 0.75rem;
}

/* ── Detail button ───────────────────────────────────────────────────────── */
.detail-btn {
  background: none;
  border: 1px solid rgba(var(--v-theme-secondary), 0.35);
  border-radius: 6px;
  padding: 0.35rem 0.85rem;
  font-size: 0.78rem;
  font-weight: 700;
  font-family: 'Raleway', sans-serif;
  color: rgb(var(--v-theme-secondary));
  cursor: pointer;
  transition: background 0.15s, border-color 0.15s;
  letter-spacing: 0.03em;
  display: inline-flex;
  align-items: center;
  gap: 0.3rem;
  outline: none;
}

.detail-btn span[aria-hidden] {
  display: inline-block;
  transition: transform 0.18s ease;
}

.detail-btn:hover span[aria-hidden] {
  transform: translateX(3px);
}

.detail-btn:hover {
  background: rgba(var(--v-theme-secondary), 0.1);
  border-color: rgb(var(--v-theme-secondary));
}

.detail-btn:focus-visible {
  outline: 2px solid rgb(var(--v-theme-secondary));
  outline-offset: 2px;
}

.detail-btn--subtle {
  font-size: 0.72rem;
  padding: 0.25rem 0.65rem;
  border-color: rgba(var(--v-theme-secondary), 0.2);
}

/* ── Empty state ─────────────────────────────────────────────────────────── */
.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 5rem 0;
  gap: 1rem;
}

.empty-state__icon {
  font-size: 3rem;
  color: rgba(var(--v-theme-on-background), 0.2);
}

.empty-state__text {
  font-size: 1rem;
  color: rgba(var(--v-theme-on-background), 0.45);
  margin: 0;
}

/* ── Skeleton loaders ────────────────────────────────────────────────────── */
@keyframes shimmer {
  0% {
    background-position: -600px 0;
  }

  100% {
    background-position: 600px 0;
  }
}

.skeleton-line,
.skeleton-text,
.skeleton-impact,
.skeleton-chip {
  border-radius: 4px;
  background: linear-gradient(90deg,
      rgba(255, 255, 255, 0.04) 25%,
      rgba(255, 255, 255, 0.1) 50%,
      rgba(255, 255, 255, 0.04) 75%);
  background-size: 1200px 100%;
  animation: shimmer 1.8s infinite linear;
}

.skeleton-card__body {
  padding: 1.25rem;
}

.skeleton-impact {
  height: 38px;
  border-radius: 0 6px 6px 0;
  border-left: 3px solid rgba(255, 255, 255, 0.08);
  margin-bottom: 0.85rem;
}

.skeleton-chips {
  display: flex;
  gap: 0.35rem;
  flex-wrap: wrap;
}

.skeleton-chip {
  height: 20px;
  width: 56px;
  border-radius: 4px;
}

/* ── Detail drawer ───────────────────────────────────────────────────────── */
.drawer-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.6);
  z-index: 2000;
  display: flex;
  justify-content: flex-end;
  backdrop-filter: blur(2px);
}

.drawer {
  width: min(520px, 95vw);
  height: 100%;
  background: rgb(var(--v-theme-surface));
  border-left: 1px solid rgba(var(--v-theme-secondary), 0.2);
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  outline: none;
  scrollbar-width: thin;
  scrollbar-color: rgba(var(--v-theme-secondary), 0.3) transparent;
}

.drawer__header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 1rem;
  padding: 2rem 2rem 1.5rem;
  border-bottom: 1px solid rgba(var(--v-theme-secondary), 0.12);
  position: sticky;
  top: 0;
  background: rgb(var(--v-theme-surface));
  z-index: 1;
}

.drawer__title-block {
  flex: 1;
  min-width: 0;
}

.drawer__title {
  font-family: 'Patua One', serif;
  font-size: 1.4rem;
  font-weight: 700;
  margin: 0 0 0.35rem;
  color: rgb(var(--v-theme-on-surface));
  line-height: 1.25;
}

.drawer__meta {
  font-size: 0.82rem;
  color: rgba(var(--v-theme-on-surface), 0.5);
  margin: 0 0 0.2rem;
}

.drawer__role {
  font-size: 0.78rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: rgb(var(--v-theme-secondary));
  margin: 0;
}

.drawer__close {
  background: none;
  border: 1px solid rgba(var(--v-theme-on-surface), 0.15);
  border-radius: 6px;
  width: 32px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  color: rgba(var(--v-theme-on-surface), 0.5);
  font-size: 0.85rem;
  transition: background 0.15s, color 0.15s;
  flex-shrink: 0;
  outline: none;
}

.drawer__close:hover {
  background: rgba(var(--v-theme-on-surface), 0.08);
  color: rgb(var(--v-theme-on-surface));
}

.drawer__close:focus-visible {
  outline: 2px solid rgb(var(--v-theme-secondary));
  outline-offset: 2px;
}

.drawer__body {
  padding: 1.75rem 2rem;
  display: flex;
  flex-direction: column;
  gap: 1.75rem;
}

.drawer__section-label {
  font-size: 0.65rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.1em;
  color: rgba(var(--v-theme-on-surface), 0.35);
  margin-bottom: 0.6rem;
}

.drawer__detail-text {
  font-size: 0.9rem;
  line-height: 1.75;
  color: rgba(var(--v-theme-on-surface), 0.8);
  margin: 0;
}

/* ── Drawer transition ───────────────────────────────────────────────────── */
.drawer-enter-active,
.drawer-leave-active {
  transition: opacity 0.25s ease;
}

.drawer-enter-active .drawer,
.drawer-leave-active .drawer {
  transition: transform 0.3s cubic-bezier(0.4, 0, 0.2, 1);
}

.drawer-enter-from,
.drawer-leave-to {
  opacity: 0;
}

.drawer-enter-from .drawer,
.drawer-leave-to .drawer {
  transform: translateX(100%);
}

/* ── Responsive ──────────────────────────────────────────────────────────── */
@media (max-width: 768px) {
  .projects-page {
    padding: 0 0 3rem;
  }

  .projects-header {
    padding: 2rem 1.25rem 1.5rem;
  }

  .tech-bar {
    margin: 0 1.25rem 1.5rem;
  }

  .tech-bar__items {
    grid-template-columns: repeat(auto-fill, minmax(100px, 1fr));
  }

  .filter-toolbar {
    padding: 0 1.25rem;
  }

  .active-filters {
    padding: 0 1.25rem 0.6rem;
  }

  .projects-body {
    padding: 0 1.25rem;
  }

  .featured-grid {
    grid-template-columns: 1fr;
  }

  .projects-grid {
    grid-template-columns: 1fr;
  }

  .drawer__header,
  .drawer__body {
    padding-left: 1.25rem;
    padding-right: 1.25rem;
  }
}

@media (max-width: 479px) {
  .tech-bar__items {
    grid-template-columns: repeat(auto-fill, minmax(88px, 1fr));
  }
}


:deep(.drawer__detail-text) h1,
:deep(.drawer__detail-text) h2,
:deep(.drawer__detail-text) h3 {
  font-family: 'Patua One', serif;
  color: rgb(var(--v-theme-on-surface));
  margin: 1.25rem 0 0.5rem;
}

:deep(.drawer__detail-text) h2 {
  font-size: 1rem;
}

:deep(.drawer__detail-text) h3 {
  font-size: 0.9rem;
}

:deep(.drawer__detail-text) p {
  margin: 0 0 0.75rem;
  font-size: 0.9rem;
  line-height: 1.75;
  color: rgba(var(--v-theme-on-surface), 0.8);
}

:deep(.drawer__detail-text) ul,
:deep(.drawer__detail-text) ol {
  padding-left: 1.25rem;
  margin: 0 0 0.75rem;
}

:deep(.drawer__detail-text) li {
  font-size: 0.875rem;
  line-height: 1.7;
  color: rgba(var(--v-theme-on-surface), 0.8);
  margin-bottom: 0.25rem;
}

:deep(.drawer__detail-text) strong {
  color: rgb(var(--v-theme-on-surface));
  font-weight: 700;
}

:deep(.drawer__detail-text) code {
  font-family: 'Courier New', monospace;
  font-size: 0.8rem;
  background: rgba(var(--v-theme-secondary), 0.1);
  color: rgb(var(--v-theme-secondary));
  padding: 0.15rem 0.4rem;
  border-radius: 4px;
}

:deep(.drawer__detail-text) blockquote {
  border-left: 3px solid rgb(var(--v-theme-secondary));
  margin: 0 0 0.75rem;
  padding: 0.5rem 0.75rem;
  background: rgba(var(--v-theme-secondary), 0.06);
  border-radius: 0 6px 6px 0;
  font-size: 0.875rem;
  color: rgba(var(--v-theme-on-surface), 0.7);
}

:deep(.drawer__detail-text) a {
  color: rgb(var(--v-theme-secondary));
  text-decoration: none;
}

:deep(.drawer__detail-text) a:hover {
  text-decoration: underline;
}

:deep(.drawer__detail-text) hr {
  border: none;
  border-top: 1px solid rgba(var(--v-theme-secondary), 0.15);
  margin: 1rem 0;
}

/* Remove margin on last child to avoid extra padding at bottom */
:deep(.drawer__detail-text)>*:last-child {
  margin-bottom: 0;
}


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

.tech-bar {
  animation: fade-up 0.5s 0.1s ease both;
}

.filter-toolbar {
  animation: fade-up 0.5s 0.18s ease both;
}

@media (prefers-reduced-motion: reduce) {

  .page-header-animate,
  .tech-bar,
  .filter-toolbar {
    animation: none;
  }
}
</style>