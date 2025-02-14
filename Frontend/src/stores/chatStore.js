import { getResponse } from '@/services/chatService'
import { defineStore } from 'pinia'
import { v6 as uuid } from 'uuid'

export const useChatStore = defineStore('chat', {
  state: () => ({
    isOpen: true,
    isExpanded: false,
    isLoading: false,
    width: 250, // Default min width
    height: window.innerHeight * 0.3, // 30% of viewport height
    archivedMessageHistory: [
      {
        sentBy: 'SamuelLM',
        sentAt: new Date(),
        message:
          'Hello, there! My name is SamuelLM. I am an AI assistant developed by Samuel to help answer any questions you might have about him. Please feel free to ask me anything.',
      },
    ],
    messageHistory: [
      // {
      //   sentBy: 'You',
      //   sentAt: new Date(),
      //   message: 'What do you know about API?',
      // },
      // {
      //   sentBy: 'SamuelLM',
      //   sentAt: new Date(),
      //   message:
      //     "I've led the development of ASP.NET Core Web APIs, like our Experience API. I understand API architecture, HTTP requests, data aggregation, integration with ERP systems using SDKs, CI/CD pipelines with GitHub Actions, and securing APIs with JWT for authentication and authorization.",
      // },
      // {
      //   sentBy: 'You',
      //   sentAt: new Date(),
      //   message: 'I want to know more about your ASP.NET Core Web API projects.',
      // },
      // {
      //   sentBy: 'SamuelLM',
      //   sentAt: new Date(),
      //   message:
      //     "I've developed multiple ASP.NET Core Web APIs, including our Experience API at OKCCC. This API integrates with Ellucian Ethos ERP system using SDKs, aggregating data for various services like student information, course catalog, and faculty directories. It follows RESTful architecture, utilizes HTTPS for secure communication, and implements JWT for authentication and authorization. The API is built on .NET 5.0, uses Entity Framework Core for database operations, and employs unit tests with xUnit and integration tests with Postman. Additionally, I've set up CI/CD pipelines using GitHub Actions for automated testing and deployment.",
      // },
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
          console.log(response)
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

          if (response.redirectToPage && response.redirectToPage != '') {
            return response.redirectToPage.replace(' ', '')
          }
        } catch (error) {
          console.error(error)
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
