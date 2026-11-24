import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { page } from 'vitest/browser';
import { order } from '../../../../../testing/fixtures';
import { OrderCardListComponent } from './order-card-list.component';

describe('OrderCardListComponent', () => {
  it('renders the same order data as stacked cards', async () => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(OrderCardListComponent);
    fixture.componentRef.setInput('orders', [
      order({ id: 8, status: 'Shipped' }),
      order({ id: 7, lines: [{ skuId: 11, qty: 1, unitPrice: 300, priceId: 1100, lineTotal: 300 }], total: 300 }),
    ]);
    await fixture.whenStable();

    expect(page.getByTestId('order-card').all()).toHaveLength(2);
    const first = page.getByTestId('order-card').first();
    await expect.element(first.getByRole('link', { name: /Order #8/ })).toHaveAttribute('href', '/orders/8');
    await expect.element(first.getByTestId('order-status-label')).toHaveTextContent('Shipped');
    await expect.element(first).toMatchTextContent(/3 items/);
    await expect.element(page.getByTestId('order-card').nth(1)).toMatchTextContent(/1 item(?!s)/);
  });
});
