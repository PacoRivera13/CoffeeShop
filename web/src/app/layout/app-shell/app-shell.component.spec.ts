import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { page } from 'vitest/browser';
import { AuthService } from '../../core/auth/auth.service';
import { AppShellComponent } from './app-shell.component';

type Session = 'anonymous' | 'customer' | 'admin';

function fakeAuth(session: Session) {
  return {
    isAuthenticated: signal(session !== 'anonymous'),
    isAdmin: signal(session === 'admin'),
    email: signal(session === 'anonymous' ? null : `${session}@example.test`),
    logout: vi.fn(),
  };
}

describe('AppShellComponent', () => {
  async function render(session: Session, width: number, cartCount = 0) {
    await page.viewport(width, 800);
    const auth = fakeAuth(session);
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthService, useValue: auth }],
    });
    const fixture = TestBed.createComponent(AppShellComponent);
    fixture.componentRef.setInput('cartCount', cartCount);
    await fixture.whenStable();
    return { fixture, auth };
  }

  it('on compact screens opens a drawer from the menu button', async () => {
    const { fixture } = await render('customer', 360);

    await expect.element(page.getByTestId('inline-nav')).not.toBeInTheDocument();
    await page.getByRole('button', { name: 'Open navigation' }).click();
    await fixture.whenStable();

    await expect.element(page.getByRole('link', { name: 'Shop' })).toBeVisible();
  });

  it('closes the drawer when the screen widens to the expanded layout', async () => {
    const { fixture } = await render('customer', 360);
    await page.getByRole('button', { name: 'Open navigation' }).click();
    await fixture.whenStable();
    await expect.element(page.getByTestId('drawer-link')).toBeVisible();

    await page.viewport(1280, 800);
    await fixture.whenStable();

    await expect.element(page.getByTestId('drawer-link')).not.toBeVisible();
    await expect.element(page.getByTestId('inline-nav')).toBeVisible();
  });

  it('on expanded screens shows inline navigation and no menu button', async () => {
    await render('customer', 1280);

    await expect.element(page.getByTestId('inline-nav')).toBeVisible();
    await expect.element(page.getByTestId('menu-button')).not.toBeInTheDocument();
    await expect.element(page.getByTestId('nav-link')).toHaveTextContent('Shop');
  });

  it('shows sign-in links to anonymous visitors and hides account actions', async () => {
    await render('anonymous', 1280);

    await expect.element(page.getByRole('link', { name: 'Sign in' })).toBeVisible();
    await expect.element(page.getByRole('link', { name: 'Create account' })).toBeVisible();
    await expect.element(page.getByTestId('logout-button')).not.toBeInTheDocument();
    await expect.element(page.getByTestId('cart-button')).not.toBeInTheDocument();
  });

  it('adds the admin link for admins', async () => {
    await render('admin', 1280);

    await expect.element(page.getByRole('link', { name: 'Admin' })).toBeVisible();
  });

  it('labels the cart with its item count', async () => {
    await render('customer', 1280, 3);

    await expect.element(page.getByTestId('cart-button')).toHaveAccessibleName('Cart, 3 items');
  });

  it('offers logout and the theme toggle on both layouts', async () => {
    for (const width of [360, 1280]) {
      TestBed.resetTestingModule();
      await render('customer', width);

      await expect.element(page.getByTestId('logout-button')).toBeVisible();
      await expect.element(page.getByTestId('theme-toggle')).toBeVisible();
    }
  });

  it('signs out and returns to the login page', async () => {
    const { fixture, auth } = await render('customer', 1280);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    await page.getByRole('button', { name: 'Sign out' }).click();
    await fixture.whenStable();

    expect(auth.logout).toHaveBeenCalledOnce();
    expect(navigate).toHaveBeenCalledWith(['/login']);
  });
});
