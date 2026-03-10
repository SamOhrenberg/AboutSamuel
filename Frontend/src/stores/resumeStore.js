import { defineStore } from 'pinia'
import axios from 'axios'


export const useResumeStore = defineStore('resume', () => {
  const resumeData = ref(null)
  const loading = ref(false)

  async function fetchResume(jobTitle, jobDescription) {
    try {
      loading.value = true

      const isTailored = !!(jobTitle?.trim() || jobDescription?.trim())
      let data

      if (isTailored) {
        const res = await axios.post(`${import.meta.env.VITE_API_URL}/chat/resume`, {
          title: jobTitle?.trim() || null,
          jobDescription: jobDescription?.trim() || null
        })
        data = res.data
      } else {
        const res = await axios.get(`${import.meta.env.VITE_API_URL}/chat/resume`)
        data = res.data
      }

      resumeData.value = data

    } catch (err) {
      console.error('Error fetching resume:', err)
    } finally {
      loading.value = false
    }
  }

  return { resumeData, loading, fetchResume }
})