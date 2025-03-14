<template>
  <div class="menu-bar elevation-2">
    Menu Bar
  </div>
  <div>
    <div v-if="resumeStore.loading" class="d-flex flex-column justify-center align-center" style="height: 100vh;">
      <v-progress-circular indeterminate size="64"></v-progress-circular>
      <p class="mt-4 text-center">My resume is being generated. Please wait...</p>
    </div>
    <div v-else v-html="resumeStore.resumeContent"></div>
  </div>
</template>

<script setup>
import { useResumeStore } from '@/stores/resumeStore';

const resumeStore = useResumeStore();
</script>

<style>
  .menu-bar {
    position: sticky;
    top: 0;
    z-index: 10;
    width: 100%;
    background: rgb(var(--v-theme-surface)) !important;
    color: rgb(var(--v-theme-primary)) !important;
    padding: 0 16px;
  }
</style>
