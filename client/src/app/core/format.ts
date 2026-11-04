const dateFormatter = new Intl.DateTimeFormat('ru-RU', {
  day: 'numeric',
  month: 'long',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
});

const dateOnlyFormatter = new Intl.DateTimeFormat('ru-RU', {
  day: 'numeric',
  month: 'long',
  year: 'numeric',
});

const moneyFormatter = new Intl.NumberFormat('ru-RU');

export function formatDateTime(iso: string): string {
  return dateFormatter.format(new Date(iso));
}

export function formatDate(iso: string): string {
  return dateOnlyFormatter.format(new Date(iso));
}

export function formatPrice(value: number): string {
  return value === 0 ? 'Бесплатно' : `${moneyFormatter.format(value)} сум`;
}

/** Локальное значение для <input type="datetime-local">. */
export function toDatetimeLocal(iso: string): string {
  const date = new Date(iso);
  const pad = (value: number) => value.toString().padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

/** Значение datetime-local (локальное) → ISO UTC для сервера. */
export function fromDatetimeLocal(value: string): string {
  return new Date(value).toISOString();
}

export function minutesUntil(iso: string): number {
  return Math.round((new Date(iso).getTime() - Date.now()) / 60000);
}
