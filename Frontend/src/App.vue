<template>
  <v-app>
    <a href="#main-content" class="skip-link">Skip to main content</a>

    <div id="app-shell" ref="appShell">
      <Navbar />

      <div id="content-wrapper">
        <div id="main-content" role="main">
          <RouterView v-slot="{ Component, route }">
            <Transition :name="transitionName" mode="out-in">
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
import { ref } from 'vue'
import { RouterView, useRouter } from 'vue-router'
import ChatBox from './components/ChatBox.vue'
import Navbar from './components/Navbar.vue'
import Footer from './components/Footer.vue'
import { useResumeStore } from '@/stores/resumeStore'
import { onMounted } from 'vue'
const appShell = ref(null)
const router = useRouter()

const resumeStore = useResumeStore()

// Route order — used to determine slide direction
const routeOrder = ['/', '/resume', '/projects', '/contact']

const transitionName = ref('page-forward')
router.beforeEach((to, from) => {
  // Set transition direction before the component swaps
  const toIdx   = routeOrder.indexOf(to.path)
  const fromIdx = routeOrder.indexOf(from.path)

  if (toIdx === -1 || fromIdx === -1) {
    transitionName.value = 'page-fade'
  } else {
    transitionName.value = toIdx > fromIdx ? 'page-forward' : 'page-back'
  }

  // Progress bar
  appShell.value?.classList.add('route-loading')
  appShell.value?.classList.remove('route-loading--done')
})

router.afterEach(() => {
  appShell.value?.classList.remove('route-loading')
  appShell.value?.classList.add('route-loading--done')
  setTimeout(() => appShell.value?.classList.remove('route-loading--done'), 400)
})


onMounted(() => {
  if (!resumeStore.resumeContent) {
    resumeStore.fetchResume()
  }

  // Preload main route chunks during idle time so nav feels instant
  const preload = () => {
    router.getRoutes()
      .filter(r => r.components?.default && typeof r.components.default === 'function')
      .forEach(r => {
        // @ts-ignore — call the lazy import to warm the chunk
        r.components.default()
      })
  }

  if ('requestIdleCallback' in window) {
    requestIdleCallback(preload, { timeout: 2000 })
  } else {
    setTimeout(preload, 1000)
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
  overflow-x: hidden;
}

#main-content>footer {
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
/* ── Forward: slide left ── */
.page-forward-enter-active,
.page-forward-leave-active,
.page-back-enter-active,
.page-back-leave-active,
.page-fade-enter-active,
.page-fade-leave-active {
  transition: opacity 0.22s ease, transform 0.22s ease;
}

.page-forward-leave-active,
.page-back-leave-active {
  position: absolute;
  width: 100%;
}

.page-forward-enter-from { opacity: 0; transform: translateX(60px); }
.page-forward-leave-to   { opacity: 0; transform: translateX(-60px); }

.page-back-enter-from    { opacity: 0; transform: translateX(-60px); }
.page-back-leave-to      { opacity: 0; transform: translateX(60px); }


.page-fade-enter-from,
.page-fade-leave-to      { opacity: 0; transform: translateY(8px); }

@media (prefers-reduced-motion: reduce) {
  .page-forward-enter-active,
  .page-forward-leave-active,
  .page-back-enter-active,
  .page-back-leave-active,
  .page-fade-enter-active,
  .page-fade-leave-active {
    transition: opacity 0.15s ease;
  }
  .page-forward-enter-from,
  .page-forward-leave-to,
  .page-back-enter-from,
  .page-back-leave-to     { transform: none; }
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
  outline: none !important;
  box-shadow:
    0 0 0 2px rgb(var(--v-theme-background)),
    0 0 0 4px #00acac !important;
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

/* ── Route loading indicator ── */
#app-shell::before {
  content: '';
  position: fixed;
  top: 0;
  left: 0;
  height: 2px;
  width: 0%;
  background: linear-gradient(90deg, #00acac, #8BE9FD);
  z-index: 9999;
  transition: width 0.3s ease, opacity 0.3s ease;
  opacity: 0;
}

#app-shell.route-loading::before {
  width: 70%;
  opacity: 1;
}

#app-shell.route-loading--done::before {
  width: 100%;
  opacity: 0;
}
</style>