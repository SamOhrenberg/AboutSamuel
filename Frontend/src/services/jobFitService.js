export const MIN_JOB_DESCRIPTION_CHARS = 20
export const MAX_JOB_DESCRIPTION_CHARS = 12000

/**
 * Streams a Job Fit run. The API sends one JSON event per SSE message:
 *   { step }, { jobTitle, company, requirements }, { evidenceCount },
 *   { assessment }, { token }, { error }, and always { done } last.
 *
 * @param {string} jobDescription
 * @param {string|null} userTrackingId
 * @param {(event: object) => void} onEvent - called for every event as it arrives
 * @param {AbortSignal} signal
 */
export async function streamJobFit(jobDescription, userTrackingId, onEvent, signal) {
  const response = await fetch(`${import.meta.env.VITE_API_URL}/JobFit/stream`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ jobDescription, userTrackingId: userTrackingId ?? null }),
    signal,
  })

  if (response.status === 429) {
    throw new JobFitError("You've run a few of these already this hour. Give it a little while and try again.")
  }
  if (!response.ok) {
    let message = 'Something went wrong starting the analysis. Please try again.'
    try {
      message = (await response.json()).message ?? message
    } catch { /* not JSON, keep the default */ }
    throw new JobFitError(message)
  }

  const reader = response.body.getReader()
  const decoder = new TextDecoder()
  let buffer = ''

  while (true) {
    const { done, value } = await reader.read()
    if (done) break

    buffer += decoder.decode(value, { stream: true })
    const messages = buffer.split('\n\n')
    buffer = messages.pop() ?? ''

    for (const message of messages) {
      if (!message.startsWith('data: ')) continue
      try {
        onEvent(JSON.parse(message.slice(6)))
      } catch { /* skip malformed */ }
    }
  }
}

/** An error with a message that's safe to show the visitor as-is. */
export class JobFitError extends Error {}
