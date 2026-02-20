import { defineStore } from 'pinia';
import { ref } from 'vue';
import axios from 'axios';

const CACHE_KEY = 'resume_content';
const CACHE_TIMESTAMP_KEY = 'resume_cached_at';
const CACHE_TTL_MS = 24 * 60 * 60 * 1000; // 24 hours

function getCachedResume() {
  try {
    const cachedAt = localStorage.getItem(CACHE_TIMESTAMP_KEY);
    if (!cachedAt) return null;

    const age = Date.now() - parseInt(cachedAt, 10);
    if (age > CACHE_TTL_MS) {
      // Cache expired — clear it
      localStorage.removeItem(CACHE_KEY);
      localStorage.removeItem(CACHE_TIMESTAMP_KEY);
      return null;
    }

    return localStorage.getItem(CACHE_KEY) || null;
  } catch {
    // localStorage unavailable (private browsing, storage quota, etc.)
    return null;
  }
}

function setCachedResume(content) {
  try {
    localStorage.setItem(CACHE_KEY, content);
    localStorage.setItem(CACHE_TIMESTAMP_KEY, Date.now().toString());
  } catch {
    // Silently fail if storage is unavailable
  }
}

export const useResumeStore = defineStore('resume', () => {
  const cached = getCachedResume();
  const resumeContent = ref(cached || '');
  const loading = ref(!cached);

  async function fetchResume(jobTitle, jobDescription) {
    try {
      loading.value = true;

      console.log(jobTitle, jobDescription);
      const isTailored = (jobTitle && jobTitle.trim() !== '') ||
                         (jobDescription && jobDescription.trim() !== '')

      let resumeHtml;

      if (isTailored) {
        const response = await axios.post(
          `${import.meta.env.VITE_API_URL}/chat/resume`,
          {
            title: jobTitle?.trim() || null,
            jobDescription: jobDescription?.trim() || null
          }
        );
        resumeHtml = response.data;
      } else {
        const response = await axios.get(
          `${import.meta.env.VITE_API_URL}/chat/resume`
        );
        resumeHtml = response.data;
        setCachedResume(resumeHtml);
      }

      resumeContent.value = resumeHtml;
    } catch (error) {
      console.error('Error fetching resume:', error);
    } finally {
      loading.value = false;
    }
  }

  return { resumeContent, loading, fetchResume };
});