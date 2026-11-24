import { computed, effect, inject, Injectable, signal, untracked } from '@angular/core';
import { AuthService } from '../auth/auth.service';

export const CART_STORAGE_KEY = 'roastery.cart';
/** The API accepts 1 to 50 units per SKU, the same bounds as QuantityStepper. */
export const MIN_QTY = 1;
export const MAX_QTY = 50;

export interface CartItem {
  skuId: number;
  productId: number;
  name: string;
  roast: string;
  grind: string;
  sizeG: number;
  /** Price when the item was added; only an estimate, the server prices the order. */
  unitPrice: number;
  currency: string;
}

export interface CartLine extends CartItem {
  qty: number;
}

/** The cart of this browser tab, kept in sessionStorage and emptied when the session ends. */
@Injectable({ providedIn: 'root' })
export class CartService {
  private readonly state = signal<readonly CartLine[]>(readStoredCart());
  private readonly changes = signal(0);

  readonly lines = this.state.asReadonly();
  /** Increments on every change to the contents; a checkout attempt belongs to one version. */
  readonly version = this.changes.asReadonly();
  readonly count = computed(() => this.state().reduce((sum, line) => sum + line.qty, 0));
  readonly currency = computed(() => this.state()[0]?.currency ?? null);
  readonly estimatedTotal = computed(() =>
    roundMoney(this.state().reduce((sum, line) => sum + line.unitPrice * line.qty, 0)),
  );

  constructor() {
    const auth = inject(AuthService);
    effect(() => {
      if (!auth.isAuthenticated()) untracked(() => this.clear());
    });
  }

  /**
   * Adds qty units of an item and returns a function that takes exactly those units back out, or null when the
   * line is already at MAX_QTY and nothing was added.
   */
  add(item: CartItem, qty = 1): (() => void) | null {
    const existing = this.state().find((line) => line.skuId === item.skuId);
    const before = existing?.qty ?? 0;
    const after = clampQty(before + qty);
    const added = after - before;
    if (added === 0) return null;
    if (existing) {
      this.write(this.state().map((line) => (line.skuId === item.skuId ? { ...item, qty: after } : line)));
    } else {
      this.write([...this.state(), { ...item, qty: after }]);
    }
    return () => {
      const line = this.state().find((l) => l.skuId === item.skuId);
      if (!line) return;
      const remaining = line.qty - added;
      if (remaining < MIN_QTY) this.remove(item.skuId);
      else this.setQty(item.skuId, remaining);
    };
  }

  setQty(skuId: number, qty: number): void {
    const next = clampQty(qty);
    if (!this.state().some((line) => line.skuId === skuId && line.qty !== next)) return;
    this.write(this.state().map((line) => (line.skuId === skuId ? { ...line, qty: next } : line)));
  }

  remove(skuId: number): void {
    if (!this.state().some((line) => line.skuId === skuId)) return;
    this.write(this.state().filter((line) => line.skuId !== skuId));
  }

  clear(): void {
    if (this.state().length === 0) {
      removeStoredCart();
      return;
    }
    this.write([]);
  }

  private write(lines: readonly CartLine[]): void {
    this.state.set(lines);
    this.changes.update((v) => v + 1);
    try {
      if (lines.length === 0) sessionStorage.removeItem(CART_STORAGE_KEY);
      else sessionStorage.setItem(CART_STORAGE_KEY, JSON.stringify(lines));
    } catch {
      // Without storage the cart lives as long as the page.
    }
  }
}

export function clampQty(qty: number): number {
  return Math.min(MAX_QTY, Math.max(MIN_QTY, Math.trunc(qty)));
}

function roundMoney(amount: number): number {
  return Math.round(amount * 100) / 100;
}

function readStoredCart(): CartLine[] {
  try {
    const parsed: unknown = JSON.parse(sessionStorage.getItem(CART_STORAGE_KEY) ?? '[]');
    return Array.isArray(parsed) ? parsed.filter(isCartLine) : [];
  } catch {
    return [];
  }
}

function removeStoredCart(): void {
  try {
    sessionStorage.removeItem(CART_STORAGE_KEY);
  } catch {
    // Nothing stored.
  }
}

function isCartLine(value: unknown): value is CartLine {
  if (typeof value !== 'object' || value === null) return false;
  const line = value as Record<string, unknown>;
  return (
    typeof line['skuId'] === 'number' &&
    typeof line['productId'] === 'number' &&
    typeof line['name'] === 'string' &&
    typeof line['roast'] === 'string' &&
    typeof line['grind'] === 'string' &&
    typeof line['sizeG'] === 'number' &&
    typeof line['unitPrice'] === 'number' &&
    typeof line['currency'] === 'string' &&
    typeof line['qty'] === 'number' &&
    line['qty'] >= MIN_QTY &&
    line['qty'] <= MAX_QTY
  );
}
