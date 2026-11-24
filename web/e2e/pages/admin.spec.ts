import { expect, test } from '@playwright/test';
import { expectAccessibleInBothSchemes, expectNoHorizontalOverflow, signInAs } from '../support/helpers';

test.describe('Admin page', () => {
  test('customers are redirected to the shop', async ({ page }) => {
    await signInAs(page, 'Customer');
    await page.goto('/admin');

    await expect(page).toHaveURL('/');
    await expect(page.getByRole('heading', { level: 1, name: 'Shop' })).toBeVisible();
  });

  test('anonymous visitors are sent to sign in', async ({ page }) => {
    await page.goto('/admin');

    await expect(page).toHaveURL(/\/login\?returnUrl=%2Fadmin$/);
  });

  test('admins see the admin area', async ({ page }) => {
    await signInAs(page, 'Admin');
    await page.goto('/admin');

    await expect(page.getByRole('heading', { level: 1, name: 'Admin' })).toBeVisible();
  });

  test('has no horizontal overflow', async ({ page }) => {
    await signInAs(page, 'Admin');
    await page.goto('/admin');

    await expect(page.getByRole('heading', { level: 1, name: 'Admin' })).toBeVisible();
    await expectNoHorizontalOverflow(page);
  });

  test('has no serious accessibility violations', async ({ page }, testInfo) => {
    await signInAs(page, 'Admin');
    await page.goto('/admin');

    await expectAccessibleInBothSchemes(page, testInfo, page.getByRole('heading', { level: 1, name: 'Admin' }));
  });
});
