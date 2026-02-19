<template>
  <v-app>
    <a href="#main-content" class="skip-link">Skip to main content</a>
    <main class="d-flex flex-row h-screen">
      <div id="page-wrapper">
        <Navbar />
        <div id="content-wrapper">
          <div
            class="flex-grow-1 overflow-y-auto"
            role="main"
            id="main-content"
          >
            <RouterView />
          </div>
          <aside id="chat-box" aria-label="AI Chat Assistant">
            <ChatBox />
          </aside>
        </div>
      </div>
    </main>
  </v-app>
</template>

<script setup>
import { RouterView } from 'vue-router'
import ChatBox from './components/ChatBox.vue'
import Navbar from './components/Navbar.vue'

import { useResumeStore } from '@/stores/resumeStore';
import { onMounted } from 'vue';

const resumeStore = useResumeStore();

onMounted(() => {
  if (!resumeStore.resumeContent) {
    resumeStore.fetchResume();
  }
});
</script>

<style scoped>
/* Skip link — visually hidden until focused, for keyboard/screen reader users */
.skip-link {
  position: absolute;
  top: -999px;
  left: 0;
  background: #00acac;
  color: #fff;
  padding: 0.5rem 1rem;
  z-index: 9999;
  font-weight: bold;
  text-decoration: none;
  border-radius: 0 0 4px 0;
}
.skip-link:focus {
  top: 0;
}

@media (min-width: 780px) {
  #chat-box {
  }
}
@media (max-width: 570px) {
  #chat-box {
    height: 10%;
    width: auto;
    max-width: 100%;
  }
  #content-wrapper {
    flex-direction: column;
  }
  #page-wrapper {
    min-width: 350px;
    overflow-x: scroll;
  }
}

@media (min-width: 571px) {
  #chat-box {
    height: 100%;
  }
}

@media (max-width: 780px) {
  #chat-box {
    z-index: 1000;
  }
}

#page-wrapper {
  display: flex;
  flex-direction: column;
  width: 100%;
}

#content-wrapper {
  display: flex;
  overflow: hidden;
  height: 100dvh;
}
</style>