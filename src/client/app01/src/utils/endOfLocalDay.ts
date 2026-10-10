// Zamienia datę yyyy-MM-dd na początek następnego dnia w lokalnej strefie
// czasowej, zwracany jako ISO string UTC (granica „stanu na koniec dnia”).
export function endOfLocalDay(date: string): string {
  const [year, month, day] = date.split("-").map(Number);
  return new Date(year, month - 1, day + 1).toISOString();
}
