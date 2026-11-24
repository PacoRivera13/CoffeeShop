import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { page } from 'vitest/browser';
import { CartSummaryBarComponent } from './cart-summary-bar.component';

describe('CartSummaryBarComponent', () => {
  async function render(count: number) {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(CartSummaryBarComponent);
    fixture.componentRef.setInput('count', count);
    fixture.componentRef.setInput('total', 760);
    fixture.componentRef.setInput('currency', 'MXN');
    await fixture.whenStable();
    return fixture;
  }

  it('links to the cart with the item count and estimated total', async () => {
    await render(3);

    const link = page.getByRole('link', { name: /Cart · 3 items · est\./ });
    await expect.element(link).toHaveAttribute('href', '/cart');
    await expect.element(link).toMatchTextContent(/760\.00/);
  });

  it('uses the singular for one item', async () => {
    await render(1);

    await expect.element(page.getByTestId('cart-bar-count')).toHaveTextContent('1 item');
  });
});
