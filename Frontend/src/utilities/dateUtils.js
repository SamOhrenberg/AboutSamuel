export function formatDateTime(date, options = {}) {
  if (!date) return ''

  const userLocale = navigator.language || 'en-US'

  const defaultOptions = {
    hour: '2-digit',
    minute: '2-digit',
  }

  return new Intl.DateTimeFormat(userLocale, { ...defaultOptions, ...options }).format(
    new Date(date)
  )
}
