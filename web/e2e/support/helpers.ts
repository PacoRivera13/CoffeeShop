import AxeBuilder from '@axe-core/playwright';
import { expect, type Locator, type Page, type TestInfo } from '@playwright/test';
import type { CartLine } from '../../src/app/core/cart/cart.service';
import { fakeAccessToken, price, products, type Role } from '../../testing/fixtures';

const SESSION_STORAGE_KEY = 'roastery.session';

/** Starts the page already signed in, as the app would be after a login in this tab. */
export async function signInAs(page: Page, role: Role): Promise<void> {
  const token = fakeAccessToken(role);
  await page.addInitScript(
    ([key, value]) => sessionStorage.setItem(key, value),
    [SESSION_STORAGE_KEY, token] as const,
  );
}

/** Replies to one API call with a JSON body; every test mocks the API, nothing reaches a real server. */
export async function mockApi(page: Page, method: string, path: string, status: number, body: unknown): Promise<void> {
  await page.route(`**/api${path}`, (route) =>
    route.request().method() === method
      ? route.fulfill({ status, contentType: status >= 400 ? 'application/problem+json' : 'application/json', body: JSON.stringify(body) })
      : route.fallback(),
  );
}

export async function expectNoHorizontalOverflow(page: Page): Promise<void> {
  const { scrollWidth, clientWidth } = await page.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    clientWidth: document.documentElement.clientWidth,
  }));
  expect(scrollWidth, 'page scrolls horizontally').toBeLessThanOrEqual(clientWidth);
}

/** No serious or critical axe violations (WCAG 2 A/AA). */
export async function expectAccessible(page: Page): Promise<void> {
  const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']).analyze();
  const blocking = results.violations
    .filter((v) => v.impact === 'serious' || v.impact === 'critical')
    .map((v) => `${v.id} (${v.impact}): ${v.nodes.map((n) => n.target.join(' ')).join(', ')}`);
  expect(blocking, blocking.join('\n')).toEqual([]);
}

/**
 * Light mode on every project; dark mode on desktop too, as the milestone requires. Waits for the page to settle
 * before analysing so transitions do not skew contrast.
 */
export async function expectAccessibleInBothSchemes(page: Page, testInfo: TestInfo, ready: Locator): Promise<void> {
  await page.emulateMedia({ colorScheme: 'light', reducedMotion: 'reduce' });
  await expect(ready).toBeVisible();
  await expectAccessible(page);

  if (testInfo.project.name === 'desktop') {
    await page.emulateMedia({ colorScheme: 'dark', reducedMotion: 'reduce' });
    // The dark cream surface (#1E1612) proves the dark tokens are active before axe measures contrast.
    await expect(page.locator('body')).toHaveCSS('background-color', 'rgb(30, 22, 18)');
    await expectAccessible(page);
  }
}

export async function expectMinTouchTarget(locator: Locator, size = 44): Promise<void> {
  const box = await locator.boundingBox();
  expect(box, 'element is not rendered').not.toBeNull();
  expect(box!.width, 'touch target width').toBeGreaterThanOrEqual(size);
  expect(box!.height, 'touch target height').toBeGreaterThanOrEqual(size);
}

const CART_STORAGE_KEY = 'roastery.cart';

/** Prices by SKU id for the default catalogue mock. */
export const defaultPrices: Readonly<Record<number, number>> = { 11: 300, 12: 560, 13: 310, 21: 160 };

/**
 * Mocks GET /products and GET /skus/{id}/price. A SKU missing from `prices` answers 404; `priceFor` can vary the
 * amount by the requested asOf.
 */
export async function mockCatalog(
  page: Page,
  prices: Readonly<Record<number, number>> = defaultPrices,
  priceFor?: (skuId: number, asOf: string | null) => number | undefined,
): Promise<void> {
  await mockApi(page, 'GET', '/products', 200, products);
  await page.route(/\/api\/skus\/\d+\/price(\?.*)?$/, (route) => {
    const url = new URL(route.request().url());
    const skuId = Number(url.pathname.split('/').at(-2));
    const asOf = url.searchParams.get('asOf');
    const amount = priceFor ? priceFor(skuId, asOf) : prices[skuId];
    return amount === undefined
      ? route.fulfill({ status: 404, contentType: 'application/problem+json', body: JSON.stringify({ title: 'No effective price' }) })
      : route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(price(skuId, amount, asOf ?? undefined)) });
  });
}

/** Starts the page with these lines already in the cart of this tab. */
export async function seedCart(page: Page, lines: readonly CartLine[]): Promise<void> {
  await page.addInitScript(([key, value]) => sessionStorage.setItem(key, value), [
    CART_STORAGE_KEY,
    JSON.stringify(lines),
  ] as const);
}

export function cartLine(skuId: 11 | 12 | 13 | 21, qty: number): CartLine {
  const product = products.find((p) => p.skus.some((s) => Number(s.id) === skuId))!;
  const sku = product.skus.find((s) => Number(s.id) === skuId)!;
  return {
    skuId,
    productId: Number(product.id),
    name: product.name,
    roast: product.roast,
    grind: sku.grind,
    sizeG: Number(sku.sizeG),
    unitPrice: defaultPrices[skuId],
    currency: 'MXN',
    qty,
  };
}
