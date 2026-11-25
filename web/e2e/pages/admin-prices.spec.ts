import { expect, test, type Page, type Request } from '@playwright/test';
import type { PriceHistoryResponse } from '../../src/app/api/models/price-history-response';
import { duplicatePrice, pastDatePrice, priceCreated, priceHistory } from '../../testing/fixtures';
import {
  expectAccessibleInBothSchemes,
  expectNoHorizontalOverflow,
  mockCatalog,
  signInAs,
} from '../support/helpers';

const todayUtc = () => new Date().toISOString().slice(0, 10);

/** Mocks GET /skus/{id}/prices; each call takes the next history in the list (the last one repeats). */
async function mockHistory(page: Page, histories: PriceHistoryResponse[][]): Promise<number[]> {
  const asked: number[] = [];
  await page.route(/\/api\/skus\/\d+\/prices$/, (route) => {
    asked.push(Number(new URL(route.request().url()).pathname.split('/').at(-2)));
    const body = histories.length > 1 ? histories.shift()! : histories[0];
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });
  });
  return asked;
}

async function mockCreate(page: Page, status: number, body: unknown): Promise<Request[]> {
  const requests: Request[] = [];
  await page.route('**/api/prices', (route) => {
    requests.push(route.request());
    return route.fulfill({
      status,
      contentType: status >= 400 ? 'application/problem+json' : 'application/json',
      body: JSON.stringify(body),
    });
  });
  return requests;
}

async function fillPrice(page: Page, amount: string, date: string) {
  await page.getByLabel('Amount').fill(amount);
  await page.getByTestId('effective-from').fill(date);
  await page.getByRole('button', { name: 'Add price' }).click();
}

test.describe('Admin prices page', () => {
  test.describe('access', () => {
    test('customers opening admin pages are sent to the catalog', async ({ page }) => {
      await signInAs(page, 'Customer');
      await mockCatalog(page);

      await page.goto('/admin/prices');
      await expect(page).toHaveURL('/');
      await page.goto('/admin/inventory');
      await expect(page).toHaveURL('/');
      await expect(page.getByRole('heading', { level: 1, name: 'Shop' })).toBeVisible();
    });

    test('anonymous visitors are sent to sign in', async ({ page }) => {
      await page.goto('/admin/prices');

      await expect(page).toHaveURL(/\/login\?returnUrl=%2Fadmin%2Fprices$/);
    });

    test('/admin opens the prices page', async ({ page }) => {
      await signInAs(page, 'Admin');
      await mockCatalog(page);
      await mockHistory(page, [priceHistory]);
      await page.goto('/admin');

      await expect(page).toHaveURL('/admin/prices');
      await expect(page.getByRole('heading', { level: 1, name: 'Prices' })).toBeVisible();
    });
  });

  test.describe('as admin', () => {
    test.beforeEach(async ({ page }) => {
      await signInAs(page, 'Admin');
      await mockCatalog(page);
    });

    test('shows the history newest first with the current price marked', async ({ page }, testInfo) => {
      await mockHistory(page, [priceHistory]);
      await page.goto('/admin/prices');

      if (testInfo.project.name === 'mobile') {
        await expect(page.getByRole('table')).toHaveCount(0);
        const cards = page.getByTestId('price-card');
        await expect(cards).toHaveCount(3);
        await expect(cards.first()).toContainText('320.00');
        await expect(cards.nth(1).getByTestId('current-chip')).toContainText('Current');
      } else {
        const rows = page.getByRole('table', { name: 'Price history' }).getByTestId('price-row');
        await expect(rows).toHaveCount(3);
        await expect(rows.first()).toContainText('320.00');
        await expect(rows.nth(1).getByTestId('current-chip')).toContainText('Current');
      }
      await expect(page.getByTestId('current-chip')).toHaveCount(1);
      await expect(page.getByRole('button', { name: /edit|delete/i })).toHaveCount(0);
    });

    test('choosing a SKU loads its history', async ({ page }) => {
      const asked = await mockHistory(page, [priceHistory]);
      await page.goto('/admin/prices');
      await expect(page.getByTestId('current-chip')).toBeVisible();

      await page.getByRole('combobox', { name: 'SKU' }).click();
      await page.getByRole('option', { name: 'Ethiopia Yirgacheffe · Whole bean · 1000 g' }).click();

      await expect(page).toHaveURL('/admin/prices?sku=12');
      await expect(page.getByRole('heading', { name: /History · Ethiopia Yirgacheffe · Whole bean · 1000 g/ })).toBeVisible();
      expect(asked).toEqual([11, 12]);
    });

    test('validates the form before sending and limits the date to today (UTC) or later', async ({ page }) => {
      await mockHistory(page, [priceHistory]);
      const requests = await mockCreate(page, 201, {});
      await page.goto('/admin/prices');

      await expect(page.getByTestId('effective-from')).toHaveAttribute('min', todayUtc());
      await page.getByLabel('Currency').fill('PESO');
      await page.getByRole('button', { name: 'Add price' }).click();

      await expect(page.getByText('Amount is required.')).toBeVisible();
      await expect(page.getByText('Effective date is required.')).toBeVisible();
      await page.getByLabel('Amount').fill('0');
      await page.getByLabel('Currency').fill('MX');
      await expect(page.getByText('Amount must be greater than 0.')).toBeVisible();
      await expect(page.getByText('Use a 3-letter currency code, e.g. MXN.')).toBeVisible();
      expect(requests).toHaveLength(0);
    });

    test('a new price is saved and appears in the history', async ({ page }) => {
      const added: PriceHistoryResponse = {
        id: 1200,
        amount: 350,
        currency: 'MXN',
        effectiveFrom: '2099-01-01T00:00:00Z',
        isCurrent: false,
      };
      await mockHistory(page, [priceHistory, [...priceHistory, added]]);
      const requests = await mockCreate(page, 201, priceCreated(11, 350, '2099-01-01'));
      await page.goto('/admin/prices');
      await expect(page.getByTestId('current-chip')).toBeVisible();

      await fillPrice(page, '350', '2099-01-01');

      await expect(page.getByText('Price saved')).toBeVisible();
      await expect(page.getByText('350.00').first()).toBeVisible();
      expect(requests[0].postDataJSON()).toEqual({
        skuId: 11,
        amount: 350,
        currency: 'MXN',
        effectiveFrom: '2099-01-01',
      });
      await expect(page.getByLabel('Amount')).toHaveValue('');
    });

    test('a duplicate date (409) is shown on the date field', async ({ page }) => {
      await mockHistory(page, [priceHistory]);
      await mockCreate(page, 409, duplicatePrice);
      await page.goto('/admin/prices');

      await fillPrice(page, '350', '2099-01-01');

      await expect(page.getByText('A price already starts on this date.')).toBeVisible();
    });

    test('a date the server finds in the past (422) is shown on the date field', async ({ page }) => {
      await mockHistory(page, [priceHistory]);
      await mockCreate(page, 422, pastDatePrice);
      await page.goto('/admin/prices');

      await fillPrice(page, '350', todayUtc());

      await expect(page.getByText('Choose today or a later date.')).toBeVisible();
    });

    test('other rejections are shown in the banner', async ({ page }) => {
      await mockHistory(page, [priceHistory]);
      await mockCreate(page, 400, {
        status: 400,
        title: 'One or more validation errors occurred.',
        errors: { body: ['currency must be MXN.'] },
      });
      await page.goto('/admin/prices');

      await page.getByLabel('Currency').fill('EUR');
      await fillPrice(page, '350', '2099-01-01');

      await expect(page.getByRole('alert')).toContainText('currency must be MXN.');
    });

    test('has no horizontal overflow', async ({ page }) => {
      await mockHistory(page, [priceHistory]);
      await page.goto('/admin/prices');

      await expect(page.getByTestId('current-chip')).toBeVisible();
      await expectNoHorizontalOverflow(page);
    });

    test('has no serious accessibility violations', async ({ page }, testInfo) => {
      await mockHistory(page, [priceHistory]);
      await page.goto('/admin/prices');

      await expectAccessibleInBothSchemes(page, testInfo, page.getByTestId('current-chip'));
    });
  });
});

