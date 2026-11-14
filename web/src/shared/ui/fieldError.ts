/** First server-side field error for a field (API uses camelCase keys). */
export function fieldError(errors: Record<string, string[]> | undefined, field: string) {
  return errors?.[field]?.[0]
}
