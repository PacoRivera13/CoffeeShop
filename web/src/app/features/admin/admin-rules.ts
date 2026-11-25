import { MovementResponse } from '../../api/models/movement-response';
import { PriceHistoryResponse } from '../../api/models/price-history-response';

/** Products below this many grams are flagged "Low stock". */
export const LOW_STOCK_THRESHOLD_G = 2000;
/** The API's limits for one restock. */
export const MIN_RESTOCK_GRAMS = 1;
export const MAX_RESTOCK_GRAMS = 1_000_000;

export function isLowStock(stockG: number): boolean {
  return stockG < LOW_STOCK_THRESHOLD_G;
}

/** "20 kg", "1.25 kg": kilograms with up to three decimals (exact to the gram). */
export function formatKg(grams: number, locale = 'en-US'): string {
  return `${new Intl.NumberFormat(locale, { maximumFractionDigits: 3 }).format(grams / 1000)} kg`;
}

export function formatGrams(grams: number, locale = 'en-US'): string {
  return `${new Intl.NumberFormat(locale).format(grams)} g`;
}

/** Newest first: the API sends the history in no guaranteed order. */
export function sortByEffectiveFromDesc(history: readonly PriceHistoryResponse[]): PriceHistoryResponse[] {
  return [...history].sort((a, b) => Date.parse(b.effectiveFrom) - Date.parse(a.effectiveFrom));
}

/** Today's date in UTC as yyyy-MM-dd: the API rejects prices effective before today (UTC). */
export function todayUtc(now: Date = new Date()): string {
  return now.toISOString().slice(0, 10);
}

export interface MovementView {
  label: string;
  icon: string;
  /** Signed grams, e.g. "+5,000 g" or "−500 g". */
  delta: string;
}

const REASONS: Record<string, { label: string; icon: string }> = {
  Restock: { label: 'Restock', icon: 'add_box' },
  Reserve: { label: 'Reserved for order', icon: 'shopping_bag' },
  Release: { label: 'Released from order', icon: 'undo' },
};

export function movementView(movement: MovementResponse): MovementView {
  const reason = REASONS[movement.reason] ?? { label: movement.reason, icon: 'swap_vert' };
  const grams = Number(movement.deltaG);
  const sign = grams > 0 ? '+' : grams < 0 ? '−' : '';
  return { ...reason, delta: `${sign}${formatGrams(Math.abs(grams))}` };
}
