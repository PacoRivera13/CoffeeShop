import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { page } from 'vitest/browser';
import { order } from '../../../../../testing/fixtures';
import { OrderTableComponent } from './order-table.component';

describe('OrderTableComponent', () => {
  it('renders one row per order with link, status, items and total', async () => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(OrderTableComponent);
    fixture.componentRef.setInput('orders', [order({ id: 8, status: 'Paid' }), order({ id: 7 })]);
    await fixture.whenStable();

    await expect.element(page.getByRole('table', { name: 'Orders' })).toBeVisible();
    expect(page.getByTestId('order-row').all()).toHaveLength(2);
    await expect.element(page.getByRole('columnheader', { name: 'Total' })).toBeVisible();

    const first = page.getByTestId('order-row').first();
    await expect.element(first.getByRole('link', { name: 'Order #8' })).toHaveAttribute('href', '/orders/8');
    await expect.element(first.getByTestId('order-status-label')).toHaveTextContent('Paid');
    await expect.element(first).toMatchTextContent(/760\.00/);
    await expect.element(first.getByTestId('order-items')).toHaveTextContent('3');
  });
});
