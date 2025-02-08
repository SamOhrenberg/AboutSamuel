import { defineStore } from 'pinia'

export const useConfigStore = defineStore('config', {
  state: () => ({
    apiUrl: '',
  }),
  actions: {
    setConfig(config) {
      this.apiUrl =
        config.apiUrl.endsWith('/') || config.apiUrl.endsWith('\\')
          ? config.apiUrl.slice(0, -1)
          : config.apiUrl
    },
  },
})
