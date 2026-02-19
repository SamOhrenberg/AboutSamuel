<template>
  <v-app>
    <a href="#main-content" class="skip-link">Skip to main content</a>

    <div id="app-shell">
      <Navbar />

      <div id="content-wrapper">
        <div id="main-content" role="main">
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
/* ── Skip link ── */
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
  border-radius: 0 0 8px 0;
}
.skip-link:focus {
  top: 0;
  outline: 3px solid #fff;
  outline-offset: 2px;
}

/* ── Layout ── */
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

<!-- Global styles — not scoped so they apply throughout the app -->
<style>
/* ── Page route transition ── */
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

/* ── Scrollbar styling (dark theme) ── */
#main-content {
  scrollbar-width: thin;
  scrollbar-color: #00acac #001e1e;
}
#main-content::-webkit-scrollbar {
  width: 7px;
}
#main-content::-webkit-scrollbar-track {
  background: #001e1e;
}
#main-content::-webkit-scrollbar-thumb {
  background-color: #00acac;
  border-radius: 4px;
  border: 1px solid #001e1e;
}
#main-content::-webkit-scrollbar-thumb:hover {
  background-color: #8BE9FD;
}

/* ── Consistent border radius on Vuetify cards ── */
.v-card {
  border-radius: 12px !important;
}

/* ── Global focus ring — teal, consistent across all interactive elements ── */
*:focus-visible {
  outline: 2px solid #00acac !important;
  outline-offset: 3px !important;
  border-radius: 4px;
}

/* Suppress focus ring for mouse users (only show for keyboard) */
*:focus:not(:focus-visible) {
  outline: none !important;
}

/* ── Light theme scrollbar ── */
.v-theme--light #main-content {
  scrollbar-color: #007c7c #d6e8e8;
}
.v-theme--light #main-content::-webkit-scrollbar-track {
  background: #d6e8e8;
}
.v-theme--light #main-content::-webkit-scrollbar-thumb {
  background-color: #007c7c;
  border-color: #d6e8e8;
}
.v-theme--light #main-content::-webkit-scrollbar-thumb:hover {
  background-color: #005f5f;
}
</style>