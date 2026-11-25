import { TestBed } from '@angular/core/testing';
import { page } from 'vitest/browser';
import { InventoryResponse } from '../../../api/models/inventory-response';
import { LOW_STOCK_THRESHOLD_G } from '../admin-rules';
import { StockRowComponent } from './stock-row.component';

describe('StockRowComponent', () => {
  async function render(stockG: number, selected = false) {
    const item: InventoryResponse = { productId: 1, name: 'Ethiopia Yirgacheffe', stockG, lastMovementAt: null };
    const fixture = TestBed.createComponent(StockRowComponent);
    fixture.componentRef.setInput('item', item);
    fixture.componentRef.setInput('selected', selected);
    const choose = vi.fn();
    fixture.componentInstance.choose.subscribe(choose);
    await fixture.whenStable();
    return { fixture, choose };
  }

  it('shows the stock in grams and kilograms', async () => {
    await render(20000);

    await expect.element(page.getByTestId('stock-grams')).toHaveTextContent('20,000 g');
    await expect.element(page.getByTestId('stock-kg')).toHaveTextContent('20 kg');
  });

  it('keeps grams exact when converting to kilograms', async () => {
    await render(1250);

    await expect.element(page.getByTestId('stock-kg')).toHaveTextContent('1.25 kg');
  });

  it(`flags stock below ${LOW_STOCK_THRESHOLD_G} g with an icon and text`, async () => {
    await render(LOW_STOCK_THRESHOLD_G - 1);

    const chip = page.getByTestId('low-stock-chip');
    await expect.element(chip).toMatchTextContent(/Low stock/);
    await expect.element(chip).toMatchTextContent(/warning/);
  });

  it(`does not flag exactly ${LOW_STOCK_THRESHOLD_G} g`, async () => {
    await render(LOW_STOCK_THRESHOLD_G);

    await expect.element(page.getByTestId('low-stock-chip')).not.toBeInTheDocument();
  });

  it('emits when chosen and reflects the selection', async () => {
    const { choose } = await render(20000, true);

    const button = page.getByRole('button', { name: 'Manage Ethiopia Yirgacheffe' });
    await expect.element(button).toHaveAttribute('aria-pressed', 'true');
    await button.click();

    expect(choose).toHaveBeenCalledOnce();
  });
});
