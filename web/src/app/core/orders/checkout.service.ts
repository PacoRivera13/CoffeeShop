import { inject, Injectable } from '@angular/core';
import { defer, Observable, tap } from 'rxjs';
import { createOrder } from '../../api/fn/orders/create-order';
import { OrderResponse } from '../../api/models/order-response';
import { CartService } from '../cart/cart.service';
import { ApiRx } from '../http/api-rx.service';

/**
 * Places the cart as an order. One Idempotency-Key per checkout attempt: a retry of the same cart reuses it, so a
 * request that reached the server but lost its response returns the original order instead of a second one. A
 * different cart is a different attempt and gets a new key.
 */
@Injectable({ providedIn: 'root' })
export class CheckoutService {
  private readonly api = inject(ApiRx);
  private readonly cart = inject(CartService);

  private key: string | null = null;
  private keyVersion = -1;

  /** The key the next placeOrder call sends. */
  idempotencyKey(): string {
    if (this.key === null || this.keyVersion !== this.cart.version()) {
      this.key = newKey();
      this.keyVersion = this.cart.version();
    }
    return this.key;
  }

  placeOrder(): Observable<OrderResponse> {
    return defer(() =>
      this.api.body(createOrder, {
        'Idempotency-Key': this.idempotencyKey(),
        body: { items: this.cart.lines().map((line) => ({ skuId: line.skuId, qty: line.qty })) },
      }),
    ).pipe(
      tap(() => {
        this.key = null;
        this.cart.clear();
      }),
    );
  }
}

function newKey(): string {
  if (typeof crypto.randomUUID === 'function') return crypto.randomUUID();
  // randomUUID needs a secure context; getRandomValues does not.
  const bytes = crypto.getRandomValues(new Uint8Array(16));
  bytes[6] = (bytes[6] & 0x0f) | 0x40;
  bytes[8] = (bytes[8] & 0x3f) | 0x80;
  const hex = [...bytes].map((b) => b.toString(16).padStart(2, '0')).join('');
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}
