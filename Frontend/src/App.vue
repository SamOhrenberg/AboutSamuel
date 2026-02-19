<template>
  <v-app>
    <a href="#main-content" class="skip-link">Skip to main content</a>

    <div id="app-shell">
      <Navbar />

      <div id="content-wrapper">
        <div id="main-content" role="main">

          <!-- Route transition: fade + slide up -->
          <RouterView v-slot="{ Component, route }">
            <Transition name="page" mode="out-in">
              <component :is="Component" :key="route.path" />
            </Transition>
          </RouterView>

          <Footer />
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
import Footer from './components/Footer.vue'
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

#app-shell {
  display: flex;
  flex-direction: column;
  height: 100dvh;
  width: 100%;
  overflow: hidden;
}

#content-wrapper {
  display: flex;
  flex-direction: row;
  flex: 1;
  min-height: 0;
  overflow: hidden;
  width: 100%;
}

#main-content {
  flex: 1;
  overflow-y: auto;
  min-width: 0;
  display: flex;
  flex-direction: column;
}

#main-content > footer {
  flex: 0;
}

#chat-box {
  flex-shrink: 0;
  height: 100%;
}

@media (max-width: 599px) {
  #chat-box {
    width: 0;
    overflow: visible;
  }
}
</style>

<!-- Page transition — global so it applies outside scoped -->
<style>
.page-enter-active,
.page-leave-active {
  transition: opacity 0.2s ease, transform 0.2s ease;
}

.page-enter-from {
  opacity: 0;
  transform: translateY(12px);
}

.page-leave-to {
  opacity: 0;
  transform: translateY(-8px);
}
</style>