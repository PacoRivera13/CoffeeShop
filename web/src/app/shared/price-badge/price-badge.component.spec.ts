import { TestBed } from '@angular/core/testing';
import { page } from 'vitest/browser';
import { PriceBadgeComponent } from './price-badge.component';

describe('PriceBadgeComponent', () => {
  async function render(amount: number, currency: string, locale?: string) {
    const fixture = TestBed.createComponent(PriceBadgeComponent);
    fixture.componentRef.setInput('amount', amount);
    fixture.componentRef.setInput('currency', currency);
    if (locale) fixture.componentRef.setInput('locale', locale);
    await fixture.whenStable();
  }

  it('formats the amount for the app locale', async () => {
    await render(280, 'MXN');

    await expect.element(page.getByTestId('price')).toHaveTextContent('MX$280.00');
  });

  it('formats the amount for an explicit locale', async () => {
    await render(1234.5, 'MXN', 'es-MX');

    await expect.element(page.getByTestId('price')).toHaveTextContent('$1,234.50');
  });

  it('uses each locale grouping and decimal separators', async () => {
    await render(1234.5, 'EUR', 'de-DE');

    // Intl uses a no-break space before the symbol; the matcher normalizes whitespace to a plain space.
    await expect.element(page.getByTestId('price')).toHaveTextContent('1.234,50 €');
  });

  it('falls back to amount and code for an unknown currency', async () => {
    await render(12, 'not-a-code');

    await expect.element(page.getByTestId('price')).toHaveTextContent('12.00 not-a-code');
  });
});
