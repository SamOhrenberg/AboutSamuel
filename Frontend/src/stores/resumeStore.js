import { defineStore } from 'pinia';
import { ref } from 'vue';
import axios from 'axios';

export const useResumeStore = defineStore('resume', () => {
  const resumeContent = ref(localStorage.getItem('resume') || '');
  const loading = ref(!resumeContent.value); // Set loading to true if no cached resume exists

  async function fetchResume() {
    try {
      loading.value = true;
      const response = await axios.get(`${import.meta.env.VITE_API_URL}/chat/resume/true`);
      resumeContent.value = response.data;
      localStorage.setItem('resume', response.data);
    } catch (error) {
      console.error('Error fetching resume:', error);
    } finally {
      loading.value = false;
    }
  }

  return { resumeContent, loading, fetchResume };
});
