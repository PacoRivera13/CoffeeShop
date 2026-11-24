import { expect, test } from '@playwright/test';
import {
  expectAccessibleInBothSchemes,
  expectNoHorizontalOverflow,
  mockCatalog,
  signInAs,
} from '../support/helpers';

test.describe('Catalog page', () => {
  test('anonymous visitors are sent to sign in, keeping where they were going', async ({ page }) => {
    await page.goto('/');

    await expect(page).toHaveURL(/\/login\?returnUrl=%2F$/);
    await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible();
  });

  test.describe('signed in', () => {
    test.beforeEach(async ({ page }) => {
      await signInAs(page, 'Customer');
    });

    test('lists the products with the price of their first SKU', async ({ page }) => {
      await mockCatalog(page);
      await page.goto('/');

      await expect(page.getByRole('heading', { level: 1, name: 'Shop' })).toBeVisible();
      const ethiopia = page.getByRole('article', { name: 'Ethiopia Yirgacheffe' });
      await expect(ethiopia.getByTestId('price')).toContainText('300.00');
      await expect(ethiopia.getByTestId('roast-chip')).toContainText('Light roast');
      await expect(page.getByRole('article', { name: 'Colombia Huila' }).getByTestId('price')).toContainText('160.00');
    });

    test('shows prices as of the asOf query parameter', async ({ page }) => {
      const asOfSeen: (string | null)[] = [];
      await mockCatalog(page, undefined, (skuId, asOf) => {
        asOfSeen.push(asOf);
        return asOf === '2027-01-01T00:00:00Z' ? 345 : 300;
      });
      await page.goto('/?asOf=2027-01-01T00:00:00Z');

      const ethiopia = page.getByRole('article', { name: 'Ethiopia Yirgacheffe' });
      await expect(ethiopia.getByTestId('price')).toContainText('345.00');
      expect(asOfSeen).toContain('2027-01-01T00:00:00Z');
    });

    test('choosing another SKU updates the price', async ({ page }) => {
      await mockCatalog(page);
      await page.goto('/');
      const ethiopia = page.getByRole('article', { name: 'Ethiopia Yirgacheffe' });
      await expect(ethiopia.getByTestId('price')).toContainText('300.00');

      await ethiopia.getByRole('radio', { name: '1000 g' }).click();

      await expect(ethiopia.getByTestId('price')).toContainText('560.00');
    });

    test('a SKU without a price cannot be added', async ({ page }) => {
      await mockCatalog(page, { 11: 300, 12: 560, 13: 310 });
      await page.goto('/');

      const colombia = page.getByRole('article', { name: 'Colombia Huila' });
      await expect(colombia.getByTestId('price-unavailable')).toBeVisible();
      await expect(colombia.getByRole('button', { name: 'Add to cart' })).toBeDisabled();
    });

    test('adding to the cart confirms with an undoable snackbar and updates the cart', async ({ page }, testInfo) => {
      await mockCatalog(page);
      await page.goto('/');
      const ethiopia = page.getByRole('article', { name: 'Ethiopia Yirgacheffe' });
      await expect(ethiopia.getByTestId('price')).toContainText('300.00');

      await ethiopia.getByRole('button', { name: 'Add to cart' }).click();

      await expect(page.getByText('Ethiopia Yirgacheffe added to cart')).toBeVisible();
      if (testInfo.project.name === 'mobile') {
        const bar = page.getByTestId('cart-summary-bar');
        await expect(bar).toBeVisible();
        await expect(bar).toContainText('1 item');
        await expect(bar).toContainText('300.00');
      } else {
        await expect(page.getByRole('link', { name: 'Cart, 1 item' })).toBeVisible();
        await expect(page.getByTestId('cart-summary-bar')).toHaveCount(0);
      }

      await page.getByRole('button', { name: 'Undo' }).click();

      await expect(page.getByRole('link', { name: 'Cart, 0 items' })).toBeVisible();
      await expect(page.getByTestId('cart-summary-bar')).toHaveCount(0);
    });

    test('has no horizontal overflow', async ({ page }) => {
      await mockCatalog(page);
      await page.goto('/');

      await expect(page.getByRole('article', { name: 'Ethiopia Yirgacheffe' }).getByTestId('price')).toBeVisible();
      await expectNoHorizontalOverflow(page);
    });

    test('has no serious accessibility violations', async ({ page }, testInfo) => {
      await mockCatalog(page);
      await page.goto('/');

      await expectAccessibleInBothSchemes(
        page,
        testInfo,
        page.getByRole('article', { name: 'Colombia Huila' }).getByTestId('price'),
      );
    });
  });
});
