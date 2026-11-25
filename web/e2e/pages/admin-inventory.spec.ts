import { expect, test, type Page, type Request } from '@playwright/test';
import type { MovementPageResponse } from '../../src/app/api/models/movement-page-response';
import { inventory, movement, movementPage, restockResponse } from '../../testing/fixtures';
import { expectAccessibleInBothSchemes, expectNoHorizontalOverflow, mockApi, signInAs } from '../support/helpers';

const ledger = movementPage(
  [
    movement({ id: 3, deltaG: -1000, reason: 'Reserve', orderId: 7, createdAt: '2026-11-22T09:00:00Z' }),
    movement({ id: 2, deltaG: 1000, reason: 'Release', orderId: 7, createdAt: '2026-11-21T09:00:00Z' }),
    movement({ id: 1, deltaG: 20000, reason: 'Restock', createdAt: '2026-11-20T09:00:00Z' }),
  ],
  1,
  13,
);

/** Mocks the movements of every product; records (productId, page) pairs and answers through `pageFor`. */
async function mockMovements(
  page: Page,
  pageFor: (productId: number, pageNumber: number) => MovementPageResponse = () => ledger,
): Promise<[number, number][]> {
  const asked: [number, number][] = [];
  await page.route(/\/api\/admin\/inventory\/\d+\/movements(\?.*)?$/, (route) => {
    const url = new URL(route.request().url());
    const productId = Number(url.pathname.split('/').at(-2));
    const pageNumber = Number(url.searchParams.get('page') ?? '1');
    asked.push([productId, pageNumber]);
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(pageFor(productId, pageNumber)),
    });
  });
  return asked;
}

async function mockRestock(page: Page, status: number, body: unknown): Promise<Request[]> {
  const requests: Request[] = [];
  await page.route(/\/api\/admin\/inventory\/\d+\/restock$/, (route) => {
    requests.push(route.request());
    return route.fulfill({
      status,
      contentType: status >= 400 ? 'application/problem+json' : 'application/json',
      body: JSON.stringify(body),
    });
  });
  return requests;
}

const row = (page: Page, name: string) => page.getByTestId('stock-row').filter({ hasText: name });

test.describe('Admin inventory page', () => {
  test.beforeEach(async ({ page }) => {
    await signInAs(page, 'Admin');
    await mockApi(page, 'GET', '/admin/inventory', 200, inventory);
  });

  test('shows stock in grams and kilograms and flags low stock', async ({ page }) => {
    await mockMovements(page);
    await page.goto('/admin/inventory');

    const ethiopia = row(page, 'Ethiopia Yirgacheffe');
    await expect(ethiopia.getByTestId('stock-grams')).toHaveText('20,000 g');
    await expect(ethiopia.getByTestId('stock-kg')).toHaveText('20 kg');
    await expect(ethiopia.getByTestId('low-stock-chip')).toHaveCount(0);
    await expect(row(page, 'Colombia Huila').getByTestId('low-stock-chip')).toContainText('Low stock');
  });

  test('the ledger is a table on desktop and cards on mobile, and pages', async ({ page }, testInfo) => {
    const asked = await mockMovements(page);
    await page.goto('/admin/inventory');

    if (testInfo.project.name === 'mobile') {
      await expect(page.getByTestId('movement-card')).toHaveCount(3);
      await expect(page.getByRole('table')).toHaveCount(0);
    } else {
      await expect(page.getByRole('table', { name: 'Stock movements' }).getByTestId('movement-row')).toHaveCount(3);
    }
    await expect(page.getByTestId('movement-delta').first()).toHaveText('−1,000 g');

    await page.getByRole('button', { name: 'Next page' }).click();

    await expect(page).toHaveURL('/admin/inventory?product=1&page=2');
    await expect.poll(() => asked.at(-1)).toEqual([1, 2]);
  });

  test('choosing a product shows its movements', async ({ page }) => {
    const asked = await mockMovements(page);
    await page.goto('/admin/inventory');
    await expect(page.getByRole('heading', { level: 2, name: 'Ethiopia Yirgacheffe' })).toBeVisible();

    await page.getByRole('button', { name: 'Manage Colombia Huila' }).click();

    await expect(page).toHaveURL('/admin/inventory?product=2');
    await expect(page.getByRole('heading', { level: 2, name: 'Colombia Huila' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Manage Colombia Huila' })).toHaveAttribute('aria-pressed', 'true');
    await expect.poll(() => asked.at(-1)).toEqual([2, 1]);
  });

  test('restock bounds are checked before sending', async ({ page }) => {
    await mockMovements(page);
    const requests = await mockRestock(page, 201, {});
    await page.goto('/admin/inventory?product=2');

    await page.getByLabel('Grams to add').fill('0');
    await page.getByRole('button', { name: 'Restock Colombia Huila' }).click();
    await expect(page.getByText('Restock between 1 and 1,000,000 g.')).toBeVisible();

    await page.getByLabel('Grams to add').fill('1000001');
    await expect(page.getByText('Restock between 1 and 1,000,000 g.')).toBeVisible();
    expect(requests).toHaveLength(0);
  });

  test('a restock updates the stock and the ledger', async ({ page }) => {
    const restocked = movementPage([movement({ id: 99, deltaG: 5000, createdAt: '2026-11-25T10:00:00Z' })], 1, 1);
    let restockDone = false;
    await mockMovements(page, () => (restockDone ? restocked : movementPage([], 1, 0)));
    await page.route(/\/api\/admin\/inventory\/2\/restock$/, async (route) => {
      expect(route.request().postDataJSON()).toEqual({ grams: 5000 });
      restockDone = true;
      await route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify(restockResponse(2, 6500, 5000)),
      });
    });
    await page.goto('/admin/inventory?product=2');
    await expect(row(page, 'Colombia Huila').getByTestId('low-stock-chip')).toBeVisible();

    await page.getByLabel('Grams to add').fill('5000');
    await page.getByRole('button', { name: 'Restock Colombia Huila' }).click();

    const colombia = row(page, 'Colombia Huila');
    await expect(colombia.getByTestId('stock-grams')).toHaveText('6,500 g');
    await expect(colombia.getByTestId('stock-kg')).toHaveText('6.5 kg');
    await expect(colombia.getByTestId('low-stock-chip')).toHaveCount(0);
    await expect(page.getByText('Added 5,000 g to Colombia Huila')).toBeVisible();
    await expect(page.getByTestId('movement-delta').first()).toHaveText('+5,000 g');
  });

  test('a restock beyond the stock limit shows the error', async ({ page }) => {
    await mockMovements(page);
    await mockRestock(page, 422, { status: 422, title: 'Stock limit exceeded', detail: 'Stock would exceed the limit.' });
    await page.goto('/admin/inventory');

    await page.getByLabel('Grams to add').fill('1000000');
    await page.getByRole('button', { name: 'Restock Ethiopia Yirgacheffe' }).click();

    await expect(page.getByRole('alert')).toContainText('Stock limit exceeded');
    await expect(row(page, 'Ethiopia Yirgacheffe').getByTestId('stock-grams')).toHaveText('20,000 g');
  });

  test('has no horizontal overflow', async ({ page }) => {
    await mockMovements(page);
    await page.goto('/admin/inventory');

    await expect(page.getByTestId('movement-delta').first()).toBeVisible();
    await expectNoHorizontalOverflow(page);
  });

  test('has no serious accessibility violations', async ({ page }, testInfo) => {
    await mockMovements(page);
    await page.goto('/admin/inventory');

    await expectAccessibleInBothSchemes(page, testInfo, page.getByTestId('movement-delta').first());
  });
});
