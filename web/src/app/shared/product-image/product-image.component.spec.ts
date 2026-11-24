import { TestBed } from '@angular/core/testing';
import { page } from 'vitest/browser';
import { ProductImageComponent } from './product-image.component';

describe('ProductImageComponent', () => {
  async function render(name: string, roast: string) {
    const fixture = TestBed.createComponent(ProductImageComponent);
    fixture.componentRef.setInput('name', name);
    fixture.componentRef.setInput('roast', roast);
    await fixture.whenStable();
  }

  it('describes the product for screen readers', async () => {
    await render('Yirgacheffe', 'Light');

    await expect.element(page.getByRole('img', { name: 'Yirgacheffe, light roast coffee bag' })).toBeVisible();
  });

  it.each([
    ['Light', 'light'],
    ['Medium', 'medium'],
    ['Dark', 'dark'],
    ['Espresso', 'other'],
  ])('tints a %s roast', async (roast, key) => {
    await render('Huila', roast);

    await expect.element(page.getByTestId('product-image')).toHaveAttribute('data-roast', key);
  });

  it('uses different theme colors for each roast level', async () => {
    const fills = [];
    for (const roast of ['Light', 'Dark']) {
      TestBed.resetTestingModule();
      await render('Huila', roast);
      fills.push(getComputedStyle(page.getByTestId('product-image').element()).getPropertyValue('--roast-color'));
    }

    expect(fills[0]).not.toBe(fills[1]);
  });

  it('keeps a long name in the alternative text', async () => {
    const name = 'Finca El Paraíso Double Anaerobic Thermal Shock Geisha Lot 42';
    await render(name, 'Medium');

    await expect.element(page.getByRole('img')).toHaveAccessibleName(`${name}, medium roast coffee bag`);
  });
});
