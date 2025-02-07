import axios from 'axios'

export async function getResponse(message, messageHistory) {
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
    const response = await axios.post('https://localhost:7276/Chat', request)

    return {
      text: response.data,
      tokenLimitReached: response.headers['x-token-limit-reached'],
    }
  } catch (error) {
    console.error('Error fetching response:', error)
    throw error
  }
}
