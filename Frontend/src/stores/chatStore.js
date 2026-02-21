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

    async sendMessage() {
      if (!this.message.trim()) return

      // Cancel any in-flight stream
      this.cancelStream()
      this._abortController = new AbortController()

      const userMessage = this.message
      this.message = ''
      this.isLoading = true

      // Push user message immediately so it appears right away
      this.messageHistory.push({
        key: uuid(),
        sentAt: new Date(),
        sentBy: 'You',
        message: userMessage,
      })

      // Push a placeholder AI message — we'll fill this in as tokens arrive
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
        const result = await getStreamingResponse(
          userMessage,
          // Pass history excluding the empty placeholder we just added
          this.messageHistory.slice(0, -2),
          this.userTrackingId,
          (token) => {
            // First token arriving — switch from loading spinner to streaming state
            if (this.isLoading) {
              this.isLoading = false
              this.isStreaming = true
            }
            if (aiMessage) aiMessage.message += token
          },
          this._abortController.signal
        )

        if (aiMessage) aiMessage.isStreaming = false

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
          // User cancelled — mark message as complete with whatever came through
          if (aiMessage) aiMessage.isStreaming = false
          return
        }

        console.error(error)
        // Replace the empty placeholder with an error message
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