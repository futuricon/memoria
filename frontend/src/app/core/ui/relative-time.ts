/**
 * Human-friendly relative time. "in 2 h", "tomorrow", "in 4 d", "overdue".
 * Designed for short upcoming-list cells, not localized formatting.
 */
export function relativeTime(isoUtc: string, now: Date = new Date()): string {
  const t = new Date(isoUtc).getTime();
  const diffMs = t - now.getTime();

  if (diffMs < 0) return 'overdue';

  const minutes = Math.round(diffMs / 60_000);
  if (minutes < 60) return `in ${minutes} min`;

  const hours = Math.round(minutes / 60);
  if (hours < 24) return `in ${hours} h`;

  const days = Math.round(hours / 24);
  if (days === 1) return 'tomorrow';
  return `in ${days} d`;
}
