import axios from 'axios'

export async function getResponse(message, messageHistory, userTrackingId) {
  try {
    var request = {
      message: message,
      history: messageHistory
        .filter((msg) => msg.sentBy !== 'System')
        .map((msg) => ({
          role: msg.sentBy === 'You' ? 'user' : 'assistant',
          content: msg.message,
        })),
      userTrackingId: userTrackingId ?? null,
    }
    const response = await axios.post(`${import.meta.env.VITE_API_URL}/Chat`, request)

    return {
      text: response.data.message,
      tokenLimitReached: response.headers['x-token-limit-reached'],
      redirectToPage: response.data.redirectToPage,
      displayResume: response.data.displayResume,
    }
  } catch (error) {
    console.error('Error fetching response:', error)
    throw error
  }
}

export async function getResume() {
  try {
    const response = await axios.get(`${import.meta.env.VITE_API_URL}/chat/resume/true`)
    console.log(response);
    console.log(response.data);
    return response.data
  } catch (error) {
    console.error('Error fetching resume:', error)
    throw error
  }
}
