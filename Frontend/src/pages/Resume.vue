<template>
  <div class="resume-page">

    <!-- ── Toolbar ──────────────────────────────────────────────────────── -->
    <div class="resume-toolbar" role="toolbar" aria-label="Resume controls">
      <div class="toolbar-inner">

        <a
          :href="pdfLink"
          target="_blank"
          rel="noopener noreferrer"
          class="toolbar-pdf-link"
          aria-label="Download official PDF resume (opens in new tab)"
        >
          <v-icon size="14" aria-hidden="true">mdi-file-pdf-box</v-icon>
          <span class="pdf-label">Official PDF</span>
        </a>

        <div class="toolbar-divider" aria-hidden="true" />

        <div class="toolbar-fields" role="group" aria-label="Resume tailoring options">
          <label for="tailor-title" class="sr-only">Job title to tailor resume for</label>
          <input
            id="tailor-title"
            v-model="tailorTitle"
            type="text"
            placeholder="Target job title..."
            class="toolbar-input"
            :disabled="resumeStore.loading"
            @keydown.enter="tryGenerate"
            autocomplete="off"
            spellcheck="false"
          />
          <button
            class="jd-toggle"
            :class="{ 'jd-toggle--active': showJd }"
            @click="showJd = !showJd"
            :aria-expanded="String(showJd)"
            aria-controls="jd-panel"
            :disabled="resumeStore.loading"
            type="button"
          >
            <v-icon size="12" aria-hidden="true">{{ showJd ? 'mdi-chevron-up' : 'mdi-text-box-plus-outline' }}</v-icon>
            <span>{{ showJd ? 'Hide Job Description' : 'Add Job Description' }}</span>
          </button>
        </div>

        <button
          class="generate-btn"
          @click="tryGenerate"
          :disabled="resumeStore.loading || (!tailorTitle.trim() && !jobDescription.trim())"
          :aria-busy="String(resumeStore.loading)"
          aria-label="Generate tailored resume"
          type="button"
        >
          <v-icon
            size="14"
            aria-hidden="true"
            :class="{ 'icon-spin': resumeStore.loading }"
          >
            {{ resumeStore.loading ? 'mdi-loading' : 'mdi-creation' }}
          </v-icon>
          <span>{{ resumeStore.loading ? 'Generating…' : 'Generate' }}</span>
        </button>

      </div>

      <!-- JD panel -->
      <transition name="jd-slide">
        <div v-if="showJd" id="jd-panel" class="jd-panel">
          <label for="jd-textarea" class="sr-only">Job description</label>
          <textarea
            id="jd-textarea"
            v-model="jobDescription"
            placeholder="Paste the full job description here — the more detail you provide, the more precisely the resume will be targeted…"
            class="jd-textarea"
            rows="5"
            :disabled="resumeStore.loading"
            aria-describedby="jd-hint"
          />
          <p id="jd-hint" class="jd-hint">
            The AI will mirror this job's language and prioritize matching experience.
          </p>
        </div>
      </transition>

      <!-- Validation nudge -->
      <transition name="fade-up">
        <p v-if="showValidation" class="validation-msg" role="alert" aria-live="assertive">
          <v-icon size="13" aria-hidden="true">mdi-alert-circle-outline</v-icon>
          Please enter a job title or paste a job description first.
        </p>
      </transition>
    </div>

    <!-- ── Page body ───────────────────────────────────────────────────── -->
    <div class="resume-body">
      <transition name="state-fade" mode="out-in">

        <!-- PROMPT STATE -->
        <div v-if="!resumeStore.loading && !data" key="prompt" class="prompt-state">
          <div class="prompt-card" role="region" aria-label="Resume generator introduction">

            <div class="prompt-orb" aria-hidden="true">
              <div class="prompt-orb__ring prompt-orb__ring--1" />
              <div class="prompt-orb__ring prompt-orb__ring--2" />
              <v-icon size="32" class="prompt-orb__icon">mdi-creation-outline</v-icon>
            </div>

            <h1 class="prompt-title">AI-Tailored Resume</h1>

            <p class="prompt-body">
              Enter a job title or paste a full job description above, then hit
              <strong>Generate</strong>. The AI reads Samuel's real experience and
              builds a resume that speaks directly to that role — no filler, no guessing.
            </p>

            <ul class="prompt-features" aria-label="What the AI does">
              <li class="prompt-feature">
                <span class="feature-icon" aria-hidden="true"><v-icon size="15">mdi-target</v-icon></span>
                <span>Mirrors the job's keywords &amp; terminology</span>
              </li>
              <li class="prompt-feature">
                <span class="feature-icon" aria-hidden="true"><v-icon size="15">mdi-sort-descending</v-icon></span>
                <span>Surfaces the most relevant projects first</span>
              </li>
              <li class="prompt-feature">
                <span class="feature-icon" aria-hidden="true"><v-icon size="15">mdi-update</v-icon></span>
                <span>Pulls from live, always-current data</span>
              </li>
              <li class="prompt-feature">
                <span class="feature-icon" aria-hidden="true"><v-icon size="15">mdi-printer-outline</v-icon></span>
                <span>Print-ready output</span>
              </li>
            </ul>

            <p class="prompt-pdf-note">
              Prefer a standard format?
              <a :href="pdfLink" target="_blank" rel="noopener noreferrer" class="prompt-pdf-link">
                Download the official PDF
                <v-icon size="11" aria-hidden="true">mdi-open-in-new</v-icon>
              </a>
            </p>

          </div>
        </div>

        <!-- LOADING STATE -->
        <div v-else-if="resumeStore.loading" key="loading" class="loading-state"
          aria-live="polite" aria-label="Generating resume, please wait">

          <div class="loading-card">

            <!-- Animated ring -->
            <div class="loading-ring-wrap" aria-hidden="true">
              <svg class="loading-ring" viewBox="0 0 60 60">
                <circle class="ring-track" cx="30" cy="30" r="25" />
                <circle class="ring-fill"  cx="30" cy="30" r="25" />
              </svg>
              <v-icon size="22" class="loading-ring__icon">mdi-file-document-edit-outline</v-icon>
            </div>

            <h2 class="loading-title">Building your resume…</h2>
            <p v-if="tailorTitle" class="loading-sub">
              Tailoring for <em>"{{ tailorTitle }}"</em>
            </p>

            <!-- Step list -->
            <ol class="step-list" aria-label="Generation progress">
              <li
                v-for="(step, i) in STEPS"
                :key="step"
                class="step-item"
                :class="{
                  'step-item--done':    currentStep > i,
                  'step-item--active':  currentStep === i,
                  'step-item--pending': currentStep < i,
                }"
                :aria-current="currentStep === i ? 'step' : undefined"
              >
                <span class="step-pip" aria-hidden="true">
                  <transition name="pip" mode="out-in">
                    <v-icon v-if="currentStep > i"  key="done"    size="13">mdi-check-circle</v-icon>
                    <v-icon v-else-if="currentStep === i" key="active" size="13" class="icon-spin">mdi-loading</v-icon>
                    <v-icon v-else                   key="pending" size="13">mdi-circle-outline</v-icon>
                  </transition>
                </span>
                <span class="step-label">{{ step }}</span>
                <span class="sr-only">
                  {{ currentStep > i ? '(complete)' : currentStep === i ? '(in progress)' : '(pending)' }}
                </span>
              </li>
            </ol>

            <p class="loading-note" aria-hidden="true">Usually takes 15–20 seconds</p>
          </div>
        </div>

        <!-- RESUME STATE -->
        <div v-else-if="data" key="resume" class="resume-state">

          <!-- Sub-toolbar -->
          <div class="resume-actions" role="toolbar" aria-label="Resume document actions">
            <button class="action-btn" @click="clearResume" type="button" aria-label="Start over and generate a new resume">
              <v-icon size="14" aria-hidden="true">mdi-refresh</v-icon>
              New Resume
            </button>
            <button class="action-btn" @click="printPage" type="button" aria-label="Print this resume">
              <v-icon size="14" aria-hidden="true">mdi-printer-outline</v-icon>
              Print
            </button>
            <span v-if="tailorTitle" class="tailor-badge" aria-label="This resume was tailored for a specific role">
              <v-icon size="11" aria-hidden="true">mdi-target</v-icon>
              Tailored for "{{ tailorTitle }}"
            </span>
          </div>

          <!-- Resume document -->
          <main
            class="resume-doc"
            ref="resumeEl"
            tabindex="-1"
            aria-label="Generated resume for Samuel Ohrenberg"
          >

            <!-- Header -->
            <header class="rd-header">
              <h1 class="rd-name">{{ data.name }}</h1>
              <p class="rd-headline">{{ data.title }}</p>
            </header>

            <!-- Summary -->
            <section class="rd-section" aria-labelledby="rd-summary">
              <h2 class="rd-section-title" id="rd-summary">Summary</h2>
              <p class="rd-summary">{{ data.summary }}</p>
            </section>

            <!-- Skills -->
            <section v-if="data.coreSkills?.length" class="rd-section" aria-labelledby="rd-skills">
              <h2 class="rd-section-title" id="rd-skills">Core Skills</h2>
              <ul class="rd-skills" aria-label="List of core skills">
                <li v-for="skill in data.coreSkills" :key="skill" class="rd-skill">{{ skill }}</li>
              </ul>
            </section>

            <!-- Experience -->
            <section v-if="data.experience?.length" class="rd-section" aria-labelledby="rd-exp">
              <h2 class="rd-section-title" id="rd-exp">Professional Experience</h2>

              <article
                v-for="(job, ji) in data.experience"
                :key="ji"
                class="rd-job"
                :style="{ '--delay': `${ji * 55}ms` }"
                :aria-label="`${job.title} at ${job.employer}`"
              >
                <div class="rd-job-header">
                  <div class="rd-job-header__left">
                    <h3 class="rd-job-title">{{ job.title }}</h3>
                    <p class="rd-job-employer">
                      <v-icon size="11" aria-hidden="true" class="rd-job-employer__icon">mdi-domain</v-icon>
                      {{ job.employer }}
                    </p>
                  </div>
                  <time v-if="job.years" class="rd-job-years">{{ job.years }}</time>
                </div>

                <p v-if="job.summary" class="rd-job-summary">{{ job.summary }}</p>

                <ul v-if="job.achievements?.length" class="rd-achievements" aria-label="Key achievements">
                  <li v-for="(ach, ai) in job.achievements" :key="ai" class="rd-achievement">
                    <span class="rd-achievement__bullet" aria-hidden="true">◈</span>
                    <span>{{ ach }}</span>
                  </li>
                </ul>

                <div v-if="job.projects?.length" class="rd-job-projects">
                  <h4 class="rd-job-projects__label">
                    <v-icon size="11" aria-hidden="true">mdi-briefcase-outline</v-icon>
                    Projects
                  </h4>
                  <div class="rd-projects-grid" role="list" :aria-label="`Projects from role at ${job.employer}`">
                    <ProjectCard
                      v-for="(proj, pi) in job.projects"
                      :key="pi"
                      :project="proj"
                    />
                  </div>
                </div>
              </article>
            </section>

            <!-- Additional projects -->
            <section v-if="data.additionalProjects?.length" class="rd-section" aria-labelledby="rd-addlprojects">
              <h2 class="rd-section-title" id="rd-addlprojects">Additional Projects</h2>
              <div class="rd-projects-grid" role="list" aria-label="Additional projects not tied to a specific employer">
                <ProjectCard
                  v-for="(proj, pi) in data.additionalProjects"
                  :key="pi"
                  :project="proj"
                />
              </div>
            </section>

          </main>
        </div>

      </transition>
    </div>

  </div>
</template>

<script setup>
import { ref, computed, watch, onBeforeUnmount, nextTick, defineComponent, h } from 'vue'
import { useResumeStore } from '@/stores/resumeStore'

const printPage = () => {
  window.print();
};

// ── Inline sub-component to avoid template repetition ─────────────────
const ProjectCard = defineComponent({
  name: 'ProjectCard',
  props: { project: { type: Object, required: true } },
  setup(props) {
    return () => {
      const p = props.project
      return h('article', {
        class: ['rd-project-card', p.isFeatured && 'rd-project-card--featured'],
        role: 'listitem',
        'aria-label': `${p.isFeatured ? 'Featured project: ' : 'Project: '}${p.title}`,
      }, [
        h('div', { class: 'rd-project-top' }, [
          h('div', { class: 'rd-project-title-row' }, [
            p.isFeatured && h('span', { class: 'rd-project-star', 'aria-label': 'Featured' }, '★'),
            h('h5', { class: 'rd-project-title' }, p.title),
          ]),
          h('div', { class: 'rd-project-meta' }, [
            h('span', { class: 'rd-project-role' }, p.role),
            p.years && h('time', { class: 'rd-project-years' }, p.years),
          ]),
        ]),
        h('p', { class: 'rd-project-summary' }, p.summary),
        p.impact && h('div', { class: 'rd-project-impact', role: 'note', 'aria-label': 'Project impact' },
          p.impact
        ),
        p.techStack?.length && h('ul', { class: 'rd-tech-list', 'aria-label': 'Technologies used' },
          p.techStack.map(t => h('li', { key: t, class: 'rd-tech-tag' }, t))
        ),
      ])
    }
  }
})

// ── Store — NOT wrapped in ref() ───────────────────────────────────────
const resumeStore    = useResumeStore()
const tailorTitle    = ref('')
const jobDescription = ref('')
const showJd         = ref(false)
const showValidation = ref(false)
const pdfLink        = import.meta.env.VITE_RESUME_PDF_LINK
const resumeEl       = ref(null)

const data = computed(() => resumeStore.resumeData)

// ── Validation ─────────────────────────────────────────────────────────
let validationTimer = null

function tryGenerate() {
  if (!tailorTitle.value.trim() && !jobDescription.value.trim()) {
    showValidation.value = true
    clearTimeout(validationTimer)
    validationTimer = setTimeout(() => { showValidation.value = false }, 4000)
    return
  }
  showValidation.value = false
  resumeStore.fetchResume(
    tailorTitle.value?.trim() || undefined,
    jobDescription.value?.trim() || undefined
  )
}

function clearResume() {
  resumeStore.resumeData = null
}

// ── Loading steps ──────────────────────────────────────────────────────
const STEPS = [
  'Reading experience & project data',
  'Matching skills to role requirements',
  'Crafting professional narrative',
  'Prioritising relevant achievements',
  'Formatting final resume',
]
const currentStep = ref(0)
let stepTimer = null

watch(() => resumeStore.loading, async (isLoading) => {
  if (isLoading) {
    currentStep.value = 0
    stepTimer = setInterval(() => {
      if (currentStep.value < STEPS.length - 1) currentStep.value++
    }, 3200)
  } else {
    clearInterval(stepTimer)
    currentStep.value = STEPS.length // mark all done
    if (resumeStore.resumeData) {
      await nextTick()
      resumeEl.value?.focus()
    }
  }
})

onBeforeUnmount(() => {
  clearInterval(stepTimer)
  clearTimeout(validationTimer)
})
</script>

<style scoped>
/* ─────────────────────────────────────────────────────────────────────
   Utilities
───────────────────────────────────────────────────────────────────── */
.sr-only {
  position: absolute;
  width: 1px; height: 1px;
  padding: 0; margin: -1px;
  overflow: hidden;
  clip: rect(0,0,0,0);
  white-space: nowrap;
  border: 0;
}

.icon-spin {
  animation: spin 0.75s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

/* ─────────────────────────────────────────────────────────────────────
   Page shell
───────────────────────────────────────────────────────────────────── */
.resume-page {
  display: flex;
  flex-direction: column;
  min-height: 100%;
  font-family: 'Raleway', sans-serif;
  background: rgb(var(--v-theme-background));
}

.resume-body {
  flex: 1;
  display: flex;
  flex-direction: column;
}

/* ─────────────────────────────────────────────────────────────────────
   Toolbar
───────────────────────────────────────────────────────────────────── */
.resume-toolbar {
  position: sticky;
  top: 0;
  z-index: 10;
  background: rgb(var(--v-theme-surface));
  border-bottom: 1px solid rgba(var(--v-theme-secondary), 0.15);
  padding: 0.55rem 1.25rem;
  box-shadow: 0 2px 20px rgba(0,0,0,.22);
}

.toolbar-inner {
  display: flex;
  align-items: center;
  gap: 0.55rem;
  flex-wrap: wrap;
}

/* PDF link */
.toolbar-pdf-link {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  font-size: 0.78rem;
  font-weight: 600;
  color: rgb(var(--v-theme-secondary)) !important;
  text-decoration: none;
  padding: 0.3rem 0.65rem;
  border-radius: 6px;
  border: 1px solid rgba(var(--v-theme-secondary), 0.25);
  background: rgba(var(--v-theme-secondary), 0.04);
  white-space: nowrap;
  flex-shrink: 0;
  transition: background .15s, border-color .15s;
}
.toolbar-pdf-link:hover  { background: rgba(var(--v-theme-secondary),.1); border-color: rgba(var(--v-theme-secondary),.5); }
.toolbar-pdf-link:focus-visible { outline: 2px solid rgb(var(--v-theme-secondary)); outline-offset: 2px; }

/* Divider */
.toolbar-divider {
  width: 1px; height: 22px;
  background: rgba(var(--v-theme-secondary), 0.15);
  flex-shrink: 0;
}

/* Fields group */
.toolbar-fields {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  flex: 1;
  min-width: 0;
}

.toolbar-input {
  flex: 1;
  min-width: 120px;
  max-width: 320px;
  height: 34px;
  padding: 0 0.7rem;
  background: rgba(var(--v-theme-secondary), 0.04);
  border: 1px solid rgba(var(--v-theme-secondary), 0.2);
  border-radius: 6px;
  color: rgb(var(--v-theme-on-surface));
  font-family: 'Raleway', sans-serif;
  font-size: 0.82rem;
  outline: none;
  transition: border-color .15s, background .15s;
}
.toolbar-input::placeholder { color: rgba(var(--v-theme-on-surface), .35); }
.toolbar-input:focus         { border-color: rgba(var(--v-theme-secondary),.6); background: rgba(var(--v-theme-secondary),.07); }
.toolbar-input:disabled      { opacity: .5; cursor: not-allowed; }

/* JD toggle */
.jd-toggle {
  display: inline-flex;
  align-items: center;
  gap: 0.28rem;
  font-family: 'Raleway', sans-serif;
  font-size: 0.7rem;
  font-weight: 700;
  padding: 0.28rem 0.55rem;
  border-radius: 6px;
  border: 1px solid rgba(var(--v-theme-secondary), 0.18);
  background: transparent;
  color: rgba(var(--v-theme-on-surface), .5);
  cursor: pointer;
  white-space: nowrap;
  flex-shrink: 0;
  transition: all .15s;
}
.jd-toggle:hover, .jd-toggle--active {
  background: rgba(var(--v-theme-secondary), .1);
  border-color: rgba(var(--v-theme-secondary), .45);
  color: rgb(var(--v-theme-secondary));
}
.jd-toggle:focus-visible { outline: 2px solid rgb(var(--v-theme-secondary)); outline-offset: 2px; }
.jd-toggle:disabled { opacity: .4; cursor: not-allowed; }

/* Generate button */
.generate-btn {
  display: inline-flex;
  align-items: center;
  gap: 0.38rem;
  font-family: 'Raleway', sans-serif;
  font-size: 0.78rem;
  font-weight: 700;
  letter-spacing: .04em;
  padding: 0.36rem 0.9rem;
  border-radius: 6px;
  border: 1px solid rgba(var(--v-theme-secondary), .4);
  background: rgba(var(--v-theme-secondary), .1);
  color: rgb(var(--v-theme-secondary));
  cursor: pointer;
  flex-shrink: 0;
  margin-left: auto;
  transition: all .15s;
}
.generate-btn:hover:not(:disabled) {
  background: rgba(var(--v-theme-secondary), .18);
  border-color: rgba(var(--v-theme-secondary), .7);
  box-shadow: 0 0 14px rgba(var(--v-theme-secondary), .2);
}
.generate-btn:focus-visible { outline: 2px solid rgb(var(--v-theme-secondary)); outline-offset: 2px; }
.generate-btn:disabled      { opacity: .38; cursor: not-allowed; }

/* JD panel */
.jd-panel {
  padding-top: 0.55rem;
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
}

.jd-textarea {
  width: 100%;
  background: rgba(var(--v-theme-secondary), .03);
  border: 1px solid rgba(var(--v-theme-secondary), .2);
  border-radius: 8px;
  padding: 0.65rem 0.8rem;
  font-family: 'Raleway', sans-serif;
  font-size: 0.8rem;
  line-height: 1.6;
  color: rgb(var(--v-theme-on-surface));
  resize: vertical;
  outline: none;
  transition: border-color .15s;
}
.jd-textarea:focus       { border-color: rgba(var(--v-theme-secondary), .5); background: rgba(var(--v-theme-secondary),.05); }
.jd-textarea:disabled    { opacity: .5; cursor: not-allowed; }
.jd-textarea::placeholder { color: rgba(var(--v-theme-on-surface), .3); }

.jd-hint {
  font-size: 0.68rem;
  color: rgba(var(--v-theme-on-surface), .38);
  margin: 0;
  font-style: italic;
}

/* Validation */
.validation-msg {
  display: flex;
  align-items: center;
  gap: 0.35rem;
  font-size: 0.73rem;
  font-weight: 600;
  color: rgb(var(--v-theme-error));
  margin: 0.4rem 0 0;
}

/* ─────────────────────────────────────────────────────────────────────
   Transitions
───────────────────────────────────────────────────────────────────── */
/* JD panel slide */
.jd-slide-enter-active, .jd-slide-leave-active {
  transition: opacity .22s ease, transform .22s ease, max-height .25s ease;
  overflow: hidden;
  max-height: 320px;
}
.jd-slide-enter-from, .jd-slide-leave-to {
  opacity: 0; transform: translateY(-6px); max-height: 0;
}

/* Validation fade-up */
.fade-up-enter-active { transition: opacity .2s ease, transform .2s ease; }
.fade-up-leave-active { transition: opacity .15s ease, transform .15s ease; }
.fade-up-enter-from, .fade-up-leave-to { opacity: 0; transform: translateY(-4px); }

/* State crossfade */
.state-fade-enter-active { transition: opacity .3s ease, transform .3s ease; }
.state-fade-leave-active { transition: opacity .2s ease, transform .18s ease; position: absolute; width: 100%; }
.state-fade-enter-from   { opacity: 0; transform: translateY(10px); }
.state-fade-leave-to     { opacity: 0; transform: translateY(-6px); }

/* Step pip swap */
.pip-enter-active, .pip-leave-active { transition: all .18s ease; }
.pip-enter-from  { opacity: 0; transform: scale(.4) rotate(-90deg); }
.pip-leave-to    { opacity: 0; transform: scale(.4) rotate(90deg); }

/* ─────────────────────────────────────────────────────────────────────
   Prompt state
───────────────────────────────────────────────────────────────────── */
.prompt-state {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 3rem 1.5rem;
}

.prompt-card {
  max-width: 500px;
  width: 100%;
  text-align: center;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 1rem;
}

.prompt-orb {
  position: relative;
  width: 76px; height: 76px;
  display: flex; align-items: center; justify-content: center;
}
.prompt-orb__ring {
  position: absolute;
  inset: 0;
  border-radius: 50%;
  border: 1px solid rgba(var(--v-theme-secondary), .3);
}
.prompt-orb__ring--1 { animation: orb-pulse 2.8s ease-in-out infinite; }
.prompt-orb__ring--2 { animation: orb-pulse 2.8s ease-in-out infinite .7s; opacity: .5; transform: scale(1.18); }
@keyframes orb-pulse {
  0%, 100% { transform: scale(1); opacity: .6; }
  50%       { transform: scale(1.12); opacity: .15; }
}
.prompt-orb__icon { color: rgb(var(--v-theme-secondary)) !important; z-index: 1; }

.prompt-title {
  font-family: 'Patua One', serif;
  font-size: clamp(1.45rem, 3.5vw, 2rem);
  color: rgb(var(--v-theme-on-background));
  margin: 0;
  line-height: 1.15;
}

.prompt-body {
  font-size: 0.88rem;
  line-height: 1.8;
  color: rgba(var(--v-theme-on-background), .6);
  margin: 0;
  max-width: 400px;
}

.prompt-features {
  list-style: none;
  padding: 0;
  margin: 0.25rem 0;
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.55rem;
  width: 100%;
  max-width: 380px;
}

.prompt-feature {
  display: flex;
  align-items: center;
  gap: 0.45rem;
  font-size: 0.76rem;
  line-height: 1.4;
  color: rgba(var(--v-theme-on-background), .65);
  background: rgba(var(--v-theme-secondary), .05);
  border: 1px solid rgba(var(--v-theme-secondary), .12);
  border-radius: 8px;
  padding: 0.5rem 0.65rem;
  text-align: left;
}
.feature-icon { color: rgb(var(--v-theme-secondary)); flex-shrink: 0; }
.feature-icon .v-icon { color: inherit !important; }

.prompt-pdf-note {
  font-size: 0.76rem;
  color: rgba(var(--v-theme-on-background), .38);
  margin: 0;
}
.prompt-pdf-link {
  color: rgb(var(--v-theme-secondary));
  text-decoration: none;
  font-weight: 600;
  display: inline-flex;
  align-items: center;
  gap: 0.2rem;
}
.prompt-pdf-link:hover { text-decoration: underline; }
.prompt-pdf-link:focus-visible { outline: 2px solid rgb(var(--v-theme-secondary)); outline-offset: 2px; border-radius: 2px; }

/* ─────────────────────────────────────────────────────────────────────
   Loading state
───────────────────────────────────────────────────────────────────── */
.loading-state {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 3rem 1.5rem;
}

.loading-card {
  max-width: 380px;
  width: 100%;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.55rem;
  text-align: center;
}

/* SVG spinner */
.loading-ring-wrap {
  position: relative;
  width: 68px; height: 68px;
  display: flex; align-items: center; justify-content: center;
  margin-bottom: 0.4rem;
}
.loading-ring {
  position: absolute; inset: 0;
  transform: rotate(-90deg);
  overflow: visible;
}
.ring-track {
  fill: none;
  stroke: rgba(var(--v-theme-secondary), .12);
  stroke-width: 3.5;
}
.ring-fill {
  fill: none;
  stroke: rgb(var(--v-theme-secondary));
  stroke-width: 3.5;
  stroke-linecap: round;
  stroke-dasharray: 157;
  stroke-dashoffset: 157;
  animation: ring-draw 1.9s ease-in-out infinite;
}
@keyframes ring-draw {
  0%   { stroke-dashoffset: 157; }
  55%  { stroke-dashoffset: 18; }
  100% { stroke-dashoffset: 157; }
}
.loading-ring__icon { color: rgb(var(--v-theme-secondary)) !important; z-index: 1; }

.loading-title {
  font-family: 'Patua One', serif;
  font-size: 1.2rem;
  color: rgb(var(--v-theme-on-background));
  margin: 0.3rem 0 0;
}
.loading-sub {
  font-size: 0.8rem;
  color: rgba(var(--v-theme-on-background), .45);
  font-style: italic;
  margin: 0 0 0.4rem;
}

/* Steps */
.step-list {
  list-style: none;
  padding: 0;
  margin: 0.5rem 0 0;
  width: 100%;
  max-width: 310px;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  text-align: left;
}
.step-item {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  font-size: 0.77rem;
  font-family: 'Courier New', monospace;
  letter-spacing: .02em;
  color: rgba(var(--v-theme-on-background), .18);
  transition: color .35s ease;
}
.step-item--active  { color: rgb(var(--v-theme-secondary)); }
.step-item--done    { color: rgba(var(--v-theme-secondary), .4); }

.step-pip {
  flex-shrink: 0;
  width: 18px;
  display: flex; align-items: center; justify-content: center;
}
.step-pip .v-icon { color: currentColor !important; }

.loading-note {
  font-size: 0.68rem;
  color: rgba(var(--v-theme-on-background), .22);
  margin: 0.5rem 0 0;
  font-style: italic;
}

/* ─────────────────────────────────────────────────────────────────────
   Resume state
───────────────────────────────────────────────────────────────────── */
.resume-state {
  flex: 1;
  display: flex;
  flex-direction: column;
}

.resume-actions {
  display: flex;
  align-items: center;
  gap: 0.45rem;
  padding: 0.55rem 1.25rem;
  background: rgb(var(--v-theme-surface));
  border-bottom: 1px solid rgba(var(--v-theme-secondary), .1);
  flex-wrap: wrap;
}
.action-btn {
  display: inline-flex;
  align-items: center;
  gap: 0.32rem;
  font-family: 'Raleway', sans-serif;
  font-size: 0.73rem;
  font-weight: 700;
  padding: 0.26rem 0.6rem;
  border-radius: 5px;
  border: 1px solid rgba(var(--v-theme-secondary), .18);
  background: rgba(var(--v-theme-secondary), .04);
  color: rgba(var(--v-theme-on-surface), .6);
  cursor: pointer;
  transition: all .15s;
}
.action-btn:hover { background: rgba(var(--v-theme-secondary),.1); color: rgb(var(--v-theme-secondary)); border-color: rgba(var(--v-theme-secondary),.4); }
.action-btn:focus-visible { outline: 2px solid rgb(var(--v-theme-secondary)); outline-offset: 2px; }

.tailor-badge {
  display: inline-flex;
  align-items: center;
  gap: 0.28rem;
  font-size: 0.68rem;
  font-weight: 700;
  color: rgb(var(--v-theme-secondary));
  background: rgba(var(--v-theme-secondary), .07);
  border: 1px solid rgba(var(--v-theme-secondary), .18);
  border-radius: 20px;
  padding: 0.2rem 0.6rem;
  margin-left: auto;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 220px;
}
.tailor-badge .v-icon { color: rgb(var(--v-theme-secondary)) !important; flex-shrink: 0; }

/* ─────────────────────────────────────────────────────────────────────
   Resume document  (always light background)
───────────────────────────────────────────────────────────────────── */
.resume-doc {
  flex: 1;
  overflow-y: auto;
  background: #e6eeee;
  outline: none;
}
.resume-doc:focus-visible { outline: 2px solid #48A9A6; outline-offset: -2px; }

/* Header card */
.rd-header {
  max-width: 880px;
  margin: 2rem auto 0;
  padding: 2.25rem 2.5rem 1.75rem;
  background: #fff;
  border-radius: 14px 14px 0 0;
  border-bottom: 3px solid #006a6a;
}
.rd-name {
  font-family: 'Patua One', serif;
  font-size: clamp(1.7rem, 4vw, 2.4rem);
  color: #1a1a1a;
  margin: 0 0 0.3rem;
  line-height: 1.1;
}
.rd-headline {
  font-size: 0.93rem;
  font-weight: 600;
  color: #006a6a;
  margin: 0;
  letter-spacing: .02em;
}

/* Sections */
.rd-section {
  max-width: 880px;
  margin: 0 auto;
  padding: 1.65rem 2.5rem;
  background: #fff;
  border-bottom: 1px solid #c0d8d8;
  animation: slide-up .4s ease both;
}
.rd-section:last-child {
  border-bottom: none;
  border-radius: 0 0 14px 14px;
  margin-bottom: 3rem;
}
@keyframes slide-up {
  from { opacity: 0; transform: translateY(10px); }
  to   { opacity: 1; transform: translateY(0); }
}
.rd-section:nth-child(2) { animation-delay: .04s; }
.rd-section:nth-child(3) { animation-delay: .09s; }
.rd-section:nth-child(4) { animation-delay: .14s; }
.rd-section:nth-child(5) { animation-delay: .19s; }

.rd-section-title {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.7rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: .1em;
  color: #006a6a;
  margin: 0 0 1.1rem;
}
.rd-section-title::before {
  content: '';
  display: block;
  width: 3px; height: 13px;
  background: #48A9A6;
  border-radius: 2px;
  flex-shrink: 0;
}

.rd-summary {
  font-size: 0.88rem;
  line-height: 1.8;
  color: #333;
  margin: 0;
}

/* Skills */
.rd-skills {
  list-style: none; padding: 0; margin: 0;
  display: flex; flex-wrap: wrap; gap: 0.38rem;
}
.rd-skill {
  background: #d0ecec;
  color: #006a6a;
  font-size: 0.73rem;
  font-weight: 600;
  padding: 0.26rem 0.68rem;
  border-radius: 20px;
  border: 1px solid #a8d8d8;
  letter-spacing: .02em;
}

/* Jobs */
.rd-job {
  padding-bottom: 1.85rem;
  margin-bottom: 1.85rem;
  border-bottom: 1px solid #e8f4f4;
  animation: slide-up .4s ease both;
  animation-delay: var(--delay, 0ms);
}
.rd-job:last-child { padding-bottom: 0; margin-bottom: 0; border-bottom: none; }

.rd-job-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 1rem;
  margin-bottom: 0.5rem;
}
.rd-job-header__left { flex: 1; min-width: 0; }
.rd-job-title {
  font-family: 'Patua One', serif;
  font-size: 0.98rem;
  color: #1a1a1a;
  margin: 0 0 0.18rem;
  line-height: 1.25;
}
.rd-job-employer {
  font-size: 0.78rem;
  color: #555;
  margin: 0;
  display: flex;
  align-items: center;
}
.rd-job-employer__icon { color: #aaa !important; margin-right: 3px; }
.rd-job-years {
  font-size: 0.66rem;
  font-weight: 700;
  color: #006a6a;
  background: #e0f4f4;
  border: 1px solid #b0d8d8;
  border-radius: 20px;
  padding: 0.18rem 0.58rem;
  white-space: nowrap;
  flex-shrink: 0;
  font-family: 'Courier New', monospace;
  letter-spacing: .03em;
}
.rd-job-summary {
  font-size: 0.84rem;
  line-height: 1.7;
  color: #444;
  margin: 0 0 0.75rem;
}

.rd-achievements {
  list-style: none; padding: 0; margin: 0 0 0.7rem;
  display: flex; flex-direction: column; gap: 0.38rem;
}
.rd-achievement {
  display: flex; align-items: flex-start; gap: 0.5rem;
  font-size: 0.82rem; line-height: 1.6; color: #333;
}
.rd-achievement__bullet { color: #48A9A6; font-size: 0.6rem; margin-top: 0.38rem; flex-shrink: 0; }

/* Job projects sub-section */
.rd-job-projects {
  margin-top: 0.85rem;
  padding-top: 0.8rem;
  border-top: 1px solid #e0f0f0;
}
.rd-job-projects__label {
  font-size: 0.61rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: .09em;
  color: #aaa;
  display: flex; align-items: center; gap: 0.28rem;
  margin: 0 0 0.65rem;
}
.rd-job-projects__label .v-icon { color: #ccc !important; }

/* Projects grid (shared) */
.rd-projects-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(250px, 1fr));
  gap: 0.75rem;
}

/* ─────────────────────────────────────────────────────────────────────
   Project card  (rendered via defineComponent h())
   These selectors target the generated DOM — they are NOT scoped to
   prevent them from being stripped, but the resume-doc scoping via
   .rd- prefix keeps specificity in check.
───────────────────────────────────────────────────────────────────── */
:deep(.rd-project-card) {
  background: #f4fbfb;
  border: 1px solid #c8dede;
  border-radius: 10px;
  padding: 0.9rem;
  transition: box-shadow .15s, border-color .15s, transform .15s;
}
:deep(.rd-project-card:hover) {
  box-shadow: 0 4px 16px rgba(0,106,106,.1);
  border-color: #48A9A6;
  transform: translateY(-1px);
}
:deep(.rd-project-card--featured) {
  border-color: #48A9A6;
  background: #edf8f8;
  box-shadow: inset 0 0 0 1px rgba(72,169,166,.15);
}
:deep(.rd-project-top)        { margin-bottom: 0.4rem; }
:deep(.rd-project-title-row)  { display: flex; align-items: center; gap: 0.25rem; margin-bottom: 0.15rem; }
:deep(.rd-project-star)       { color: #006a6a; font-size: 0.75rem; }
:deep(.rd-project-title)      { font-size: 0.84rem; font-weight: 700; color: #1a1a1a; margin: 0; line-height: 1.25; }
:deep(.rd-project-meta)       { display: flex; align-items: center; gap: 0.4rem; flex-wrap: wrap; }
:deep(.rd-project-role)       { font-size: 0.68rem; font-weight: 600; color: #006a6a; }
:deep(.rd-project-years)      { font-size: 0.64rem; color: #999; font-family: 'Courier New', monospace; }
:deep(.rd-project-summary)    { font-size: 0.77rem; line-height: 1.6; color: #444; margin: 0 0 0.5rem; }
:deep(.rd-project-impact) {
  display: flex; align-items: flex-start; gap: 0.28rem;
  font-size: 0.72rem; line-height: 1.5; color: #004d4d;
  background: #e0f4f4; border-left: 3px solid #48A9A6;
  border-radius: 0 6px 6px 0; padding: 0.38rem 0.55rem;
  margin-bottom: 0.5rem;
}
:deep(.rd-tech-list)  { list-style: none; padding: 0; margin: 0; display: flex; flex-wrap: wrap; gap: 0.25rem; }
:deep(.rd-tech-tag)   { background: #d0ecec; color: #006a6a; font-size: 0.6rem; font-weight: 600; padding: 0.14rem 0.46rem; border-radius: 12px; letter-spacing: .02em; }

/* ─────────────────────────────────────────────────────────────────────
   Print
───────────────────────────────────────────────────────────────────── */
@media print {
  .resume-toolbar,
  .resume-actions { display: none !important; }
  .resume-doc     { background: #fff; overflow: visible; }
  .rd-header,
  .rd-section     { max-width: 100%; margin: 0; border-radius: 0; animation: none; }
  :deep(.rd-project-card) { break-inside: avoid; box-shadow: none; }
  .rd-job         { break-inside: avoid; }
}

/* ─────────────────────────────────────────────────────────────────────
   Responsive
───────────────────────────────────────────────────────────────────── */
@media (max-width: 600px) {
  .resume-toolbar   { padding: 0.5rem 0.85rem; }
  .toolbar-input    { min-width: 80px; max-width: none; }
  .generate-btn     { margin-left: 0; }
  .pdf-label        { display: none; }
  .tailor-badge     { display: none; }

  .rd-header        { margin: 1rem auto 0; border-radius: 10px 10px 0 0; padding: 1.4rem 1.2rem 1.1rem; }
  .rd-section       { padding: 1.2rem; }
  .rd-job-header    { flex-direction: column; gap: 0.28rem; }
  .rd-job-years     { align-self: flex-start; }
  .rd-projects-grid { grid-template-columns: 1fr; }

  .prompt-features  { grid-template-columns: 1fr; }
  .step-list        { max-width: 100%; padding: 0 0.5rem; }
}

@media (max-width: 360px) {
  .toolbar-pdf-link { padding: 0.28rem 0.4rem; }
}
</style>