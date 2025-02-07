<script setup>
import { RouterView } from 'vue-router'
import ChatBox from './components/ChatBox.vue'
</script>

<template>
  <main class="d-flex flex-row h-screen">
    <div class="flex-grow-1">
      <RouterView />
    </div>
    <div id="chat-box">
      <ChatBox />
    </div>
  </main>
</template>

<style scoped>
@media (min-width: 780px) {
  #chat-box {
    height: 100vh;
  }
}

@media (max-width: 780px) {
  #chat-box {
  }
}
</style>
