import { defineStore } from 'pinia'
import { ref, watch } from 'vue'
import { useChatStore } from '@/stores/chatStore'
import { JobFitError, streamJobFit } from '@/services/jobFitService'

// Matches the agent's node names, in run order
export const STEPS = [
  { key: 'extract_requirements', label: 'Reading the job posting' },
  { key: 'retrieve_evidence', label: "Searching Samuel's experience" },
  { key: 'assess_fit', label: 'Weighing the evidence' },
  { key: 'write_cover_letter', label: 'Writing a cover letter' },
]

const STORAGE_KEY = 'jobFit:lastRun'

/**
 * Job Fit state lives here instead of in the page so it survives navigation:
 * click through to a cited project, hit back, and the analysis is still there.
 * A run started before leaving the page keeps streaming in the background.
 * Finished runs are also saved to sessionStorage so a refresh keeps them too.
 */
export const useJobFitStore = defineStore('jobFit', () => {
  const jobDescription = ref('')
  const phase = ref('input') // input | running | done
  const currentStep = ref(-1)
  const jobTitle = ref(null)
  const company = ref(null)
  const requirements = ref([])
  const evidenceCount = ref(null)
  const assessment = ref(null)
  const coverLetter = ref('')
  const errorMessage = ref('')
  let controller = null

  function reset() {
    currentStep.value = -1
    jobTitle.value = null
    company.value = null
    requirements.value = []
    evidenceCount.value = null
    assessment.value = null
    coverLetter.value = ''
    errorMessage.value = ''
  }

  function handleEvent(ev) {
    if (ev.step) {
      currentStep.value = STEPS.findIndex(s => s.key === ev.step)
    } else if (ev.requirements) {
      jobTitle.value = ev.jobTitle
      company.value = ev.company
      requirements.value = ev.requirements
    } else if ('evidenceCount' in ev) {
      evidenceCount.value = ev.evidenceCount
    } else if (ev.assessment) {
      assessment.value = ev.assessment
    } else if (ev.token) {
      coverLetter.value += ev.token
    } else if (ev.error) {
      errorMessage.value = ev.error
    } else if (ev.done && !errorMessage.value) {
      currentStep.value = STEPS.length
    }
  }

  async function analyze() {
    controller?.abort()
    reset()
    phase.value = 'running'
    const run = controller = new AbortController()

    try {
      await streamJobFit(
        jobDescription.value.trim(), useChatStore().userTrackingId, handleEvent, run.signal)
      phase.value = 'done'
    } catch (e) {
      if (e.name === 'AbortError') return
      errorMessage.value = e instanceof JobFitError
        ? e.message
        : 'Something went wrong with the analysis. Please try again.'
      // Failed before anything streamed (rate limit, bad input): back to the form, text intact
      phase.value = currentStep.value < 0 ? 'input' : 'done'
    } finally {
      if (controller === run) controller = null
    }
  }

  function startOver() {
    controller?.abort()
    controller = null
    reset()
    phase.value = 'input'
    save(null)
  }

  // ---- sessionStorage -----------------------------------------------------

  function save(snapshot) {
    try {
      if (snapshot) sessionStorage.setItem(STORAGE_KEY, JSON.stringify(snapshot))
      else sessionStorage.removeItem(STORAGE_KEY)
    } catch { /* storage blocked or full, the in-memory store still works */ }
  }

  function restore() {
    try {
      const saved = JSON.parse(sessionStorage.getItem(STORAGE_KEY) ?? 'null')
      if (!saved) return
      jobDescription.value = saved.jobDescription
      currentStep.value = saved.currentStep
      jobTitle.value = saved.jobTitle
      company.value = saved.company
      requirements.value = saved.requirements
      evidenceCount.value = saved.evidenceCount
      assessment.value = saved.assessment
      coverLetter.value = saved.coverLetter
      errorMessage.value = saved.errorMessage
      phase.value = 'done'
    } catch { /* corrupt or unavailable, start fresh */ }
  }

  restore()

  watch(phase, value => {
    if (value !== 'done') return
    save({
      jobDescription: jobDescription.value,
      currentStep: currentStep.value,
      jobTitle: jobTitle.value,
      company: company.value,
      requirements: requirements.value,
      evidenceCount: evidenceCount.value,
      assessment: assessment.value,
      coverLetter: coverLetter.value,
      errorMessage: errorMessage.value,
    })
  })

  return {
    jobDescription, phase, currentStep, jobTitle, company, requirements,
    evidenceCount, assessment, coverLetter, errorMessage,
    analyze, startOver,
  }
})
