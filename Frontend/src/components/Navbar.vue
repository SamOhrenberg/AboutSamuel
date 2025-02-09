<script setup>
import { ref, watch, nextTick } from 'vue'
import { useChatStore } from '@/stores/chatStore' // Import Pinia store
import { useTheme } from 'vuetify/lib/framework.mjs'

const store = useChatStore()
const chatContainer = ref(null)

const theme = useTheme()
const activeTheme = ref(theme.global.name.value)

// Watch for new messages and scroll to bottom
watch(
  () => store.messageHistory.length,
  async () => {
    await nextTick() // Wait for DOM update
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
  <nav class="navbar">
    <v-switch
        v-model="activeTheme"
        :label="`Turn Lights ${activeTheme == 'dark' ? 'On' : 'Off'}`"
        class="ma-0 pa-0"
        direction="vertical"
        true-value="light"
        false-value="dark"
        base-color="white"
        @change="changeTheme"
        color="primary"
      ></v-switch>
    <ul class="nav-links">
      <li><a>Resume</a></li>
      <li>Testimonial</li>
      <li>Contact</li>
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

li {
  color: var(--v-theme-accent) !important;

  text-decoration: none;
}

.nav-links li a:hover {
  text-decoration: underline;
}

ul{
  display: flex;
  width: 100%;
  justify-content: flex-end;

}
.v-switch div.v-input__details {
  display: none;
}
</style>
