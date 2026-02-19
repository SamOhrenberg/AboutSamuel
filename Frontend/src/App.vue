<template>
  <v-app>
    <a href="#main-content" class="skip-link">Skip to main content</a>

    <div id="app-shell">
      <Navbar />

      <div id="content-wrapper">
        <div role="main" id="main-content">
          <RouterView />
        </div>

        <aside id="chat-box" aria-label="AI Chat Assistant">
          <ChatBox />
        </aside>
      </div>
    </div>

  </v-app>
</template>

<script setup>
import { RouterView } from 'vue-router'
import ChatBox from './components/ChatBox.vue'
import Navbar from './components/Navbar.vue'
import { useResumeStore } from '@/stores/resumeStore'
import { onMounted } from 'vue'

const resumeStore = useResumeStore()

onMounted(() => {
  if (!resumeStore.resumeContent) {
    resumeStore.fetchResume()
  }
})
</script>

<style scoped>
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

/* Full viewport column: navbar on top, content below */
#app-shell {
  display: flex;
  flex-direction: column;
  height: 100dvh;
  width: 100%;
  overflow: hidden;
}

/* Row beneath navbar — fills all remaining height exactly */
#content-wrapper {
  display: flex;
  flex-direction: row;
  flex: 1;
  min-height: 0; /* critical: allows flex children to shrink below content size */
  overflow: hidden;
  width: 100%;
}

/* Scrollable page content */
#main-content {
  flex: 1;
  overflow-y: auto;
  min-width: 0;
}

/* Chat panel */
#chat-box {
  flex-shrink: 0;
  height: 100%;
}

/* Mobile: collapse to zero width but keep mounted for the FAB */
@media (max-width: 599px) {
  #chat-box {
    width: 0;
    overflow: visible;
  }
}
</style>