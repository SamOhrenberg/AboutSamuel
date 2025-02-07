import { getResponse } from '@/services/chatService'
import { defineStore } from 'pinia'
import { v6 as uuid } from 'uuid'

export const useChatStore = defineStore('chat', {
  state: () => ({
    isOpen: window.innerWidth > 1080,
    isExpanded: false,
    isLoading: false,
    width: 250, // Default min width
    height: window.innerHeight * 0.3, // 30% of viewport height
    archivedMessageHistory: [],
    messageHistory: [
      {
        sentBy: 'System',
        sentAt: new Date(),
        message:
          'Hello, there! My name is SamuelLM. I am an AI assistant developed by Samuel to help answer any questions you might have about him. Please feel free to ask me anything.',
      },
    ],
    message: '',
  }),
  actions: {
    async sendMessage() {
      if (this.message.trim()) {
        console.log('Sending message:', this.message)
        this.isLoading = true
        try {
          var response = await getResponse(this.message, this.messageHistory)

          if (response.tokenLimitReached) {
            console.warn('token limit reached')
            this.archivedMessageHistory = [...this.archivedMessageHistory, ...this.messageHistory]
            this.messageHistory = []
          }

          this.messageHistory.push({
            key: uuid(),
            sentAt: new Date(),
            sentBy: 'You',
            message: this.message,
          })
          this.messageHistory.push({
            key: uuid(),
            sentAt: new Date(),
            sentBy: 'SamuelLM',
            message: response.text,
          })

          this.message = ''
        } catch {
          this.messageHistory.pop()
          this.messageHistory.push({
            key: uuid(),
            sentAt: new Date(),
            sentBy: 'System',
            message: 'An error occurred. Please try your message again.',
          })
        } finally {
          this.isLoading = false
        }
      }
    },
  },
})
