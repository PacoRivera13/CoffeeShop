import { expect, test, type Page } from '@playwright/test';
import type { OrderResponse } from '../../src/app/api/models/order-response';
import { invoice, invoiceNotAvailable, order, products } from '../../testing/fixtures';
import { expectAccessibleInBothSchemes, expectNoHorizontalOverflow, mockApi, signInAs } from '../support/helpers';

async function openOrder(page: Page, current: OrderResponse): Promise<void> {
  await mockApi(page, 'GET', '/products', 200, products);
  await mockApi(page, 'GET', `/orders/${current.id}`, 200, current);
  await page.goto(`/orders/${current.id}`);
  await expect(page.getByRole('heading', { level: 1, name: `Order #${current.id}` })).toBeVisible();
}

/** window.open is replaced so the invoice URL can be asserted without leaving the mocked app. */
async function recordOpenedWindows(page: Page): Promise<void> {
  await page.addInitScript(() => {
    const opened: string[] = [];
    (window as unknown as { __opened: string[] }).__opened = opened;
    window.open = (url?: string | URL) => {
      opened.push(String(url));
      return null;
    };
  });
}

const openedWindows = (page: Page) => page.evaluate(() => (window as unknown as { __opened: string[] }).__opened);

test.describe('Order detail page', () => {
  test.beforeEach(async ({ page }) => {
    await signInAs(page, 'Customer');
  });

  test('shows the lines with their frozen prices, the status and the timeline', async ({ page }) => {
    await openOrder(page, order({ status: 'Preparing' }));

    const lines = page.getByTestId('order-line');
    await expect(lines).toHaveCount(2);
    await expect(lines.first()).toContainText('Ethiopia Yirgacheffe · Whole bean · 500 g');
    await expect(lines.first()).toContainText('2 ×');
    await expect(lines.first()).toContainText('600.00');
    await expect(page.getByTestId('order-total')).toContainText('760.00');
    await expect(page.getByTestId('order-status-label')).toHaveText('Preparing');
    const current = page.getByRole('list', { name: 'Order progress' }).locator('[aria-current="step"]');
    await expect(current).toContainText('Preparing');
  });

  test('a pending order can be paid; the button is disabled while paying', async ({ page }) => {
    let release!: () => void;
    const paid = new Promise<void>((resolve) => (release = resolve));
    await page.route('**/api/orders/7/pay', async (route) => {
      await paid;
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(order({ status: 'Paid' })) });
    });
    await openOrder(page, order({ status: 'Pending' }));
    await expect(page.getByTestId('invoice-button')).toHaveCount(0);

    await page.getByRole('button', { name: 'Pay' }).click();
    await expect(page.getByRole('button', { name: 'Pay' })).toBeDisabled();
    await expect(page.getByRole('button', { name: 'Cancel order' })).toBeDisabled();
    release();

    await expect(page.getByTestId('order-status-label')).toHaveText('Paid');
    await expect(page.getByRole('button', { name: 'Pay' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Cancel order' })).toBeEnabled();
    await expect(page.getByRole('button', { name: 'Invoice' })).toBeVisible();
  });

  test('cancelling asks first: Escape keeps the order, confirming cancels it', async ({ page }) => {
    let cancelCalls = 0;
    await page.route('**/api/orders/7/cancel', (route) => {
      cancelCalls++;
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(order({ status: 'Cancelled' })),
      });
    });
    await openOrder(page, order({ status: 'Paid' }));

    await page.getByRole('button', { name: 'Cancel order' }).click();
    const dialog = page.getByRole('alertdialog', { name: 'Cancel order #7?' });
    await expect(dialog).toBeVisible();
    await expect(dialog.getByRole('button', { name: 'Keep order' })).toBeFocused();
    await page.keyboard.press('Escape');
    await expect(dialog).toHaveCount(0);
    expect(cancelCalls).toBe(0);

    await page.getByRole('button', { name: 'Cancel order' }).click();
    await dialog.getByRole('button', { name: 'Cancel order' }).click();

    await expect(page.getByTestId('order-status-label')).toHaveText('Cancelled');
    await expect(page.getByRole('button', { name: 'Cancel order' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Invoice' })).toHaveCount(0);
    expect(cancelCalls).toBe(1);
  });

  test('a shipped order can be neither paid nor cancelled, but has its invoice', async ({ page }) => {
    await openOrder(page, order({ status: 'Shipped' }));

    await expect(page.getByRole('button', { name: 'Pay' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Cancel order' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Invoice' })).toBeVisible();
  });

  test('the invoice is polled while it is generated (202 -> 202 -> 200) and then opened', async ({ page }) => {
    await recordOpenedWindows(page);
    const responses = [
      { status: 202, body: { status: 'Pending' } },
      { status: 202, body: { status: 'Pending' } },
      { status: 200, body: invoice },
    ];
    let calls = 0;
    await page.route('**/api/orders/7/invoice', (route) => {
      calls++;
      const { status, body } = responses.shift()!;
      return route.fulfill({
        status,
        contentType: 'application/json',
        headers: status === 202 ? { 'Retry-After': '0' } : {},
        body: JSON.stringify(body),
      });
    });
    await openOrder(page, order({ status: 'Paid' }));

    await page.getByRole('button', { name: 'Invoice' }).click();

    await expect(page.getByRole('link', { name: `Open invoice ${invoice.invoiceNumber}` })).toHaveAttribute(
      'href',
      invoice.url,
    );
    await expect(page.getByTestId('invoice-status')).toContainText('is ready');
    expect(calls).toBe(3);
    expect(await openedWindows(page)).toEqual([invoice.url]);
  });

  test('an invoice that cannot exist shows the conflict', async ({ page }) => {
    await page.route('**/api/orders/7/invoice', (route) =>
      route.fulfill({ status: 409, contentType: 'application/problem+json', body: JSON.stringify(invoiceNotAvailable) }),
    );
    await openOrder(page, order({ status: 'Paid' }));

    await page.getByRole('button', { name: 'Invoice' }).click();

    await expect(page.getByRole('alert')).toContainText('Invoice unavailable');
    await expect(page.getByRole('button', { name: 'Invoice' })).toBeEnabled();
  });

  test('an unknown order shows the error', async ({ page }) => {
    await mockApi(page, 'GET', '/products', 200, products);
    await mockApi(page, 'GET', '/orders/99', 404, { status: 404, title: 'Order not found' });
    await page.goto('/orders/99');

    await expect(page.getByRole('alert')).toContainText('Order not found');
  });

  test('has no horizontal overflow', async ({ page }) => {
    await openOrder(page, order({ status: 'Pending' }));

    await expect(page.getByTestId('order-line').first()).toBeVisible();
    await expectNoHorizontalOverflow(page);
  });

  test('has no serious accessibility violations', async ({ page }, testInfo) => {
    await openOrder(page, order({ status: 'Paid' }));

    await expectAccessibleInBothSchemes(page, testInfo, page.getByRole('button', { name: 'Invoice' }));
  });
});
