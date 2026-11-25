import { TestBed } from '@angular/core/testing';
import { page } from 'vitest/browser';
import { RestockFormComponent } from './restock-form.component';

describe('RestockFormComponent', () => {
  async function render() {
    const fixture = TestBed.createComponent(RestockFormComponent);
    fixture.componentRef.setInput('productName', 'Colombia Huila');
    const restock = vi.fn();
    fixture.componentInstance.restock.subscribe(restock);
    await fixture.whenStable();
    return { fixture, restock };
  }

  async function tryGrams(value: string) {
    await page.getByLabelText('Grams to add').fill(value);
    await page.getByRole('button', { name: 'Restock Colombia Huila' }).click();
  }

  it('requires a value', async () => {
    const { restock } = await render();

    await page.getByRole('button', { name: 'Restock Colombia Huila' }).click();

    await expect.element(page.getByText('Enter the grams to add.')).toBeVisible();
    expect(restock).not.toHaveBeenCalled();
  });

  it('rejects 0 and 1,000,001 grams', async () => {
    const { restock } = await render();

    await tryGrams('0');
    await expect.element(page.getByText('Restock between 1 and 1,000,000 g.')).toBeVisible();
    await tryGrams('1000001');
    await expect.element(page.getByText('Restock between 1 and 1,000,000 g.')).toBeVisible();

    expect(restock).not.toHaveBeenCalled();
  });

  it('rejects fractions of a gram', async () => {
    const { restock } = await render();

    await tryGrams('2.5');

    await expect.element(page.getByText('Use whole grams.')).toBeVisible();
    expect(restock).not.toHaveBeenCalled();
  });

  it('accepts the bounds 1 and 1,000,000', async () => {
    const { restock } = await render();

    await tryGrams('1');
    await tryGrams('1000000');

    expect(restock.mock.calls).toEqual([[1], [1000000]]);
  });

  it('disables the button while restocking', async () => {
    const { fixture } = await render();
    fixture.componentRef.setInput('pending', true);
    await fixture.whenStable();

    await expect.element(page.getByTestId('restock-button')).toBeDisabled();
    await expect.element(page.getByTestId('restock-button')).toMatchTextContent(/Restocking/);
  });
});
