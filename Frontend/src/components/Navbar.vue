<script setup>
import { ref, watch, nextTick } from 'vue'
import { useChatStore } from '@/stores/chatStore'
import { useTheme } from 'vuetify/lib/framework.mjs'

const store = useChatStore()
const chatContainer = ref(null)

const theme = useTheme()
const activeTheme = ref(theme.global.name.value)

watch(
  () => store.messageHistory.length,
  async () => {
    await nextTick()
    if (chatContainer.value) {
      chatContainer.value.scrollTop = chatContainer.value.scrollHeight
    }
  }
)

const changeTheme = () => {
  theme.global.name.value = theme.global.current.value.dark ? 'light' : 'dark'
}
</script>

<template>
  <nav class="navbar" aria-label="Main navigation">
    <v-switch
      v-model="activeTheme"
      class="ma-0 pa-0"
      direction="vertical"
      true-value="light"
      false-value="dark"
      base-color="navbar_links"
      @change="changeTheme"
      color="navbar_links"
      :aria-label="`Switch to ${activeTheme === 'dark' ? 'light' : 'dark'} mode`"
    >
      <template v-slot:label>
        <span class="text-navbar_links">
          Turn Lights {{ activeTheme == 'dark' ? 'On' : 'Off' }}
        </span>
      </template>
    </v-switch>
    <ul class="nav-links text-navbar_links" role="list">
      <li><router-link to="/">Home</router-link></li>
      <li><router-link to="/resume">Resume</router-link></li>
      <li><router-link to="/projects">Projects</router-link></li>
      <li><router-link to="/contact">Contact</router-link></li>
      <!-- Testimonial page hidden until content is ready -->
    </ul>
  </nav>
</template>

<style scoped>
.navbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 2rem 2rem;
  background-color: var(--v-theme-background);
  border-bottom: 1px white solid;
}

.nav-links {
  list-style: none;
  display: flex;
  gap: 1.5rem;
  padding: 0;
  margin: 0;
}

li a {
  color: var(--v-theme-accent) !important;
  text-decoration: none;
}

.nav-links li a:hover {
  text-decoration: underline;
}

li a:visited {
  color: var(--v-theme-accent) !important;
}

/* Visible focus ring for keyboard navigation */
li a:focus-visible {
  outline: 2px solid var(--v-theme-accent);
  outline-offset: 3px;
  border-radius: 2px;
}

ul {
  display: flex;
  width: 100%;
  justify-content: flex-end;
}
.v-switch div.v-input__details {
  display: none;
}
</style>