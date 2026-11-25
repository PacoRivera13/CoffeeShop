import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { page, userEvent } from 'vitest/browser';
import { AdminMenuComponent } from './admin-menu.component';

describe('AdminMenuComponent', () => {
  beforeEach(async () => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(AdminMenuComponent);
    fixture.componentRef.setInput('links', [
      { label: 'Prices', path: '/admin/prices' },
      { label: 'Inventory', path: '/admin/inventory' },
    ]);
    await fixture.whenStable();
  });

  it('opens a menu with the admin pages', async () => {
    await page.getByRole('button', { name: 'Admin' }).click();

    await expect.element(page.getByRole('menuitem', { name: 'Prices' })).toHaveAttribute('href', '/admin/prices');
    await expect.element(page.getByRole('menuitem', { name: 'Inventory' })).toHaveAttribute('href', '/admin/inventory');
  });

  it('closes on Escape', async () => {
    await page.getByRole('button', { name: 'Admin' }).click();
    await expect.element(page.getByRole('menu')).toBeVisible();

    await userEvent.keyboard('{Escape}');

    await expect.element(page.getByRole('menu')).not.toBeInTheDocument();
  });
});
