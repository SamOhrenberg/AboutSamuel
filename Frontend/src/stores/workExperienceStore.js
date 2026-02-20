import { defineStore } from 'pinia'
import { ref } from 'vue'
import axios from 'axios'

export const useWorkExperienceStore = defineStore('workExperience', () => {
  const work    = ref([])
  const loading = ref(false)
  const error   = ref(null)

  async function fetchWork() {
    if (work.value.length) return

    try {
      loading.value = true
      error.value   = null
      const response = await axios.get(`${import.meta.env.VITE_API_URL}/workexperience`)
      work.value = response.data
    } catch (err) {
      console.error('Error fetching work experience:', err)
      error.value = 'Failed to load work experience.'
    } finally {
      loading.value = false
    }
  }

  return { work, loading, error, fetchWork }
})
