import { defineStore } from 'pinia';
import { ref } from 'vue';
import axios from 'axios';

export const useResumeStore = defineStore('resume', () => {
  const resumeContent = ref(localStorage.getItem('resume') || '');
  const loading = ref(!resumeContent.value); // Set loading to true if no cached resume exists

  async function fetchResume(jobTitle) {
    try {
      loading.value = true;
      var baseUrl = `${import.meta.env.VITE_API_URL}/chat/resume`;
      if (jobTitle && jobTitle !== '') {
        baseUrl += `/${jobTitle}`;
      }
      const response = await axios.get(baseUrl);
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
