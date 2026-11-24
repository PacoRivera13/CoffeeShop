import AxeBuilder from '@axe-core/playwright';
import { expect, type Locator, type Page, type TestInfo } from '@playwright/test';
import { fakeAccessToken, type Role } from '../../testing/fixtures';

const SESSION_STORAGE_KEY = 'roastery.session';

/** Starts the page already signed in, as the app would be after a login in this tab. */
export async function signInAs(page: Page, role: Role): Promise<void> {
  const token = fakeAccessToken(role);
  await page.addInitScript(
    ([key, value]) => sessionStorage.setItem(key, value),
    [SESSION_STORAGE_KEY, token] as const,
  );
}

/** Replies to one API call with a JSON body; every test mocks the API, nothing reaches a real server. */
export async function mockApi(page: Page, method: string, path: string, status: number, body: unknown): Promise<void> {
  await page.route(`**/api${path}`, (route) =>
    route.request().method() === method
      ? route.fulfill({ status, contentType: status >= 400 ? 'application/problem+json' : 'application/json', body: JSON.stringify(body) })
      : route.fallback(),
  );
}

export async function expectNoHorizontalOverflow(page: Page): Promise<void> {
  const { scrollWidth, clientWidth } = await page.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    clientWidth: document.documentElement.clientWidth,
  }));
  expect(scrollWidth, 'page scrolls horizontally').toBeLessThanOrEqual(clientWidth);
}

/** No serious or critical axe violations (WCAG 2 A/AA). */
export async function expectAccessible(page: Page): Promise<void> {
  const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']).analyze();
  const blocking = results.violations
    .filter((v) => v.impact === 'serious' || v.impact === 'critical')
    .map((v) => `${v.id} (${v.impact}): ${v.nodes.map((n) => n.target.join(' ')).join(', ')}`);
  expect(blocking, blocking.join('\n')).toEqual([]);
}

/**
 * Light mode on every project; dark mode on desktop too, as the milestone requires. Waits for the page to settle
 * before analysing so transitions do not skew contrast.
 */
export async function expectAccessibleInBothSchemes(page: Page, testInfo: TestInfo, ready: Locator): Promise<void> {
  await page.emulateMedia({ colorScheme: 'light', reducedMotion: 'reduce' });
  await expect(ready).toBeVisible();
  await expectAccessible(page);

  if (testInfo.project.name === 'desktop') {
    await page.emulateMedia({ colorScheme: 'dark', reducedMotion: 'reduce' });
    // The dark cream surface (#1E1612) proves the dark tokens are active before axe measures contrast.
    await expect(page.locator('body')).toHaveCSS('background-color', 'rgb(30, 22, 18)');
    await expectAccessible(page);
  }
}

export async function expectMinTouchTarget(locator: Locator, size = 44): Promise<void> {
  const box = await locator.boundingBox();
  expect(box, 'element is not rendered').not.toBeNull();
  expect(box!.width, 'touch target width').toBeGreaterThanOrEqual(size);
  expect(box!.height, 'touch target height').toBeGreaterThanOrEqual(size);
}
