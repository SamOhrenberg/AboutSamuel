import constants from '@/utilities/constants'

export async function sendContactRequest(email, message) {
  try {
    const response = await fetch(`${import.meta.env.VITE_API_URL}/contact`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ email, message }),
    })

    if (!response.ok) {
      const errorData = await response.json()
      console.error(errorData)
      throw new Error(constants.unknownSystemError)
    }
  } catch (error) {
    throw error
  }
}
