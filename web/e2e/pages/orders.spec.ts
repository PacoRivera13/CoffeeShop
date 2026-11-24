import { expect, test, type Page } from '@playwright/test';
import type { OrderPageResponse } from '../../src/app/api/models/order-page-response';
import { order, orderPage } from '../../testing/fixtures';
import { expectAccessibleInBothSchemes, expectNoHorizontalOverflow, signInAs } from '../support/helpers';

/** Mocks GET /orders?page&pageSize, answering by page number, and records the pages asked for. */
async function mockOrders(page: Page, pages: Record<number, OrderPageResponse>): Promise<number[]> {
  const asked: number[] = [];
  await page.route(/\/api\/orders(\?.*)?$/, (route) => {
    if (route.request().method() !== 'GET') return route.fallback();
    const url = new URL(route.request().url());
    const number = Number(url.searchParams.get('page') ?? '1');
    asked.push(number);
    expect(url.searchParams.get('pageSize')).toBe('10');
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(pages[number]) });
  });
  return asked;
}

const firstPage = orderPage(
  [order({ id: 12, status: 'Paid', createdAt: '2026-11-24T10:00:00Z' }), order({ id: 11, status: 'Delivered' })],
  1,
  12,
);

test.describe('Orders page', () => {
  test.beforeEach(async ({ page }) => {
    await signInAs(page, 'Customer');
  });

  test('shows a table on desktop and cards on mobile, newest first', async ({ page }, testInfo) => {
    await mockOrders(page, { 1: firstPage });
    await page.goto('/orders');

    if (testInfo.project.name === 'mobile') {
      await expect(page.getByTestId('order-card')).toHaveCount(2);
      await expect(page.getByRole('table')).toHaveCount(0);
      await expect(page.getByTestId('order-card').first()).toContainText('Order #12');
    } else {
      await expect(page.getByRole('table', { name: 'Orders' })).toBeVisible();
      await expect(page.getByTestId('order-row')).toHaveCount(2);
      await expect(page.getByTestId('order-cards')).toHaveCount(0);
      await expect(page.getByTestId('order-row').first()).toContainText('Order #12');
    }
    await expect(page.getByTestId('order-status-label').first()).toHaveText('Paid');
  });

  test('pages through the orders', async ({ page }) => {
    const asked = await mockOrders(page, {
      1: firstPage,
      2: orderPage([order({ id: 2, status: 'Cancelled' })], 2, 12),
    });
    await page.goto('/orders');
    await expect(page.getByRole('link', { name: /Order #12/ })).toBeVisible();

    await page.getByRole('button', { name: 'Next page' }).click();

    await expect(page).toHaveURL('/orders?page=2');
    await expect(page.getByRole('link', { name: /Order #2\b/ })).toBeVisible();
    expect(asked).toEqual([1, 2]);
  });

  test('opens an order', async ({ page }) => {
    await mockOrders(page, { 1: firstPage });
    await page.goto('/orders');

    await expect(page.getByRole('link', { name: /Order #12/ })).toHaveAttribute('href', '/orders/12');
  });

  test('shows an empty state without orders', async ({ page }) => {
    await mockOrders(page, { 1: orderPage([], 1, 0) });
    await page.goto('/orders');

    await expect(page.getByRole('heading', { name: 'No orders yet' })).toBeVisible();
  });

  test('a page past the last order offers the first page', async ({ page }) => {
    await mockOrders(page, { 9: orderPage([], 9, 12), 1: firstPage });
    await page.goto('/orders?page=9');

    await expect(page.getByRole('heading', { name: 'No orders on this page' })).toBeVisible();
    await page.getByRole('link', { name: 'Go to the first page' }).click();

    await expect(page).toHaveURL('/orders');
    await expect(page.getByRole('link', { name: /Order #12/ })).toBeVisible();
  });

  test('has no horizontal overflow', async ({ page }) => {
    await mockOrders(page, { 1: firstPage });
    await page.goto('/orders');

    await expect(page.getByRole('link', { name: /Order #12/ })).toBeVisible();
    await expectNoHorizontalOverflow(page);
  });

  test('has no serious accessibility violations', async ({ page }, testInfo) => {
    await mockOrders(page, { 1: firstPage });
    await page.goto('/orders');

    await expectAccessibleInBothSchemes(page, testInfo, page.getByRole('link', { name: /Order #12/ }));
  });
});
