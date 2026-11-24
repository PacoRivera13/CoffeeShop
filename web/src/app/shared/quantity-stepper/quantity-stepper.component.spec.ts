import { TestBed } from '@angular/core/testing';
import { page } from 'vitest/browser';
import { QuantityStepperComponent } from './quantity-stepper.component';

describe('QuantityStepperComponent', () => {
  async function render(value: number) {
    const fixture = TestBed.createComponent(QuantityStepperComponent);
    fixture.componentRef.setInput('value', value);
    const valueChange = vi.fn();
    fixture.componentInstance.valueChange.subscribe(valueChange);
    await fixture.whenStable();
    return { fixture, valueChange };
  }

  const increase = () => page.getByRole('button', { name: 'Increase quantity' });
  const decrease = () => page.getByRole('button', { name: 'Decrease quantity' });

  it('shows the value and emits each change', async () => {
    const { fixture, valueChange } = await render(3);

    await increase().click();
    await fixture.whenStable();
    await decrease().click();
    await decrease().click();
    await fixture.whenStable();

    await expect.element(page.getByTestId('quantity-value')).toHaveTextContent('2');
    expect(valueChange.mock.calls).toEqual([[4], [3], [2]]);
  });

  it('cannot go below 1', async () => {
    const { valueChange } = await render(1);

    await expect.element(decrease()).toBeDisabled();
    await expect.element(increase()).toBeEnabled();
    expect(valueChange).not.toHaveBeenCalled();
  });

  it('cannot go above 50', async () => {
    const { fixture, valueChange } = await render(49);

    await increase().click();
    await fixture.whenStable();

    await expect.element(page.getByTestId('quantity-value')).toHaveTextContent('50');
    await expect.element(increase()).toBeDisabled();
    expect(valueChange).toHaveBeenCalledExactlyOnceWith(50);
  });

  it('clamps an out-of-range input value', async () => {
    await render(500);

    await expect.element(page.getByTestId('quantity-value')).toHaveTextContent('50');
  });

  it('labels the group for assistive technology', async () => {
    await render(1);

    await expect.element(page.getByRole('group', { name: 'Quantity' })).toBeVisible();
  });
});
