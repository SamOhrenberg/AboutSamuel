<template>
  <div class="jobfit-page">
    <div class="jobfit-inner">

      <!-- ── Header ───────────────────────────────────────────────────── -->
      <header class="jobfit-header">
        <div class="header-orb" aria-hidden="true">
          <div class="header-orb__ring header-orb__ring--1" />
          <div class="header-orb__ring header-orb__ring--2" />
          <v-icon size="28" class="header-orb__icon">mdi-scale-balance</v-icon>
        </div>
        <h1 class="jobfit-title">How would I fit?</h1>
        <p class="jobfit-intro">
          Paste a job posting. An AI agent pulls out what the role is asking for, searches
          my real projects and work history for evidence, and tells you honestly where
          I match and where my portfolio comes up short. Then it drafts a cover letter
          using only what it could back up.
        </p>
        <ul class="header-features" aria-label="How the analysis works">
          <li><v-icon size="14" aria-hidden="true">mdi-link-variant</v-icon> Cites real projects</li>
          <li><v-icon size="14" aria-hidden="true">mdi-eye-outline</v-icon> Honest about gaps</li>
          <li><v-icon size="14" aria-hidden="true">mdi-broadcast</v-icon> Every step streams live</li>
        </ul>
      </header>

      <!-- ── Input phase ──────────────────────────────────────────────── -->
      <section v-if="phase === 'input'" class="input-card" aria-labelledby="jd-label">
        <label id="jd-label" for="jd-input" class="input-label">Job description</label>
        <textarea
          id="jd-input"
          v-model="jobDescription"
          class="jd-input"
          rows="11"
          placeholder="Paste the full job posting here. Requirements, responsibilities, nice-to-haves, all of it. The more detail, the better the analysis."
          aria-describedby="jd-count jd-hint"
          spellcheck="false"
        />
        <div class="input-footer">
          <p id="jd-hint" class="length-hint">Takes about 20 seconds.</p>
          <span
            id="jd-count"
            class="char-count"
            :class="{ 'char-count--over': charCount > MAX_JOB_DESCRIPTION_CHARS }"
            aria-live="polite"
          >
            {{ charCount.toLocaleString() }} / {{ MAX_JOB_DESCRIPTION_CHARS.toLocaleString() }}
          </span>
          <button
            class="analyze-btn"
            type="button"
            :disabled="!canSubmit"
            @click="analyze"
          >
            <v-icon size="15" aria-hidden="true">mdi-creation</v-icon>
            Analyze fit
          </button>
        </div>
        <transition name="fade-up">
          <p v-if="errorMessage" class="input-error" role="alert">
            <v-icon size="14" aria-hidden="true">mdi-alert-circle-outline</v-icon>
            {{ errorMessage }}
          </p>
        </transition>
      </section>

      <!-- ── Run phase ────────────────────────────────────────────────── -->
      <template v-else>

        <div class="run-bar">
          <p class="run-bar__title">
            <span class="run-bar__eyebrow">{{ phase === 'running' ? 'Analyzing' : 'Analysis for' }}</span>
            <span>{{ jobTitle || 'your job posting' }}<template v-if="company"> at {{ company }}</template></span>
          </p>
          <button class="ghost-btn" type="button" @click="startOver">
            <v-icon size="14" aria-hidden="true">mdi-refresh</v-icon>
            New analysis
          </button>
        </div>

        <!-- Live steps -->
        <ol class="step-list" aria-label="Analysis progress" aria-live="polite">
          <li
            v-for="(step, i) in STEPS"
            :key="step.key"
            class="step-item"
            :class="`step-item--${stepState(i)}`"
            :aria-current="stepState(i) === 'active' ? 'step' : undefined"
          >
            <span class="step-pip" aria-hidden="true">
              <transition name="pip" mode="out-in">
                <v-icon v-if="stepState(i) === 'done'" key="done" size="15">mdi-check-circle</v-icon>
                <v-icon v-else-if="stepState(i) === 'active'" key="active" size="15" class="icon-spin">mdi-loading</v-icon>
                <v-icon v-else-if="stepState(i) === 'stopped'" key="stopped" size="15">mdi-minus-circle-outline</v-icon>
                <v-icon v-else key="pending" size="15">mdi-circle-outline</v-icon>
              </transition>
            </span>
            <span class="step-label">{{ step.label }}</span>
            <span v-if="stepDetail(i)" class="step-detail">{{ stepDetail(i) }}</span>
            <span class="sr-only">({{ stepState(i) }})</span>
          </li>
        </ol>

        <transition name="fade-up">
          <p v-if="errorMessage" class="run-error" role="alert">
            <v-icon size="15" aria-hidden="true">mdi-alert-circle-outline</v-icon>
            {{ errorMessage }}
          </p>
        </transition>

        <!-- Summary -->
        <transition name="fade-up">
          <section v-if="assessment" class="summary-card" aria-labelledby="summary-heading">
            <div class="summary-top">
              <span class="rating-badge" :class="`rating-badge--${assessment.rating}`">
                {{ RATING_LABELS[assessment.rating] }}
              </span>
              <span class="must-haves">
                <strong>{{ mustHaves.met }} of {{ mustHaves.total }}</strong> must-haves backed by evidence
              </span>
            </div>
            <div
              class="score-bar"
              role="meter"
              aria-label="Overall fit score"
              :aria-valuenow="Math.round(assessment.fitScore * 100)"
              aria-valuemin="0"
              aria-valuemax="100"
            >
              <div class="score-bar__fill" :style="{ width: `${assessment.fitScore * 100}%` }" />
            </div>
            <h2 id="summary-heading" class="summary-headline">{{ assessment.summary.headline }}</h2>
            <div class="summary-columns">
              <div>
                <h3 class="summary-subhead summary-subhead--strengths">Strongest matches</h3>
                <ul class="summary-list">
                  <li v-for="s in assessment.summary.strengths" :key="s">{{ s }}</li>
                </ul>
              </div>
              <div v-if="assessment.summary.gaps.length">
                <h3 class="summary-subhead">Not shown in the portfolio</h3>
                <ul class="summary-list">
                  <li v-for="g in assessment.summary.gaps" :key="g">{{ g }}</li>
                </ul>
              </div>
            </div>
          </section>
        </transition>

        <!-- Requirements -->
        <transition name="fade-up">
          <section v-if="requirements.length" class="req-section" aria-labelledby="req-heading">
            <h2 id="req-heading" class="section-title">What the role asks for</h2>
            <ul class="req-list">
              <li v-for="r in orderedRequirements" :key="r.id" class="req-item">
                <div class="req-head">
                  <span class="importance-tag" :class="`importance-tag--${r.importance}`">
                    {{ r.importance === 'must_have' ? 'Must have' : 'Nice to have' }}
                  </span>
                  <span class="req-text">{{ r.text }}</span>
                  <span v-if="verdicts[r.id]" class="status-chip" :class="`status-chip--${verdicts[r.id].status}`">
                    <v-icon size="13" aria-hidden="true">{{ STATUS[verdicts[r.id].status].icon }}</v-icon>
                    {{ STATUS[verdicts[r.id].status].label }}
                  </span>
                  <span v-else-if="phase === 'running'" class="status-chip status-chip--checking">
                    Checking...
                  </span>
                </div>

                <template v-if="verdicts[r.id]">
                  <p class="req-reason">{{ verdicts[r.id].reason }}</p>
                  <ul v-if="verdicts[r.id].citations.length" class="citation-list" aria-label="Evidence">
                    <li v-for="(c, ci) in verdicts[r.id].citations" :key="ci" class="citation">
                      <router-link v-if="citationLink(c)" :to="citationLink(c)" class="citation-source">
                        <v-icon size="12" aria-hidden="true">{{ c.entityType === 'work' ? 'mdi-briefcase-outline' : 'mdi-folder-outline' }}</v-icon>
                        {{ c.label }}
                      </router-link>
                      <span v-else class="citation-source citation-source--plain">
                        <v-icon size="12" aria-hidden="true">mdi-note-text-outline</v-icon>
                        Samuel's notes
                      </span>
                      <span class="citation-supports">{{ c.supports }}</span>
                    </li>
                  </ul>
                </template>
              </li>
            </ul>
          </section>
        </transition>

        <!-- Cover letter -->
        <transition name="fade-up">
          <section v-if="coverLetter" class="letter-card" aria-labelledby="letter-heading">
            <div class="letter-head">
              <h2 id="letter-heading" class="section-title">Draft cover letter</h2>
              <button class="ghost-btn" type="button" :disabled="letterStreaming" @click="copyLetter">
                <v-icon size="14" aria-hidden="true">{{ copied ? 'mdi-check' : 'mdi-content-copy' }}</v-icon>
                {{ copied ? 'Copied' : 'Copy' }}
              </button>
            </div>
            <p class="letter-body" :aria-busy="String(letterStreaming)">{{ coverLetter }}<span v-if="letterStreaming" class="caret" aria-hidden="true" /></p>
            <p class="letter-note">Written only from the matches above that had evidence behind them.</p>
          </section>
        </transition>

        <p v-if="phase === 'done'" class="footnote">
          "Not shown in the portfolio" means just that. It doesn't mean Samuel can't do it.
          Curious about something?
          <router-link to="/contact" class="footnote-link">Ask him</router-link>.
        </p>
      </template>

    </div>
  </div>
</template>

<script setup>
import { computed, ref } from 'vue'
import { storeToRefs } from 'pinia'
import { STEPS, useJobFitStore } from '@/stores/jobFitStore'
import { MAX_JOB_DESCRIPTION_CHARS, MIN_JOB_DESCRIPTION_CHARS } from '@/services/jobFitService'

const RATING_LABELS = {
  strong_fit: 'Strong fit',
  good_fit: 'Good fit',
  partial_fit: 'Partial fit',
  weak_fit: 'Weak fit',
}

const STATUS = {
  strong: { label: 'Strong match', icon: 'mdi-check-circle' },
  partial: { label: 'Partial match', icon: 'mdi-circle-half-full' },
  no_evidence: { label: 'Not in portfolio', icon: 'mdi-circle-outline' },
}

// State lives in the store so it survives clicking through to a cited project and back
const store = useJobFitStore()
const {
  jobDescription, phase, currentStep, jobTitle, company, requirements,
  evidenceCount, assessment, coverLetter, errorMessage,
} = storeToRefs(store)
const copied = ref(false)

const charCount = computed(() => jobDescription.value.trim().length)
const canSubmit = computed(() =>
  charCount.value >= MIN_JOB_DESCRIPTION_CHARS && charCount.value <= MAX_JOB_DESCRIPTION_CHARS)

const verdicts = computed(() =>
  Object.fromEntries((assessment.value?.requirements ?? []).map(v => [v.requirementId, v])))

// Must-haves first, otherwise keep the order they appeared in the posting
const orderedRequirements = computed(() => [
  ...requirements.value.filter(r => r.importance === 'must_have'),
  ...requirements.value.filter(r => r.importance !== 'must_have'),
])

const mustHaves = computed(() => {
  const [met, total] = (assessment.value?.mustHavesMet ?? '0/0').split('/')
  return { met, total }
})

const letterStreaming = computed(() => phase.value === 'running' && currentStep.value === STEPS.length - 1)

function stepState(i) {
  if (currentStep.value > i) return 'done'
  if (currentStep.value === i) {
    if (phase.value === 'running') return 'active'
    return errorMessage.value ? 'stopped' : 'done'
  }
  return phase.value === 'done' && errorMessage.value ? 'skipped' : 'pending'
}

function stepDetail(i) {
  if (i === 0 && requirements.value.length) return `${requirements.value.length} requirements found`
  if (i === 1 && evidenceCount.value !== null) return `${evidenceCount.value} pieces of evidence`
  if (i === 2 && assessment.value) return RATING_LABELS[assessment.value.rating]
  return null
}

function citationLink(c) {
  if (c.entityType === 'project') return `/projects?id=${c.entityId}`
  if (c.entityType === 'work') return `/work-experience?id=${c.entityId}`
  return null // information entries have no page of their own
}

function analyze() {
  if (!canSubmit.value) return
  copied.value = false
  store.analyze()
}

function startOver() {
  copied.value = false
  store.startOver()
}

async function copyLetter() {
  try {
    await navigator.clipboard.writeText(coverLetter.value)
    copied.value = true
    setTimeout(() => { copied.value = false }, 2000)
  } catch { /* clipboard blocked, nothing useful to do */ }
}
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
.icon-spin { animation: spin 0.75s linear infinite; }
@keyframes spin { to { transform: rotate(360deg); } }

/* ─────────────────────────────────────────────────────────────────────
   Page shell
───────────────────────────────────────────────────────────────────── */
.jobfit-page {
  min-height: 100%;
  font-family: 'Raleway', sans-serif;
  background: rgb(var(--v-theme-background));
  padding: 2.5rem 1.25rem 4rem;
}
.jobfit-inner {
  max-width: 820px;
  margin: 0 auto;
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

/* ─────────────────────────────────────────────────────────────────────
   Header
───────────────────────────────────────────────────────────────────── */
.jobfit-header {
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  gap: 0.85rem;
}
.header-orb {
  position: relative;
  width: 64px; height: 64px;
  display: flex; align-items: center; justify-content: center;
}
.header-orb__ring {
  position: absolute;
  inset: 0;
  border-radius: 50%;
  border: 1px solid rgba(var(--v-theme-secondary), .3);
}
.header-orb__ring--1 { animation: orb-pulse 2.8s ease-in-out infinite; }
.header-orb__ring--2 { animation: orb-pulse 2.8s ease-in-out infinite .7s; opacity: .5; transform: scale(1.18); }
@keyframes orb-pulse {
  0%, 100% { transform: scale(1); opacity: .6; }
  50%      { transform: scale(1.12); opacity: .15; }
}
.header-orb__icon { color: rgb(var(--v-theme-secondary)) !important; z-index: 1; }
.jobfit-title {
  font-family: 'Patua One', serif;
  font-size: clamp(1.6rem, 4vw, 2.2rem);
  color: rgb(var(--v-theme-on-background));
  margin: 0;
  line-height: 1.15;
}
.jobfit-intro {
  font-size: 0.9rem;
  line-height: 1.8;
  color: rgba(var(--v-theme-on-background), .65);
  margin: 0;
  max-width: 600px;
}
.header-features {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: 0.5rem;
}
.header-features li {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  font-size: 0.74rem;
  font-weight: 600;
  color: rgba(var(--v-theme-on-background), .7);
  background: rgba(var(--v-theme-secondary), .05);
  border: 1px solid rgba(var(--v-theme-secondary), .14);
  border-radius: 999px;
  padding: 0.3rem 0.75rem;
}
.header-features .v-icon { color: rgb(var(--v-theme-secondary)) !important; }

/* ─────────────────────────────────────────────────────────────────────
   Shared card + buttons
───────────────────────────────────────────────────────────────────── */
.input-card,
.summary-card,
.req-section,
.letter-card {
  background: rgb(var(--v-theme-surface));
  border: 1px solid rgba(var(--v-theme-secondary), .14);
  border-radius: 10px;
  padding: 1.25rem 1.35rem;
  box-shadow: 0 4px 24px rgba(0,0,0,.18);
}
.section-title {
  font-family: 'Patua One', serif;
  font-size: 1.1rem;
  font-weight: 400;
  color: rgb(var(--v-theme-on-surface));
  margin: 0 0 0.9rem;
}
.analyze-btn,
.ghost-btn {
  display: inline-flex;
  align-items: center;
  gap: 0.38rem;
  font-family: 'Raleway', sans-serif;
  font-weight: 700;
  border-radius: 6px;
  cursor: pointer;
  transition: all .15s;
  white-space: nowrap;
}
.analyze-btn {
  font-size: 0.82rem;
  letter-spacing: .04em;
  padding: 0.5rem 1.1rem;
  border: 1px solid rgba(var(--v-theme-secondary), .45);
  background: rgba(var(--v-theme-secondary), .12);
  color: rgb(var(--v-theme-secondary));
}
.analyze-btn:hover:not(:disabled) {
  background: rgba(var(--v-theme-secondary), .2);
  border-color: rgba(var(--v-theme-secondary), .75);
  box-shadow: 0 0 14px rgba(var(--v-theme-secondary), .22);
}
.ghost-btn {
  font-size: 0.74rem;
  padding: 0.32rem 0.7rem;
  border: 1px solid rgba(var(--v-theme-secondary), .22);
  background: transparent;
  color: rgba(var(--v-theme-on-surface), .7);
}
.ghost-btn:hover:not(:disabled) {
  background: rgba(var(--v-theme-secondary), .1);
  border-color: rgba(var(--v-theme-secondary), .5);
  color: rgb(var(--v-theme-secondary));
}
.analyze-btn:disabled, .ghost-btn:disabled { opacity: .38; cursor: not-allowed; }
.analyze-btn:focus-visible, .ghost-btn:focus-visible {
  outline: 2px solid rgb(var(--v-theme-secondary));
  outline-offset: 2px;
}

/* ─────────────────────────────────────────────────────────────────────
   Input phase
───────────────────────────────────────────────────────────────────── */
.input-label {
  display: block;
  font-size: 0.72rem;
  font-weight: 700;
  letter-spacing: .08em;
  text-transform: uppercase;
  color: rgba(var(--v-theme-on-surface), .55);
  margin-bottom: 0.5rem;
}
.jd-input {
  width: 100%;
  background: rgba(var(--v-theme-secondary), .03);
  border: 1px solid rgba(var(--v-theme-secondary), .2);
  border-radius: 8px;
  padding: 0.8rem 0.9rem;
  font-family: 'Raleway', sans-serif;
  font-size: 0.85rem;
  line-height: 1.65;
  color: rgb(var(--v-theme-on-surface));
  resize: vertical;
  outline: none;
  transition: border-color .15s, background .15s;
}
.jd-input:focus { border-color: rgba(var(--v-theme-secondary), .55); background: rgba(var(--v-theme-secondary), .05); }
.jd-input::placeholder { color: rgba(var(--v-theme-on-surface), .32); }
.input-footer {
  display: flex;
  align-items: center;
  gap: 0.8rem;
  margin-top: 0.7rem;
}
.length-hint {
  font-size: 0.72rem;
  font-style: italic;
  color: rgba(var(--v-theme-on-surface), .42);
  margin: 0;
  flex: 1;
}
.char-count {
  font-size: 0.72rem;
  font-variant-numeric: tabular-nums;
  color: rgba(var(--v-theme-on-surface), .45);
}
.char-count--over { color: rgb(var(--v-theme-error)); font-weight: 700; }
.input-error,
.run-error {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  font-size: 0.8rem;
  font-weight: 600;
  color: rgb(var(--v-theme-error));
  margin: 0.7rem 0 0;
}
.run-error {
  margin: 0;
  padding: 0.75rem 1rem;
  border-radius: 8px;
  border: 1px solid rgba(var(--v-theme-error), .35);
  background: rgba(var(--v-theme-error), .07);
}

/* ─────────────────────────────────────────────────────────────────────
   Run bar + steps
───────────────────────────────────────────────────────────────────── */
.run-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  flex-wrap: wrap;
}
.run-bar__title {
  display: flex;
  flex-direction: column;
  margin: 0;
  font-size: 1rem;
  font-weight: 700;
  color: rgb(var(--v-theme-on-background));
}
.run-bar__eyebrow {
  font-size: 0.68rem;
  font-weight: 700;
  letter-spacing: .1em;
  text-transform: uppercase;
  color: rgb(var(--v-theme-secondary));
}
.step-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 0.6rem;
}
.step-item {
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
  padding: 0.7rem 0.8rem;
  border-radius: 8px;
  border: 1px solid rgba(var(--v-theme-secondary), .1);
  background: rgba(var(--v-theme-secondary), .03);
  transition: border-color .25s, background .25s, opacity .25s;
}
.step-pip { display: flex; }
.step-label { font-size: 0.78rem; font-weight: 700; line-height: 1.3; }
.step-detail { font-size: 0.7rem; color: rgba(var(--v-theme-on-background), .55); }
.step-item--pending, .step-item--skipped { opacity: .45; }
.step-item--pending .step-pip, .step-item--skipped .step-pip { color: rgba(var(--v-theme-on-background), .4); }
.step-item--active {
  border-color: rgba(var(--v-theme-secondary), .5);
  background: rgba(var(--v-theme-secondary), .08);
  box-shadow: 0 0 16px rgba(var(--v-theme-secondary), .12);
}
.step-item--active .step-pip { color: rgb(var(--v-theme-secondary)); }
.step-item--done .step-pip { color: rgb(var(--v-theme-success)); }
.step-item--stopped .step-pip { color: rgb(var(--v-theme-error)); }
.step-pip .v-icon { color: inherit !important; }

/* ─────────────────────────────────────────────────────────────────────
   Summary
───────────────────────────────────────────────────────────────────── */
.summary-top {
  display: flex;
  align-items: center;
  gap: 0.8rem;
  flex-wrap: wrap;
}
.rating-badge {
  font-family: 'Patua One', serif;
  font-size: 1.05rem;
  padding: 0.25rem 0.8rem;
  border-radius: 6px;
  border: 1px solid currentColor;
}
.rating-badge--strong_fit  { color: rgb(var(--v-theme-success)); background: rgba(var(--v-theme-success), .08); }
.rating-badge--good_fit    { color: rgb(var(--v-theme-secondary)); background: rgba(var(--v-theme-secondary), .08); }
.rating-badge--partial_fit { color: rgb(var(--v-theme-warning)); background: rgba(var(--v-theme-warning), .08); }
.rating-badge--weak_fit    { color: rgba(var(--v-theme-on-surface), .6); background: rgba(var(--v-theme-on-surface), .05); }
.must-haves { font-size: 0.82rem; color: rgba(var(--v-theme-on-surface), .7); }
.must-haves strong { color: rgb(var(--v-theme-on-surface)); }
.score-bar {
  height: 6px;
  border-radius: 999px;
  background: rgba(var(--v-theme-secondary), .12);
  margin: 0.85rem 0 1rem;
  overflow: hidden;
}
.score-bar__fill {
  height: 100%;
  border-radius: inherit;
  background: linear-gradient(90deg, rgb(var(--v-theme-accent)), rgb(var(--v-theme-secondary)));
  transition: width .8s cubic-bezier(.2,.8,.2,1);
}
.summary-headline {
  font-size: 0.98rem;
  font-weight: 600;
  line-height: 1.6;
  color: rgb(var(--v-theme-on-surface));
  margin: 0 0 1rem;
}
.summary-columns {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1.25rem;
}
.summary-subhead {
  font-size: 0.7rem;
  font-weight: 700;
  letter-spacing: .08em;
  text-transform: uppercase;
  color: rgba(var(--v-theme-on-surface), .5);
  margin: 0 0 0.45rem;
}
.summary-subhead--strengths { color: rgb(var(--v-theme-success)); }
.summary-list {
  margin: 0;
  padding-left: 1.1rem;
  font-size: 0.82rem;
  line-height: 1.65;
  color: rgba(var(--v-theme-on-surface), .78);
}

/* ─────────────────────────────────────────────────────────────────────
   Requirements
───────────────────────────────────────────────────────────────────── */
.req-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
}
.req-item {
  padding: 0.85rem 0;
  border-top: 1px solid rgba(var(--v-theme-secondary), .08);
}
.req-item:first-child { border-top: none; padding-top: 0; }
.req-head {
  display: flex;
  align-items: baseline;
  gap: 0.6rem;
  flex-wrap: wrap;
}
.importance-tag {
  font-size: 0.62rem;
  font-weight: 700;
  letter-spacing: .07em;
  text-transform: uppercase;
  padding: 0.12rem 0.45rem;
  border-radius: 4px;
  flex-shrink: 0;
}
.importance-tag--must_have    { color: rgb(var(--v-theme-secondary)); background: rgba(var(--v-theme-secondary), .1); }
.importance-tag--nice_to_have { color: rgba(var(--v-theme-on-surface), .55); background: rgba(var(--v-theme-on-surface), .06); }
.req-text {
  flex: 1;
  min-width: 200px;
  font-size: 0.88rem;
  font-weight: 600;
  color: rgb(var(--v-theme-on-surface));
}
.status-chip {
  display: inline-flex;
  align-items: center;
  gap: 0.3rem;
  font-size: 0.72rem;
  font-weight: 700;
  padding: 0.18rem 0.55rem;
  border-radius: 999px;
  flex-shrink: 0;
}
.status-chip .v-icon { color: inherit !important; }
.status-chip--strong      { color: rgb(var(--v-theme-success)); background: rgba(var(--v-theme-success), .1); }
.status-chip--partial     { color: rgb(var(--v-theme-warning)); background: rgba(var(--v-theme-warning), .1); }
.status-chip--no_evidence { color: rgba(var(--v-theme-on-surface), .55); background: rgba(var(--v-theme-on-surface), .06); }
.status-chip--checking {
  color: rgba(var(--v-theme-on-surface), .45);
  background: linear-gradient(90deg,
    rgba(var(--v-theme-secondary), .04) 0%,
    rgba(var(--v-theme-secondary), .14) 50%,
    rgba(var(--v-theme-secondary), .04) 100%);
  background-size: 200% 100%;
  animation: shimmer 1.4s linear infinite;
}
@keyframes shimmer { to { background-position: -200% 0; } }
.req-reason {
  font-size: 0.8rem;
  line-height: 1.6;
  color: rgba(var(--v-theme-on-surface), .68);
  margin: 0.45rem 0 0;
}
.citation-list {
  list-style: none;
  padding: 0;
  margin: 0.5rem 0 0;
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
}
.citation {
  display: flex;
  align-items: baseline;
  gap: 0.5rem;
  font-size: 0.76rem;
  flex-wrap: wrap;
}
.citation-source {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  font-weight: 700;
  color: rgb(var(--v-theme-secondary));
  text-decoration: none;
  flex-shrink: 0;
}
.citation-source .v-icon { color: inherit !important; }
a.citation-source:hover { text-decoration: underline; }
a.citation-source:focus-visible { outline: 2px solid rgb(var(--v-theme-secondary)); outline-offset: 2px; border-radius: 2px; }
.citation-source--plain { color: rgba(var(--v-theme-on-surface), .6); }
.citation-supports { color: rgba(var(--v-theme-on-surface), .55); }

/* ─────────────────────────────────────────────────────────────────────
   Cover letter
───────────────────────────────────────────────────────────────────── */
.letter-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
}
.letter-head .section-title { margin: 0; }
.letter-body {
  white-space: pre-wrap;
  font-size: 0.88rem;
  line-height: 1.8;
  color: rgba(var(--v-theme-on-surface), .85);
  margin: 0.9rem 0 0;
}
.caret {
  display: inline-block;
  width: 2px;
  height: 1em;
  margin-left: 2px;
  vertical-align: text-bottom;
  background: rgb(var(--v-theme-secondary));
  animation: blink 1s steps(1) infinite;
}
@keyframes blink { 50% { opacity: 0; } }
.letter-note {
  font-size: 0.72rem;
  font-style: italic;
  color: rgba(var(--v-theme-on-surface), .42);
  margin: 0.9rem 0 0;
}
.footnote {
  font-size: 0.78rem;
  line-height: 1.7;
  text-align: center;
  color: rgba(var(--v-theme-on-background), .5);
  margin: 0.5rem 0 0;
}
.footnote-link { color: rgb(var(--v-theme-secondary)); font-weight: 600; text-decoration: none; }
.footnote-link:hover { text-decoration: underline; }

/* ─────────────────────────────────────────────────────────────────────
   Transitions
───────────────────────────────────────────────────────────────────── */
.fade-up-enter-active { transition: opacity .3s ease, transform .3s ease; }
.fade-up-leave-active { transition: opacity .15s ease; }
.fade-up-enter-from   { opacity: 0; transform: translateY(8px); }
.fade-up-leave-to     { opacity: 0; }
.pip-enter-active, .pip-leave-active { transition: all .18s ease; }
.pip-enter-from { opacity: 0; transform: scale(.4) rotate(-90deg); }
.pip-leave-to   { opacity: 0; transform: scale(.4) rotate(90deg); }

/* ─────────────────────────────────────────────────────────────────────
   Responsive + motion
───────────────────────────────────────────────────────────────────── */
@media (max-width: 700px) {
  .jobfit-page { padding: 1.75rem 0.85rem 3rem; }
  .step-list { grid-template-columns: 1fr 1fr; }
  .summary-columns { grid-template-columns: 1fr; }
  .input-footer { flex-wrap: wrap; }
  .length-hint { flex-basis: 100%; }
  .analyze-btn { margin-left: auto; }
}
@media (prefers-reduced-motion: reduce) {
  /* Decorative motion off. The step spinner stays (slower): it's the only sign a
     step is still working, and a frozen one reads as the page being stuck. */
  .header-orb__ring, .caret, .status-chip--checking { animation: none; }
  .icon-spin { animation-duration: 1.6s; }
  .score-bar__fill { transition: none; }
}
</style>
