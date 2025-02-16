import './assets/main.css'

import { createApp } from 'vue'
import { createPinia } from 'pinia'

import App from './App.vue'
import router from './router'
import { createVuetify } from 'vuetify'
import * as components from 'vuetify/components' // Import all Vuetify components
import * as directives from 'vuetify/directives' // Import Vuetify directives
import 'vuetify/styles' // Import Vuetify styles
import '@mdi/font/css/materialdesignicons.css'
import { useConfigStore } from './stores/configStore'

import { dark } from './themes/dark'
import { light } from './themes/light'

fetch('/config.json')
  .then((response) => response.json())
  .then((config) => {
    const app = createApp(App)

    app.use(createPinia())
    app.use(router)
    app.use(
      createVuetify({
        components,
        directives,
        theme: {
          defaultTheme: 'dark',
          themes: {
            dark,
            light,
          },
        },
      }),
    )

    const configStore = useConfigStore()
    configStore.setConfig(config)

    app.mount('#app')
  })
