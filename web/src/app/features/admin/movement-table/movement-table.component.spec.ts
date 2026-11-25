import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { page } from 'vitest/browser';
import { movement } from '../../../../../testing/fixtures';
import { MovementTableComponent } from './movement-table.component';

describe('MovementTableComponent', () => {
  beforeEach(async () => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(MovementTableComponent);
    fixture.componentRef.setInput('movements', [
      movement({ id: 3, deltaG: -1000, reason: 'Reserve', orderId: 7 }),
      movement({ id: 2, deltaG: 500, reason: 'Release', orderId: 6 }),
      movement({ id: 1, deltaG: 20000, reason: 'Restock' }),
    ]);
    await fixture.whenStable();
  });

  it('shows each movement with a signed delta, an icon and a label', async () => {
    const rows = page.getByTestId('movement-row');
    expect(rows.all()).toHaveLength(3);

    await expect.element(rows.nth(0).getByTestId('movement-delta')).toHaveTextContent('−1,000 g');
    await expect.element(rows.nth(0).getByTestId('movement-reason')).toMatchTextContent(/shopping_bag\s*Reserved for order/);
    await expect.element(rows.nth(1).getByTestId('movement-delta')).toHaveTextContent('+500 g');
    await expect.element(rows.nth(1).getByTestId('movement-reason')).toMatchTextContent(/Released from order/);
    await expect.element(rows.nth(2).getByTestId('movement-delta')).toHaveTextContent('+20,000 g');
    await expect.element(rows.nth(2).getByTestId('movement-reason')).toMatchTextContent(/Restock/);
  });

  it('links order movements to their order only', async () => {
    const rows = page.getByTestId('movement-row');

    await expect.element(rows.nth(0).getByRole('link', { name: 'Order #7' })).toHaveAttribute('href', '/orders/7');
    await expect.element(rows.nth(2).getByTestId('movement-order')).not.toBeInTheDocument();
  });
});
