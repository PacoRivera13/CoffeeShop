import { expect, test } from '@playwright/test';
import { expectMinTouchTarget, expectNoHorizontalOverflow, signInAs } from '../support/helpers';

test.describe('App shell', () => {
  test.beforeEach(async ({ page }) => {
    await signInAs(page, 'Admin');
    await page.goto('/');
    await expect(page.getByRole('heading', { level: 1, name: 'Shop' })).toBeVisible();
  });

  test('navigation matches the screen size', async ({ page }, testInfo) => {
    if (testInfo.project.name === 'mobile') {
      await expect(page.getByTestId('inline-nav')).toHaveCount(0);
      await page.getByRole('button', { name: 'Open navigation' }).click();

      const drawer = page.getByRole('navigation', { name: 'Main menu' });
      await expect(drawer.getByRole('link', { name: 'Admin' })).toBeVisible();
      await drawer.getByRole('link', { name: 'Admin' }).click();
      await expect(page).toHaveURL('/admin');
    } else {
      await expect(page.getByRole('button', { name: 'Open navigation' })).toHaveCount(0);
      const nav = page.getByRole('navigation', { name: 'Main', exact: true });
      await expect(nav.getByRole('link', { name: 'Shop' })).toBeVisible();
      await expect(nav.getByRole('link', { name: 'Admin' })).toBeVisible();
    }
  });

  test('logout and the theme toggle are always reachable', async ({ page }) => {
    await expect(page.getByRole('button', { name: 'Sign out' })).toBeVisible();
    await expect(page.getByRole('button', { name: /Switch to (light|dark) theme/ })).toBeVisible();

    await page.getByRole('button', { name: 'Sign out' }).click();
    await expect(page).toHaveURL('/login');
  });

  test('touch targets are at least 44x44 on mobile', async ({ page }, testInfo) => {
    const targets = [
      page.getByRole('button', { name: /Switch to (light|dark) theme/ }),
      page.getByRole('button', { name: 'Sign out' }),
    ];
    if (testInfo.project.name === 'mobile') {
      targets.push(page.getByRole('button', { name: 'Open navigation' }));
      await page.getByRole('button', { name: 'Open navigation' }).click();
      const drawer = page.getByRole('navigation', { name: 'Main menu' });
      await expect(drawer.getByRole('link', { name: 'Shop' })).toBeVisible();
      targets.push(drawer.getByRole('link', { name: 'Shop' }), drawer.getByRole('link', { name: 'Admin' }));

      for (const target of targets) await expectMinTouchTarget(target);

      await page.keyboard.press('Escape');
      await page.goto('/login');
      await expectMinTouchTarget(page.getByRole('button', { name: 'Sign in' }));
    } else {
      for (const target of targets) await expect(target).toBeVisible();
    }
  });

  test('has no horizontal overflow', async ({ page }) => {
    await expectNoHorizontalOverflow(page);
  });
});
