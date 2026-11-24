import { expect, test, type Page, type Request } from '@playwright/test';
import { insufficientStock, order, products } from '../../testing/fixtures';
import {
  cartLine,
  expectAccessibleInBothSchemes,
  expectNoHorizontalOverflow,
  mockApi,
  mockCatalog,
  seedCart,
  signInAs,
} from '../support/helpers';

/** Replies to POST /orders with each response in turn and records every request. */
async function mockCheckout(page: Page, responses: { status: number; body: unknown }[]): Promise<Request[]> {
  const requests: Request[] = [];
  await page.route('**/api/orders', (route) => {
    if (route.request().method() !== 'POST') return route.fallback();
    requests.push(route.request());
    const { status, body } = responses.shift() ?? { status: 500, body: { title: 'Unexpected request' } };
    return route.fulfill({
      status,
      contentType: status >= 400 ? 'application/problem+json' : 'application/json',
      body: JSON.stringify(body),
    });
  });
  return requests;
}

test.describe('Cart page', () => {
  test.beforeEach(async ({ page }) => {
    await signInAs(page, 'Customer');
  });

  test('an empty cart invites the user back to the shop', async ({ page }) => {
    await page.goto('/cart');

    await expect(page.getByRole('heading', { name: 'Your cart is empty' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Browse coffee' })).toHaveAttribute('href', '/');
  });

  test('shows the lines and an estimated total that follows the quantities', async ({ page }) => {
    await seedCart(page, [cartLine(11, 2), cartLine(21, 1)]);
    await page.goto('/cart');

    const summary = page.getByTestId('cart-summary');
    await expect(summary).toContainText('Estimated total');
    await expect(page.getByTestId('summary-total')).toContainText('760.00');
    await expect(page.getByTestId('summary-note')).toContainText('final total is calculated');

    const ethiopia = page.getByTestId('cart-line').filter({ hasText: 'Ethiopia Yirgacheffe' });
    await ethiopia.getByRole('button', { name: 'Increase quantity' }).click();

    await expect(page.getByTestId('summary-total')).toContainText('1,060.00');
    await expect(page.getByTestId('summary-count')).toHaveText('4');
  });

  test('quantities stay between 1 and 50', async ({ page }) => {
    await seedCart(page, [cartLine(11, 1), cartLine(21, 50)]);
    await page.goto('/cart');

    const ethiopia = page.getByTestId('cart-line').filter({ hasText: 'Ethiopia Yirgacheffe' });
    const colombia = page.getByTestId('cart-line').filter({ hasText: 'Colombia Huila' });
    await expect(ethiopia.getByRole('button', { name: 'Decrease quantity' })).toBeDisabled();
    await expect(colombia.getByRole('button', { name: 'Increase quantity' })).toBeDisabled();
    await expect(colombia.getByRole('button', { name: 'Decrease quantity' })).toBeEnabled();
  });

  test('removing the last line empties the cart', async ({ page }) => {
    await seedCart(page, [cartLine(11, 1)]);
    await page.goto('/cart');

    await page.getByRole('button', { name: 'Remove Ethiopia Yirgacheffe from the cart' }).click();

    await expect(page.getByRole('heading', { name: 'Your cart is empty' })).toBeVisible();
  });

  test('checkout places the order, clears the cart and opens the order', async ({ page }) => {
    await seedCart(page, [cartLine(11, 2), cartLine(21, 1)]);
    await mockApi(page, 'GET', '/orders/42', 200, order({ id: 42 }));
    await mockApi(page, 'GET', '/products', 200, products);
    const requests = await mockCheckout(page, [{ status: 201, body: order({ id: 42 }) }]);
    await page.goto('/cart');

    await page.getByRole('button', { name: 'Checkout' }).click();

    await expect(page).toHaveURL('/orders/42');
    await expect(page.getByText('Order #42 placed')).toBeVisible();
    await expect(page.getByRole('heading', { level: 1, name: 'Order #42' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Cart, 0 items' })).toBeVisible();
    expect(requests[0].postDataJSON()).toEqual({
      items: [
        { skuId: 11, qty: 2 },
        { skuId: 21, qty: 1 },
      ],
    });
  });

  test('a stock conflict names the product and the grams left, and keeps the cart', async ({ page }) => {
    await seedCart(page, [cartLine(12, 1)]);
    await mockCheckout(page, [{ status: 409, body: insufficientStock }]);
    await page.goto('/cart');

    await page.getByRole('button', { name: 'Checkout' }).click();

    const banner = page.getByRole('alert');
    await expect(banner).toContainText('Not enough stock');
    await expect(banner).toContainText('Ethiopia Yirgacheffe: only 400 g available');
    await expect(page).toHaveURL('/cart');
    await expect(page.getByTestId('cart-line')).toHaveCount(1);
    await expect(page.getByRole('button', { name: 'Checkout' })).toBeEnabled();
  });

  test('a retry after a failure sends the same Idempotency-Key', async ({ page }) => {
    await seedCart(page, [cartLine(11, 1)]);
    await mockApi(page, 'GET', '/orders/43', 200, order({ id: 43 }));
    await mockApi(page, 'GET', '/products', 200, products);
    const requests = await mockCheckout(page, [
      { status: 503, body: { status: 503, title: 'Service unavailable' } },
      { status: 200, body: order({ id: 43 }) },
    ]);
    await page.goto('/cart');

    await page.getByRole('button', { name: 'Checkout' }).click();
    await expect(page.getByRole('alert')).toContainText('Service unavailable');
    await page.getByRole('button', { name: 'Checkout' }).click();

    await expect(page).toHaveURL('/orders/43');
    expect(requests).toHaveLength(2);
    const firstKey = await requests[0].headerValue('Idempotency-Key');
    expect(firstKey).toMatch(/^[0-9a-f-]{36}$/);
    expect(await requests[1].headerValue('Idempotency-Key')).toBe(firstKey);
  });

  test('stacks on mobile with a full-width checkout, and puts the summary beside the lines on desktop', async ({
    page,
  }, testInfo) => {
    await seedCart(page, [cartLine(11, 1)]);
    await page.goto('/cart');

    const lines = (await page.getByRole('list', { name: 'Cart items' }).boundingBox())!;
    const summary = (await page.getByTestId('cart-summary').boundingBox())!;
    if (testInfo.project.name === 'mobile') {
      const button = (await page.getByRole('button', { name: 'Checkout' }).boundingBox())!;
      expect(button.width).toBeGreaterThan(page.viewportSize()!.width - 80);
      expect(summary.y).toBeGreaterThanOrEqual(lines.y + lines.height);
    } else {
      expect(summary.x).toBeGreaterThanOrEqual(lines.x + lines.width);
    }
  });

  test('has no horizontal overflow', async ({ page }) => {
    await seedCart(page, [cartLine(11, 2), cartLine(21, 1)]);
    await page.goto('/cart');

    await expect(page.getByTestId('cart-line').first()).toBeVisible();
    await expectNoHorizontalOverflow(page);
  });

  test('has no serious accessibility violations', async ({ page }, testInfo) => {
    await seedCart(page, [cartLine(11, 2), cartLine(21, 1)]);
    await mockCatalog(page);
    await page.goto('/cart');

    await expectAccessibleInBothSchemes(page, testInfo, page.getByTestId('cart-summary'));
  });
});
