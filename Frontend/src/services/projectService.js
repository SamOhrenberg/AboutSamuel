import axios from 'axios'

export async function getProjects() {
  try {
    const response = await axios.get(`${import.meta.env.VITE_API_URL}/project`)
    return response.data
  } catch (error) {
    console.error('Error fetching projects:', error)
    throw error
  }
}