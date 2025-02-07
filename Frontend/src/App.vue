<script setup>
import { RouterView } from 'vue-router'
import ChatBox from './components/ChatBox.vue'
</script>

<template>
  <main class="d-flex flex-row">
    <RouterView />
    <ChatBox />
  </main>
</template>
