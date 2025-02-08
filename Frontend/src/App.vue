<template>
  <v-app>
    <v-switch
      v-model="activeTheme"
      :label="`Lights are ${activeTheme == 'dark' ? 'off' : 'on'}`"
      class="ma-0 pa-0"
      direction="vertical"
      true-value="light"
      false-value="dark"
      base-color="background"
      color="primary"
      @change="changeTheme"
    />

    <router-view />
  </v-app>
</template>

<script setup>
import { ref } from 'vue'

import { useTheme } from 'vuetify/lib/framework.mjs'

const theme = useTheme()
const activeTheme = ref(theme.global.name.value)

const changeTheme = () => {
  theme.global.name.value = theme.global.current.value.dark ? 'light' : 'dark'
}
</script>
