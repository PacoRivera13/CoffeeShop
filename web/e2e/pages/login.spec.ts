import { expect, test } from '@playwright/test';
import { admin, customer, invalidCredentials, loginResponse, priceHistory } from '../../testing/fixtures';
import { expectAccessibleInBothSchemes, expectNoHorizontalOverflow, mockApi, mockCatalog } from '../support/helpers';

test.describe('Login page', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
  });

  test('a customer signs in and lands on the shop', async ({ page }) => {
    await mockApi(page, 'POST', '/auth/login', 200, loginResponse('Customer'));

    await page.getByLabel('Email').fill(customer.email);
    await page.getByLabel('Password').fill(customer.password);
    await page.getByRole('button', { name: 'Sign in' }).click();

    await expect(page).toHaveURL('/');
    await expect(page.getByRole('heading', { level: 1, name: 'Shop' })).toBeVisible();
  });

  test('an admin is redirected to the admin area', async ({ page }) => {
    await mockApi(page, 'POST', '/auth/login', 200, loginResponse('Admin'));
    await mockCatalog(page);
    await mockApi(page, 'GET', '/skus/11/prices', 200, priceHistory);

    await page.getByLabel('Email').fill(admin.email);
    await page.getByLabel('Password').fill(admin.password);
    await page.getByRole('button', { name: 'Sign in' }).click();

    await expect(page).toHaveURL('/admin/prices');
    await expect(page.getByRole('heading', { level: 1, name: 'Prices' })).toBeVisible();
  });

  test('wrong credentials show the server error and stay on the page', async ({ page }) => {
    await mockApi(page, 'POST', '/auth/login', 401, invalidCredentials);

    await page.getByLabel('Email').fill(customer.email);
    await page.getByLabel('Password').fill('Wr0ng!Passw0rd');
    await page.getByRole('button', { name: 'Sign in' }).click();

    await expect(page.getByRole('alert')).toContainText('Invalid credentials');
    await expect(page).toHaveURL('/login');
  });

  test('empty or malformed fields show validation messages without calling the API', async ({ page }) => {
    let called = false;
    await page.route('**/api/auth/login', (route) => {
      called = true;
      return route.abort();
    });

    await page.getByRole('button', { name: 'Sign in' }).click();
    await expect(page.getByText('Email is required.')).toBeVisible();
    await expect(page.getByText('Password is required.')).toBeVisible();

    await page.getByLabel('Email').fill('not-an-email');
    await page.getByLabel('Email').blur();
    await expect(page.getByText('Enter a valid email address.')).toBeVisible();
    expect(called).toBe(false);
  });

  test('has no horizontal overflow', async ({ page }) => {
    await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible();
    await expectNoHorizontalOverflow(page);
  });

  test('has no serious accessibility violations', async ({ page }, testInfo) => {
    await expectAccessibleInBothSchemes(page, testInfo, page.getByRole('heading', { name: 'Sign in' }));
  });
});
