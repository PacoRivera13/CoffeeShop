// Typed API payloads for tests, built from the models generated from openapi.json, so a contract change breaks
// the tests at compile time instead of letting them mock a response the API no longer sends.
import type { HttpValidationProblemDetails } from '../../src/app/api/models/http-validation-problem-details';
import type { InventoryResponse } from '../../src/app/api/models/inventory-response';
import type { InvoiceResponse } from '../../src/app/api/models/invoice-response';
import type { LoginResponse } from '../../src/app/api/models/login-response';
import type { MovementPageResponse } from '../../src/app/api/models/movement-page-response';
import type { MovementResponse } from '../../src/app/api/models/movement-response';
import type { OrderPageResponse } from '../../src/app/api/models/order-page-response';
import type { OrderResponse } from '../../src/app/api/models/order-response';
import type { PriceCreatedResponse } from '../../src/app/api/models/price-created-response';
import type { PriceHistoryResponse } from '../../src/app/api/models/price-history-response';
import type { PriceResponse } from '../../src/app/api/models/price-response';
import type { ProblemDetails } from '../../src/app/api/models/problem-details';
import type { ProductResponse } from '../../src/app/api/models/product-response';
import type { RegisterResponse } from '../../src/app/api/models/register-response';
import type { RestockResponse } from '../../src/app/api/models/restock-response';

export type Role = 'Customer' | 'Admin';

export const customer = { email: 'ana@example.test', password: 'Str0ng!Passw0rd', name: 'Ana López' } as const;
export const admin = { email: 'admin@example.test', password: 'Adm1n!Passw0rd', name: 'Admin' } as const;

/** An unsigned JWT with the claims the UI reads; the API is mocked, so no signature is checked. */
export function fakeAccessToken(role: Role, email: string = role === 'Admin' ? admin.email : customer.email): string {
  // btoa works in Node and in the browser, so component specs can import these fixtures too.
  const encode = (value: object) => btoa(JSON.stringify(value)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  const exp = Math.floor(Date.now() / 1000) + 3600;
  return `${encode({ alg: 'HS256', typ: 'JWT' })}.${encode({ sub: `user-${role}`, email, role, exp })}.signature`;
}

export function loginResponse(role: Role, email?: string): LoginResponse {
  return { accessToken: fakeAccessToken(role, email), tokenType: 'Bearer', expiresIn: 3600 };
}

export const registerResponse: RegisterResponse = { customerId: 42, email: customer.email };

export const invalidCredentials: ProblemDetails = { status: 401, title: 'Invalid credentials' };

export const duplicateEmail: ProblemDetails = { status: 409, title: 'Email already registered' };

export const weakPassword: HttpValidationProblemDetails = {
  status: 400,
  title: 'One or more validation errors occurred.',
  errors: { password: ["Passwords must have at least one non alphanumeric character."] },
};

// Catalogue: two products, the first with three SKUs so grind and size both matter.
export const products: ProductResponse[] = [
  {
    id: 1,
    name: 'Ethiopia Yirgacheffe',
    origin: 'Ethiopia',
    roast: 'Light',
    skus: [
      { id: 11, grind: 'WholeBean', sizeG: 500 },
      { id: 12, grind: 'WholeBean', sizeG: 1000 },
      { id: 13, grind: 'Ground', sizeG: 500 },
    ],
  },
  {
    id: 2,
    name: 'Colombia Huila',
    origin: 'Colombia',
    roast: 'Medium',
    skus: [{ id: 21, grind: 'Ground', sizeG: 500 }],
  },
];

export function price(skuId: number, amount: number, asOf = '2026-11-24T12:00:00Z'): PriceResponse {
  return { skuId, priceId: skuId * 100, amount, currency: 'MXN', effectiveFrom: '2026-01-01T00:00:00Z', asOf };
}

export function order(overrides: Partial<OrderResponse> = {}): OrderResponse {
  return {
    id: 7,
    status: 'Pending',
    currency: 'MXN',
    createdAt: '2026-11-24T10:30:00Z',
    total: 760,
    lines: [
      { skuId: 11, qty: 2, unitPrice: 300, priceId: 1100, lineTotal: 600 },
      { skuId: 21, qty: 1, unitPrice: 160, priceId: 2100, lineTotal: 160 },
    ],
    ...overrides,
  };
}

export function orderPage(items: OrderResponse[], page = 1, totalCount = items.length, pageSize = 10): OrderPageResponse {
  return { items, page, pageSize, totalCount };
}

export const invoice: InvoiceResponse = {
  url: 'https://files.example.test/invoices/7.pdf?X-Amz-Signature=abc',
  expiresAt: '2026-11-24T12:05:00Z',
  invoiceNumber: 'INV-2026-000007',
};

/** 409 from POST /orders: the ProblemDetails extensions name the product and the grams left. */
export const insufficientStock: ProblemDetails & { productId: number; requiredG: number; availableG: number } = {
  status: 409,
  title: 'Insufficient stock',
  detail: 'Product 1 needs 1000 g but only 400 g are available.',
  productId: 1,
  requiredG: 1000,
  availableG: 400,
};

export const invoiceNotAvailable: ProblemDetails = {
  status: 409,
  title: 'Invoice unavailable',
  detail: 'Pending or cancelled orders have no invoice.',
};

// Admin: price history (deliberately not sorted, the UI sorts it), inventory and the stock ledger.
export const priceHistory: PriceHistoryResponse[] = [
  { id: 1100, amount: 280, currency: 'MXN', effectiveFrom: '2026-01-01T00:00:00Z', isCurrent: false },
  { id: 1102, amount: 320, currency: 'MXN', effectiveFrom: '2027-01-01T00:00:00Z', isCurrent: false },
  { id: 1101, amount: 300, currency: 'MXN', effectiveFrom: '2026-06-01T00:00:00Z', isCurrent: true },
];

export function priceCreated(skuId: number, amount: number, effectiveFrom: string): PriceCreatedResponse {
  return { id: 1200, skuId, amount, currency: 'MXN', effectiveFrom: `${effectiveFrom}T00:00:00Z` };
}

export const duplicatePrice: ProblemDetails = {
  status: 409,
  title: 'Duplicate price',
  detail: 'SKU 11 already has a price effective at that instant.',
};

export const pastDatePrice: ProblemDetails = {
  status: 422,
  title: 'Effective date in the past',
  detail: 'effectiveFrom must be today (UTC) or later.',
};

export const inventory: InventoryResponse[] = [
  { productId: 1, name: 'Ethiopia Yirgacheffe', stockG: 20000, lastMovementAt: '2026-11-20T09:00:00Z' },
  { productId: 2, name: 'Colombia Huila', stockG: 1500, lastMovementAt: '2026-11-21T09:00:00Z' },
];

export function movement(overrides: Partial<MovementResponse> = {}): MovementResponse {
  return { id: 1, deltaG: 20000, reason: 'Restock', orderId: null, createdAt: '2026-11-20T09:00:00Z', ...overrides };
}

export function movementPage(items: MovementResponse[], page = 1, totalCount = items.length): MovementPageResponse {
  return { items, page, pageSize: 10, totalCount };
}

export function restockResponse(productId: number, stockG: number, grams: number): RestockResponse {
  return {
    productId,
    stockG,
    movement: movement({ id: 99, deltaG: grams, createdAt: '2026-11-25T10:00:00Z' }),
  };
}
