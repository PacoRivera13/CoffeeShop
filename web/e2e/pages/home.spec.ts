import { expect, test } from '@playwright/test';
import { customer } from '../../testing/fixtures';
import { expectAccessibleInBothSchemes, expectNoHorizontalOverflow, signInAs } from '../support/helpers';

test.describe('Home page', () => {
  test('anonymous visitors are sent to sign in, keeping where they were going', async ({ page }) => {
    await page.goto('/');

    await expect(page).toHaveURL(/\/login\?returnUrl=%2F$/);
    await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible();
  });

  test('a signed-in customer sees the shop', async ({ page }) => {
    await signInAs(page, 'Customer');
    await page.goto('/');

    await expect(page.getByRole('heading', { level: 1, name: 'Shop' })).toBeVisible();
    await expect(page.getByText(`Signed in as ${customer.email}`)).toBeVisible();
  });

  test('has no horizontal overflow', async ({ page }) => {
    await signInAs(page, 'Customer');
    await page.goto('/');

    await expect(page.getByRole('heading', { level: 1, name: 'Shop' })).toBeVisible();
    await expectNoHorizontalOverflow(page);
  });

  test('has no serious accessibility violations', async ({ page }, testInfo) => {
    await signInAs(page, 'Customer');
    await page.goto('/');

    await expectAccessibleInBothSchemes(page, testInfo, page.getByRole('heading', { level: 1, name: 'Shop' }));
  });
});
