import axios from 'axios'
import { useConfigStore } from '../stores/configStore'

export async function getResponse(message, messageHistory) {
  const configStore = useConfigStore()

  try {
    var request = {
      message: message,
      history: messageHistory
        .filter((msg) => msg.sentBy !== 'System')
        .map((msg) => ({
          role: msg.sentBy === 'You' ? 'user' : 'assistant',
          content: msg.message,
        })),
    }
    const response = await axios.post(`${configStore.apiUrl}/Chat`, request)

    return {
      text: response.data,
      tokenLimitReached: response.headers['x-token-limit-reached'],
    }
  } catch (error) {
    console.error('Error fetching response:', error)
    throw error
  }
}
