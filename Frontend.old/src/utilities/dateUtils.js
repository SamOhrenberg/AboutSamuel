export function formatDateTime(date, options = {}) {
  if (!date) return ''

  const userLocale = navigator.language || 'en-US'

  const defaultOptions = {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  }

  return new Intl.DateTimeFormat(userLocale, { ...defaultOptions, ...options }).format(
    new Date(date),
  )
}
