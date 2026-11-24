import { TestBed } from '@angular/core/testing';
import { page } from 'vitest/browser';
import { THEME_STORAGE_KEY } from '../../core/theme/theme.service';
import { ThemeToggleComponent } from './theme-toggle.component';

describe('ThemeToggleComponent', () => {
  afterEach(() => {
    localStorage.removeItem(THEME_STORAGE_KEY);
    document.documentElement.removeAttribute('data-theme');
    vi.restoreAllMocks();
  });

  async function render() {
    const fixture = TestBed.createComponent(ThemeToggleComponent);
    await fixture.whenStable();
    return fixture;
  }

  it('follows the system until the user chooses', async () => {
    await render();

    expect(document.documentElement.hasAttribute('data-theme')).toBe(false);
    await expect.element(page.getByTestId('theme-toggle')).toHaveAccessibleName(/^Switch to (light|dark) theme$/);
  });

  it('switches to the opposite scheme and persists the choice', async () => {
    const fixture = await render();
    const toggle = page.getByTestId('theme-toggle');
    const wasDark = (await toggle.element().getAttribute('aria-label')) === 'Switch to light theme';

    await toggle.click();
    await fixture.whenStable();

    const expected = wasDark ? 'light' : 'dark';
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe(expected);
    expect(document.documentElement.getAttribute('data-theme')).toBe(expected);
    await expect.element(toggle).toHaveAccessibleName(wasDark ? 'Switch to dark theme' : 'Switch to light theme');
  });

  it('restores the persisted choice on the next visit', async () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'dark');

    await render();

    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
    await expect.element(page.getByTestId('theme-toggle')).toHaveAccessibleName('Switch to light theme');
  });

  it('still switches when storage is unavailable', async () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new DOMException('Blocked', 'SecurityError');
    });
    const fixture = await render();
    const before = await page.getByTestId('theme-toggle').element().getAttribute('aria-label');

    await page.getByTestId('theme-toggle').click();
    await fixture.whenStable();

    await expect.element(page.getByTestId('theme-toggle')).not.toHaveAccessibleName(before!);
  });
});
