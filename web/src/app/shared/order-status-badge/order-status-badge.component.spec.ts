import { TestBed } from '@angular/core/testing';
import { page } from 'vitest/browser';
import { OrderStatusBadgeComponent } from './order-status-badge.component';

describe('OrderStatusBadgeComponent', () => {
  async function render(status: string) {
    const fixture = TestBed.createComponent(OrderStatusBadgeComponent);
    fixture.componentRef.setInput('status', status);
    await fixture.whenStable();
  }

  it.each([
    ['Pending', 'schedule'],
    ['Paid', 'paid'],
    ['Preparing', 'coffee_maker'],
    ['Shipped', 'local_shipping'],
    ['Delivered', 'check_circle'],
    ['Cancelled', 'cancel'],
  ])('shows %s as an icon plus text', async (status, icon) => {
    await render(status);

    await expect.element(page.getByTestId('order-status-label')).toHaveTextContent(status);
    await expect.element(page.getByTestId('order-status-icon')).toHaveTextContent(icon);
    await expect.element(page.getByTestId('order-status')).toHaveAttribute('data-status', status);
  });

  it('still shows the text of an unknown status', async () => {
    await render('Refunded');

    await expect.element(page.getByTestId('order-status-label')).toHaveTextContent('Refunded');
    await expect.element(page.getByTestId('order-status-icon')).toHaveTextContent('help');
  });
});
