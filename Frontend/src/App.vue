<script setup>
import { RouterView } from 'vue-router'
import ChatBox from './components/ChatBox/ChatBox.vue'
</script>

<template>
  <main class="w-75">
    <RouterView />
  </main>
  <div>
    <ChatBox />
  </div>
</template>
