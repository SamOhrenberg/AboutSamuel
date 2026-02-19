import { defineStore } from 'pinia'
import { ref } from 'vue'
import { getProjects } from '@/services/projectService'

export const useProjectStore = defineStore('projects', () => {
  const projects = ref([])
  const loading = ref(false)
  const error = ref(null)

  async function fetchProjects() {
    loading.value = true
    error.value = null
    try {
      projects.value = await getProjects()
    } catch (err) {
      console.error('Failed to load projects:', err)
      error.value = 'Failed to load projects. Please try again later.'
    } finally {
      loading.value = false
    }
  }

  const featured = () => projects.value.filter((p) => p.isFeatured)

  return { projects, loading, error, fetchProjects, featured }
})