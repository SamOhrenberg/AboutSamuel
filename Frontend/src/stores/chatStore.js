import { getStreamingResponse } from '@/services/chatService'
import { defineStore } from 'pinia'
import { v6 as uuid } from 'uuid'

export const useChatStore = defineStore('chat', {
  state: () => ({
    isOpen: true,
    isExpanded: false,
    isLoading: false,    // true while waiting for first token
    isStreaming: false,  // true while tokens are actively flowing
    width: 250,
    height: window.innerHeight * 0.3,
    _abortController: null,
    userTrackingId: (() => {
      let id = sessionStorage.getItem('chat_tracking_id')
      if (!id) {
        id = crypto.randomUUID()
        sessionStorage.setItem('chat_tracking_id', id)
      }
      return id
    })(),
    archivedMessageHistory: [
      {
        sentBy: 'SamuelLM',
        sentAt: new Date(),
        message: 'Hello! I\'m SamuelLM. Ask me anything about Samuel\'s background, skills, or experience.',
      },
    ],
    messageHistory: [],
    message: '',
  }),

  actions: {
    cancelStream() {
      if (this._abortController) {
        this._abortController.abort()
        this._abortController = null
      }
    },

    async simulateStreaming(messageKey, fullText) {
      const message = this.messageHistory.find(m => m.key === messageKey)
      if (!message) return

      message.isStreaming = true
      message.message = ''

      // Chunk by character, but batch every ~3 chars so it feels
      // like natural typing speed without being too granular
      const chunkSize = 3
      const delayMs = 18

      for (let i = 0; i < fullText.length; i += chunkSize) {
        if (!message.isStreaming) break // respect cancellation
        message.message += fullText.slice(i, i + chunkSize)
        await new Promise(resolve => setTimeout(resolve, delayMs))
      }

      message.isStreaming = false
    },

    async sendMessage() {
      if (!this.message.trim()) return

      this.cancelStream()
      this._abortController = new AbortController()

      const userMessage = this.message
      this.message = ''
      this.isLoading = true

      this.messageHistory.push({
        key: uuid(),
        sentAt: new Date(),
        sentBy: 'You',
        message: userMessage,
      })

      const aiMessageKey = uuid()
      this.messageHistory.push({
        key: aiMessageKey,
        sentAt: new Date(),
        sentBy: 'SamuelLM',
        message: '',
        isStreaming: true,
      })

      const aiMessage = this.messageHistory.find(m => m.key === aiMessageKey)

      try {
        let firstTokenReceived = false
        let shortCircuitText = null

        const result = await getStreamingResponse(
          userMessage,
          this.messageHistory.slice(0, -2),
          this.userTrackingId,
          (token) => {
            if (!firstTokenReceived) {
              firstTokenReceived = true
              this.isLoading = false
              this.isStreaming = true
            }
            if (aiMessage) aiMessage.message += token
          },
          this._abortController.signal,
          // Callback fired if the entire response arrived as one instant chunk
          (fullText) => { shortCircuitText = fullText }
        )

        // If backend sent everything at once (greeting, clarification etc.)
        // animate it client-side so it never just "pops" in
        if (shortCircuitText && aiMessage) {
          this.isLoading = false
          this.isStreaming = true
          await this.simulateStreaming(aiMessageKey, shortCircuitText)
        } else {
          if (aiMessage) aiMessage.isStreaming = false
        }

        if (result.tokenLimitReached) {
          this.archivedMessageHistory = [...this.archivedMessageHistory, ...this.messageHistory]
          this.messageHistory = []
        }

        if (result.redirectToPage) {
          const page = result.redirectToPage.trim()
          if (page.toLowerCase() === 'projects') {
            return `${page}?highlight=${encodeURIComponent(userMessage)}`
          }
          return page
        }

      } catch (error) {
        if (error.name === 'AbortError') {
          if (aiMessage) aiMessage.isStreaming = false
          return
        }
        console.error(error)
        if (aiMessage) {
          aiMessage.sentBy = 'System'
          aiMessage.message = 'An error occurred. Please try your message again.'
          aiMessage.isStreaming = false
        }
      } finally {
        this.isLoading = false
        this.isStreaming = false
        this._abortController = null
      }
    },
  },
})