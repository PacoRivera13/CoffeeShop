import { expect, test, type Page } from '@playwright/test';

/**
 * Full stack, no mocks: runs against `docker compose up` (web on :8080 by default, see E2E_BASE_URL) with the
 * database migrated. Admin credentials come from E2E_ADMIN_EMAIL / E2E_ADMIN_PASSWORD and must match the
 * Admin__Email / Admin__Password the API was started with. Run with `npm run e2e:full`.
 */

const adminEmail = process.env['E2E_ADMIN_EMAIL'];
const adminPassword = process.env['E2E_ADMIN_PASSWORD'];

const runId = `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
const customer = {
  name: 'E2E Customer',
  email: `e2e+${runId}@example.test`,
  password: `E2e!${runId}Aa1`,
};

function addDaysUtc(days: number): string {
  const date = new Date();
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
}

/** "MX$1,234.50" -> 1234.5 */
function amountOf(text: string): number {
  return Number(text.replace(/[^\d.]/g, ''));
}

async function signIn(page: Page, email: string, password: string): Promise<void> {
  await page.goto('/login');
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page).not.toHaveURL(/\/login/);
}

async function signOut(page: Page): Promise<void> {
  await page.getByRole('button', { name: 'Sign out' }).click();
  await expect(page).toHaveURL('/login');
}

test('a customer buys coffee priced and stocked by an admin, and downloads the invoice @full', async ({
  page,
  request,
}) => {
  test.setTimeout(180_000);
  if (!adminEmail || !adminPassword) {
    throw new Error('Set E2E_ADMIN_EMAIL and E2E_ADMIN_PASSWORD to the Admin__Email / Admin__Password of the stack.');
  }

  await test.step('register a new customer', async () => {
    await page.goto('/register');
    await page.getByLabel('Name').fill(customer.name);
    await page.getByLabel('Email').fill(customer.email);
    await page.getByLabel('Password').fill(customer.password);
    await page.getByRole('button', { name: 'Create account' }).click();
    await expect(page.getByRole('heading', { level: 1, name: 'Shop' })).toBeVisible();
    await signOut(page);
  });

  await signIn(page, adminEmail, adminPassword);

  // The prices page opens on the catalogue's first SKU, which is also the one the catalog card preselects.
  // Product names are not unique (the demo seed adds a second Yirgacheffe), so cards and stock rows are located by
  // position: the catalogue, the prices page and the inventory all list products in id order.
  const firstCard = () => page.getByTestId('product-card').first();
  const { productName, todayPrice } = await test.step('admin schedules a price for tomorrow', async () => {
    await page.goto('/admin/prices');
    const current = page.getByRole('table', { name: 'Price history' }).getByTestId('price-row').filter({
      has: page.getByTestId('current-chip'),
    });
    const todayText = (await current.getByTestId('price').textContent())!;
    const heading = (await page.getByRole('heading', { level: 2, name: /^History · / }).textContent())!;
    const name = heading.replace(/^History · /, '').split(' · ')[0].trim();

    // Earlier runs may already hold tomorrow's price for this SKU (409): take the next free day.
    for (let day = 1; day <= 60; day++) {
      await page.getByLabel('Amount').fill(String(amountOf(todayText) + 10));
      await page.getByTestId('effective-from').fill(addDaysUtc(day));
      const response = page.waitForResponse((r) => r.url().endsWith('/api/prices') && r.request().method() === 'POST');
      await page.getByRole('button', { name: 'Add price' }).click();
      const status = (await response).status();
      if (status === 201) break;
      expect(status, 'only a duplicate date may be retried').toBe(409);
    }
    await expect(page.getByText('Price saved')).toBeVisible();
    return { productName: name, todayPrice: todayText.trim() };
  });

  await test.step('the catalog still shows today\'s price', async () => {
    await page.goto('/');
    const card = firstCard();
    await expect(card.getByTestId('product-name')).toHaveText(productName);
    await expect(card.getByTestId('price')).toHaveText(todayPrice);
  });

  await test.step('admin restocks the product', async () => {
    await page.goto('/admin/inventory');
    const row = page.getByTestId('stock-row').first();
    await row.getByRole('button', { name: `Manage ${productName}` }).click();
    const before = amountOf((await row.getByTestId('stock-grams').textContent())!);
    await page.getByLabel('Grams to add').fill('5000');
    await page.getByRole('button', { name: `Restock ${productName}` }).click();
    await expect(row.getByTestId('stock-grams')).toHaveText(`${(before + 5000).toLocaleString('en-US')} g`);
    await signOut(page);
  });

  await signIn(page, customer.email, customer.password);

  await test.step('the customer orders and pays', async () => {
    await page.goto('/');
    const card = firstCard();
    await expect(card.getByTestId('product-name')).toHaveText(productName);
    await expect(card.getByTestId('price')).toHaveText(todayPrice);
    await card.getByRole('button', { name: 'Add to cart' }).click();
    await page.getByTestId('cart-button').click();
    await page.getByRole('button', { name: 'Checkout' }).click();
    await expect(page).toHaveURL(/\/orders\/\d+$/);
    await expect(page.getByTestId('order-status-label')).toHaveText('Pending');

    await page.getByRole('button', { name: 'Pay' }).click();
    await expect(page.getByTestId('order-status-label')).toHaveText('Paid');
  });

  await test.step('the invoice URL serves a PDF', async () => {
    const link = page.getByRole('link', { name: /^Open invoice / });
    // The invoice is generated by the outbox after payment; the button polls, and is clicked again if it gave up.
    for (let attempt = 0; attempt < 4 && !(await link.isVisible()); attempt++) {
      await page.getByRole('button', { name: 'Invoice' }).click();
      await expect(page.getByRole('button', { name: 'Invoice' })).toBeEnabled({ timeout: 40_000 });
    }
    await expect(link).toBeVisible();

    const response = await request.get((await link.getAttribute('href'))!);
    expect(response.status()).toBe(200);
    expect(response.headers()['content-type']).toContain('application/pdf');
    expect((await response.body()).subarray(0, 4).toString('latin1')).toBe('%PDF');
  });
});
