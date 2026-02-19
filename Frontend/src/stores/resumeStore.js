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
  const loading = ref(!cached); // Only show loading if no valid cache exists

  async function fetchResume(jobTitle) {
    try {
      loading.value = true;
      let url = `${import.meta.env.VITE_API_URL}/chat/resume`;
      if (jobTitle && jobTitle.trim() !== '') {
        url += `/${encodeURIComponent(jobTitle.trim())}`;
      }
      const response = await axios.get(url);
      resumeContent.value = response.data;

      // Only cache the generic (no job title) resume to avoid polluting
      // the cache with role-specific variants
      if (!jobTitle || jobTitle.trim() === '') {
        setCachedResume(response.data);
      }
    } catch (error) {
      console.error('Error fetching resume:', error);
    } finally {
      loading.value = false;
    }
  }

  return { resumeContent, loading, fetchResume };
});