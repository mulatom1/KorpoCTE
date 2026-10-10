// Formatuje datę w lokalnej strefie czasowej jako yyyy-MM-dd HH:mm:ss.
// Przyjmuje Date albo ISO string (np. UTC z sufiksem Z z API).
export function formatDateTime(value: Date | string): string {
  const date = typeof value === "string" ? new Date(value) : value;
  const pad = (n: number) => String(n).padStart(2, "0");

  return (
    `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ` +
    `${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`
  );
}
