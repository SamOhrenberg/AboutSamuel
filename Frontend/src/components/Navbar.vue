<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useTheme } from 'vuetify/lib/framework.mjs'

const theme = useTheme()
const activeTheme = ref(theme.global.name.value)
const drawer = ref(false)
const router = useRouter()

const changeTheme = () => {
  theme.global.name.value = theme.global.current.value.dark ? 'light' : 'dark'
}

const navLinks = [
  { label: 'Home', to: '/' },
  { label: 'Resume', to: '/resume' },
  { label: 'Experience', to: '/work-experience' },
  { label: 'Projects', to: '/projects' },
  { label: 'Contact', to: '/contact' },
]

router.afterEach(() => {
  drawer.value = false
})
</script>

<template>
  <!-- Mobile drawer -->
  <v-navigation-drawer
    v-model="drawer"
    temporary
    location="left"
    aria-label="Mobile navigation menu"
  >
    <v-list nav>
      <v-list-item class="py-4">
        <span class="drawer-title">Samuel Ohrenberg</span>
      </v-list-item>
      <v-divider class="mb-2" />
      <v-list-item
        v-for="link in navLinks"
        :key="link.to"
        :to="link.to"
        :title="link.label"
        active-color="secondary"
        rounded="lg"
        class="mb-1 drawer-link"
      />
      <v-divider class="my-3" />
      <v-list-item>
        <v-switch
          v-model="activeTheme"
          true-value="light"
          false-value="dark"
          @change="changeTheme"
          color="secondary"
          hide-details
          :label="`Turn Lights ${activeTheme === 'dark' ? 'On' : 'Off'}`"
          :aria-label="`Switch to ${activeTheme === 'dark' ? 'light' : 'dark'} mode`"
        />
      </v-list-item>
    </v-list>
  </v-navigation-drawer>

  <!-- Navbar -->
  <nav class="navbar" aria-label="Main navigation">
    <!-- Hamburger — mobile only -->
    <v-btn
      class="d-flex d-md-none"
      icon
      variant="text"
      @click="drawer = !drawer"
      :aria-expanded="String(drawer)"
      aria-label="Open navigation menu"
      style="min-width:44px;min-height:44px;"
    >
      <v-icon>mdi-menu</v-icon>
    </v-btn>

    <span class="site-title d-flex d-md-none">Samuel Ohrenberg</span>
    <span class="d-flex d-md-none" style="width:44px;" aria-hidden="true" />

    <!-- Theme toggle — desktop only -->
    <v-switch
      v-model="activeTheme"
      class="ma-0 pa-0 d-none d-md-flex"
      direction="vertical"
      true-value="light"
      false-value="dark"
      base-color="navbar_links"
      @change="changeTheme"
      color="navbar_links"
      hide-details
      :aria-label="`Switch to ${activeTheme === 'dark' ? 'light' : 'dark'} mode`"
    >
      <template v-slot:label>
        <span class="text-navbar_links">
          Turn Lights {{ activeTheme === 'dark' ? 'On' : 'Off' }}
        </span>
      </template>
    </v-switch>

    <!-- Desktop nav links -->
    <ul class="nav-links d-none d-md-flex" role="list">
      <li v-for="link in navLinks" :key="link.to">
        <router-link :to="link.to" class="nav-link">{{ link.label }}</router-link>
      </li>
    </ul>
  </nav>
</template>

<style scoped>
.navbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0 1.5rem;
  background-color: rgb(var(--v-theme-background));
  border-bottom: 1px solid rgba(255, 255, 255, 0.15);
  height: 64px;
  width: 100%;
  flex-shrink: 0;
}

.nav-links {
  list-style: none;
  display: flex;
  gap: 1.5rem;
  padding: 0;
  margin: 0;
}

/* Animated underline nav link */
.nav-link {
  position: relative;
  color: rgb(var(--v-theme-accent)) !important;
  text-decoration: none;
  font-size: 1rem;
  padding: 0.5rem 0.1rem;
  min-height: 44px;
  display: inline-flex;
  align-items: center;
  transition: color 0.15s ease;
}

.nav-link:hover {
  color: rgb(var(--v-theme-secondary)) !important;
  text-shadow: 0 0 12px rgba(var(--v-theme-secondary), 0.4);
}

/* Underline pseudo-element */
.nav-link::after {
  content: '';
  position: absolute;
  bottom: 4px;
  left: 0;
  width: 100%;
  height: 2px;
  background-color: rgb(var(--v-theme-secondary));
  transform: scaleX(0);
  transform-origin: left center;
  transition: transform 0.22s ease;
  border-radius: 2px;
}

.nav-link:hover::after,
.nav-link.router-link-active::after {
  transform: scaleX(1);
}

/* Active link slightly brighter */
.nav-link.router-link-active {
  color: rgb(var(--v-theme-secondary)) !important;
}

.nav-link:visited {
  color: rgb(var(--v-theme-accent)) !important;
}

.nav-link:focus-visible {
  outline: 2px solid rgb(var(--v-theme-accent));
  outline-offset: 3px;
  border-radius: 2px;
}

ul {
  display: flex;
  width: 100%;
  justify-content: flex-end;
}

.site-title {
  font-family: 'Patua One', serif;
  font-size: 1.1rem;
  color: rgb(var(--v-theme-accent));
  flex: 1;
  text-align: center;
}

.drawer-title {
  font-family: 'Patua One', serif;
  font-size: 1.2rem;
  color: rgb(var(--v-theme-secondary));
}

.drawer-link { min-height: 48px; }

:deep(.v-input__details) { display: none; }

:deep(.v-list-item--active) {
  border-left: 3px solid rgb(var(--v-theme-secondary));
  padding-left: calc(16px - 3px) !important;
}
</style>