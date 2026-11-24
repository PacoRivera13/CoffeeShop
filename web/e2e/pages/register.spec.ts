import { expect, test } from '@playwright/test';
import { customer, duplicateEmail, loginResponse, registerResponse, weakPassword } from '../../testing/fixtures';
import { expectAccessibleInBothSchemes, expectNoHorizontalOverflow, mockApi } from '../support/helpers';

test.describe('Register page', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/register');
  });

  async function fillForm(page: import('@playwright/test').Page, password: string = customer.password) {
    await page.getByLabel('Name').fill(customer.name);
    await page.getByLabel('Email').fill(customer.email);
    await page.getByLabel('Password').fill(password);
  }

  test('a new customer is registered, signed in and taken to the shop', async ({ page }) => {
    await mockApi(page, 'POST', '/auth/register', 201, registerResponse);
    await mockApi(page, 'POST', '/auth/login', 200, loginResponse('Customer'));

    await fillForm(page);
    await page.getByRole('button', { name: 'Create account' }).click();

    await expect(page).toHaveURL('/');
    await expect(page.getByRole('heading', { level: 1, name: 'Shop' })).toBeVisible();
  });

  test('if signing in fails after registering, the user is sent to sign in instead of retrying', async ({ page }) => {
    await mockApi(page, 'POST', '/auth/register', 201, registerResponse);
    await page.route('**/api/auth/login', (route) => route.abort());

    await fillForm(page);
    await page.getByRole('button', { name: 'Create account' }).click();

    await expect(page).toHaveURL('/login?registered=true');
    await expect(page.getByRole('status')).toContainText('Your account was created');
  });

  test('a weak password shows the server validation message', async ({ page }) => {
    await mockApi(page, 'POST', '/auth/register', 400, weakPassword);

    await fillForm(page, 'longbutweak');
    await page.getByRole('button', { name: 'Create account' }).click();

    await expect(page.getByRole('alert')).toContainText('non alphanumeric');
    await expect(page).toHaveURL('/register');
  });

  test('an email already registered shows the conflict', async ({ page }) => {
    await mockApi(page, 'POST', '/auth/register', 409, duplicateEmail);

    await fillForm(page);
    await page.getByRole('button', { name: 'Create account' }).click();

    await expect(page.getByRole('alert')).toContainText('Email already registered');
  });

  test('client-side validation runs before any request', async ({ page }) => {
    await page.getByRole('button', { name: 'Create account' }).click();

    await expect(page.getByText('Name is required.')).toBeVisible();
    await expect(page.getByText('Email is required.')).toBeVisible();
    await expect(page.getByText('Password is required.')).toBeVisible();

    await page.getByLabel('Password').fill('short');
    await page.getByLabel('Password').blur();
    await expect(page.getByText('Password must have at least 8 characters.')).toBeVisible();

    await page.getByLabel('Name').fill('   ');
    await page.getByLabel('Name').blur();
    await expect(page.getByText('Name is required.')).toBeVisible();
  });

  test('has no horizontal overflow', async ({ page }) => {
    await expect(page.getByRole('heading', { name: 'Create account' })).toBeVisible();
    await expectNoHorizontalOverflow(page);
  });

  test('has no serious accessibility violations', async ({ page }, testInfo) => {
    await expectAccessibleInBothSchemes(page, testInfo, page.getByRole('heading', { name: 'Create account' }));
  });
});
