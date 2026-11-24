import { TestBed } from '@angular/core/testing';
import { page } from 'vitest/browser';
import { CartSummaryComponent } from './cart-summary.component';

describe('CartSummaryComponent', () => {
  async function render(count: number, pending = false) {
    const fixture = TestBed.createComponent(CartSummaryComponent);
    fixture.componentRef.setInput('count', count);
    fixture.componentRef.setInput('total', 1060.5);
    fixture.componentRef.setInput('currency', 'MXN');
    fixture.componentRef.setInput('pending', pending);
    const checkout = vi.fn();
    fixture.componentInstance.checkout.subscribe(checkout);
    await fixture.whenStable();
    return { fixture, checkout };
  }

  it('labels the total as an estimate', async () => {
    await render(3);

    await expect.element(page.getByTestId('summary-count')).toHaveTextContent('3');
    await expect.element(page.getByTestId('summary-total')).toMatchTextContent(/1,060\.50/);
    await expect.element(page.getByText('Estimated total')).toBeVisible();
    await expect.element(page.getByTestId('summary-note')).toMatchTextContent(/final total is calculated/);
  });

  it('emits checkout', async () => {
    const { checkout } = await render(3);

    await page.getByRole('button', { name: 'Checkout' }).click();

    expect(checkout).toHaveBeenCalledOnce();
  });

  it('disables checkout while the order is being placed', async () => {
    await render(3, true);

    const button = page.getByTestId('checkout-button');
    await expect.element(button).toBeDisabled();
    await expect.element(button).toMatchTextContent(/Placing order/);
    await expect.element(button).toHaveAttribute('aria-busy', 'true');
  });

  it('disables checkout for an empty cart', async () => {
    await render(0);

    await expect.element(page.getByRole('button', { name: 'Checkout' })).toBeDisabled();
  });
});
