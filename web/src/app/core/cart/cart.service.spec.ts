import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthService } from '../auth/auth.service';
import { CART_STORAGE_KEY, CartItem, CartService, MAX_QTY } from './cart.service';

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
const OTHER: CartItem = { ...ITEM, skuId: 21, productId: 2, name: 'Colombia Huila', unitPrice: 160.5 };

describe('CartService', () => {
  const authenticated = signal(true);

  function create(): CartService {
    TestBed.configureTestingModule({
      providers: [{ provide: AuthService, useValue: { isAuthenticated: authenticated } }],
    });
    const cart = TestBed.inject(CartService);
    TestBed.tick();
    return cart;
  }

  beforeEach(() => authenticated.set(true));
  afterEach(() => sessionStorage.removeItem(CART_STORAGE_KEY));

  it('adds items, merging the same SKU, and estimates the total', () => {
    const cart = create();

    cart.add(ITEM);
    cart.add(ITEM, 2);
    cart.add(OTHER);

    expect(cart.lines().map((l) => [l.skuId, l.qty])).toEqual([
      [11, 3],
      [21, 1],
    ]);
    expect(cart.count()).toBe(4);
    expect(cart.estimatedTotal()).toBe(1060.5);
    expect(cart.currency()).toBe('MXN');
  });

  it('keeps quantities between 1 and 50', () => {
    const cart = create();
    cart.add(ITEM, MAX_QTY + 10);
    expect(cart.lines()[0].qty).toBe(MAX_QTY);
    expect(cart.add(ITEM)).toBeNull();

    cart.setQty(11, 0);
    expect(cart.lines()[0].qty).toBe(1);

    cart.setQty(11, 999);
    expect(cart.lines()[0].qty).toBe(MAX_QTY);
  });

  it('undo takes back exactly the units that were added', () => {
    const cart = create();
    cart.add(ITEM, 2);

    const undo = cart.add(ITEM, 3);
    undo!();
    expect(cart.lines()[0].qty).toBe(2);

    const undoNew = cart.add(OTHER);
    undoNew!();
    expect(cart.lines().map((l) => l.skuId)).toEqual([11]);
  });

  it('persists to sessionStorage and restores on the next start', () => {
    create().add(ITEM, 2);
    expect(JSON.parse(sessionStorage.getItem(CART_STORAGE_KEY)!)).toHaveLength(1);

    TestBed.resetTestingModule();
    const restored = create();

    expect(restored.count()).toBe(2);
  });

  it('ignores corrupt stored data', () => {
    sessionStorage.setItem(CART_STORAGE_KEY, '{not json');
    expect(create().lines()).toEqual([]);

    TestBed.resetTestingModule();
    const { roast: _roast, ...withoutRoast } = ITEM;
    sessionStorage.setItem(
      CART_STORAGE_KEY,
      JSON.stringify([{ skuId: 'x' }, { ...ITEM, qty: 0 }, { ...withoutRoast, qty: 1 }]),
    );
    expect(create().lines()).toEqual([]);
  });

  it('removes lines and counts every change as a new version', () => {
    const cart = create();
    cart.add(ITEM);
    const version = cart.version();

    cart.remove(11);

    expect(cart.lines()).toEqual([]);
    expect(cart.version()).toBeGreaterThan(version);
    expect(sessionStorage.getItem(CART_STORAGE_KEY)).toBeNull();
  });

  it('empties the cart when the session ends', () => {
    const cart = create();
    cart.add(ITEM);

    authenticated.set(false);
    TestBed.tick();

    expect(cart.lines()).toEqual([]);
  });
});
