import axios from 'axios'

export async function getResponse(message, messageHistory, userTrackingId) {
  try {
    const request = {
      message,
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

/**
 * Streams a chat response token by token via SSE.
 * @param {string} message
 * @param {Array} messageHistory
 * @param {string} userTrackingId
 * @param {(token: string) => void} onToken - called for each token as it arrives
 * @param {AbortSignal} signal - for cancellation
 * @returns {Promise<{ redirectToPage?: string, displayResume?: boolean, tokenLimitReached?: boolean }>}
 */
export async function getStreamingResponse(message, messageHistory, userTrackingId, onToken, signal) {
  const request = {
    message,
    history: messageHistory
      .filter((msg) => msg.sentBy !== 'System')
      .map((msg) => ({
        role: msg.sentBy === 'You' ? 'user' : 'assistant',
        content: msg.message,
      })),
    userTrackingId: userTrackingId ?? null,
  }

  const response = await fetch(`${import.meta.env.VITE_API_URL}/Chat/stream`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
    signal,
  })

  if (!response.ok) {
    throw new Error(`Stream request failed: ${response.status}`)
  }

  const reader = response.body.getReader()
  const decoder = new TextDecoder()
  let buffer = ''
  // Side-effect metadata comes through as special SSE events
  let redirectToPage = null
  let displayResume = false
  let tokenLimitReached = false

  while (true) {
    const { done, value } = await reader.read()
    if (done) break

    buffer += decoder.decode(value, { stream: true })
    const lines = buffer.split('\n\n')
    buffer = lines.pop() ?? '' // hold incomplete chunk for next iteration

    for (const line of lines) {
      if (!line.startsWith('data: ')) continue
      const data = line.slice(6).trim()
      if (data === '[DONE]') break

      try {
        const parsed = JSON.parse(data)

        // Regular token
        if (parsed.token !== undefined) {
          onToken(parsed.token)
          continue
        }

        // Metadata event (sent before [DONE])
        if (parsed.redirectToPage) redirectToPage = parsed.redirectToPage
        if (parsed.displayResume)  displayResume  = parsed.displayResume
        if (parsed.tokenLimitReached) tokenLimitReached = parsed.tokenLimitReached
      } catch {
        // skip malformed chunk
      }
    }
  }

  return { redirectToPage, displayResume, tokenLimitReached }
}

export async function getResume() {
  try {
    const response = await axios.get(`${import.meta.env.VITE_API_URL}/chat/resume/true`)
    return response.data
  } catch (error) {
    console.error('Error fetching resume:', error)
    throw error
  }
}