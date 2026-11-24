import { TestBed } from '@angular/core/testing';
import { page } from 'vitest/browser';
import { CartLine, MAX_QTY } from '../../../core/cart/cart.service';
import { CartLineComponent } from './cart-line.component';

const LINE: CartLine = {
  skuId: 11,
  productId: 1,
  name: 'Ethiopia Yirgacheffe',
  roast: 'Light',
  grind: 'WholeBean',
  sizeG: 500,
  unitPrice: 300,
  currency: 'MXN',
  qty: 2,
};

describe('CartLineComponent', () => {
  async function render(line: CartLine) {
    const fixture = TestBed.createComponent(CartLineComponent);
    fixture.componentRef.setInput('line', line);
    const qtyChange = vi.fn();
    const remove = vi.fn();
    fixture.componentInstance.qtyChange.subscribe(qtyChange);
    fixture.componentInstance.remove.subscribe(remove);
    await fixture.whenStable();
    return { fixture, qtyChange, remove };
  }

  it('shows the item, its variant and the estimated line total', async () => {
    await render(LINE);

    await expect.element(page.getByRole('heading', { name: 'Ethiopia Yirgacheffe' })).toBeVisible();
    await expect.element(page.getByTestId('cart-line-variant')).toHaveTextContent('Whole bean · 500 g');
    await expect.element(page.getByTestId('cart-line-total')).toMatchTextContent(/600\.00/);
  });

  it('emits quantity changes from the stepper', async () => {
    const { qtyChange } = await render(LINE);

    await page.getByRole('button', { name: 'Increase quantity' }).click();

    expect(qtyChange).toHaveBeenCalledExactlyOnceWith(3);
  });

  it('cannot go below 1 or above 50', async () => {
    const { fixture } = await render({ ...LINE, qty: 1 });
    await expect.element(page.getByRole('button', { name: 'Decrease quantity' })).toBeDisabled();

    fixture.componentRef.setInput('line', { ...LINE, qty: MAX_QTY });
    await fixture.whenStable();

    await expect.element(page.getByRole('button', { name: 'Increase quantity' })).toBeDisabled();
    await expect.element(page.getByRole('button', { name: 'Decrease quantity' })).toBeEnabled();
  });

  it('removes the line', async () => {
    const { remove } = await render(LINE);

    await page.getByRole('button', { name: 'Remove Ethiopia Yirgacheffe from the cart' }).click();

    expect(remove).toHaveBeenCalledOnce();
  });
});
