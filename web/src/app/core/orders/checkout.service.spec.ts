import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { order } from '../../../../testing/fixtures';
import { AuthService } from '../auth/auth.service';
import { CART_STORAGE_KEY, CartItem, CartService } from '../cart/cart.service';
import { CheckoutService } from './checkout.service';

const ITEM: CartItem = {
  skuId: 11,
  productId: 1,
  name: 'Ethiopia Yirgacheffe',
  roast: 'Light',
  grind: 'WholeBean',
  sizeG: 500,
  unitPrice: 300,
  currency: 'MXN',
};

describe('CheckoutService', () => {
  let cart: CartService;
  let checkout: CheckoutService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { isAuthenticated: signal(true) } },
      ],
    });
    cart = TestBed.inject(CartService);
    checkout = TestBed.inject(CheckoutService);
    http = TestBed.inject(HttpTestingController);
    cart.add(ITEM, 2);
  });

  afterEach(() => {
    http.verify();
    sessionStorage.removeItem(CART_STORAGE_KEY);
  });

  it('sends the cart with an Idempotency-Key and clears the cart on success', async () => {
    const placed = firstValueFrom(checkout.placeOrder());

    const request = http.expectOne((r) => r.method === 'POST' && r.url.endsWith('/orders'));
    expect(request.request.headers.get('Idempotency-Key')).toMatch(/^[0-9a-f-]{36}$/);
    expect(request.request.body).toEqual({ items: [{ skuId: 11, qty: 2 }] });
    request.flush(order({ id: 9 }), { status: 201, statusText: 'Created' });

    expect((await placed).id).toBe(9);
    expect(cart.lines()).toEqual([]);
  });

  it('reuses the key when the same cart is retried after a failure', async () => {
    const first = firstValueFrom(checkout.placeOrder()).catch(() => 'failed');
    const firstRequest = http.expectOne((r) => r.url.endsWith('/orders'));
    const key = firstRequest.request.headers.get('Idempotency-Key');
    firstRequest.flush(null, { status: 503, statusText: 'Unavailable' });
    expect(await first).toBe('failed');

    void firstValueFrom(checkout.placeOrder()).catch(() => undefined);
    const retry = http.expectOne((r) => r.url.endsWith('/orders'));

    expect(retry.request.headers.get('Idempotency-Key')).toBe(key);
    retry.flush(order(), { status: 200, statusText: 'OK' });
  });

  it('uses a new key once the cart changes', () => {
    const key = checkout.idempotencyKey();
    expect(checkout.idempotencyKey()).toBe(key);

    cart.setQty(11, 3);

    expect(checkout.idempotencyKey()).not.toBe(key);
  });
});
