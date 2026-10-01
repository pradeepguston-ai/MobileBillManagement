const currencyFormatter = new Intl.NumberFormat('en-LK', { style: 'currency', currency: 'LKR', minimumFractionDigits: 2 })

export function formatCurrency(value: number | null | undefined) {
  return value == null ? 'Unavailable' : currencyFormatter.format(value)
}

export function formatDateTime(value?: string | null) {
  if (!value) return 'Not available'
  return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
}
