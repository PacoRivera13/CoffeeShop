import { TestBed } from '@angular/core/testing';
import { page } from 'vitest/browser';
import { priceHistory } from '../../../../../testing/fixtures';
import { PriceHistoryTableComponent } from './price-history-table.component';

describe('PriceHistoryTableComponent', () => {
  beforeEach(async () => {
    const fixture = TestBed.createComponent(PriceHistoryTableComponent);
    fixture.componentRef.setInput('history', priceHistory);
    await fixture.whenStable();
  });

  it('lists prices newest first', async () => {
    await expect.element(page.getByRole('table', { name: 'Price history' })).toBeVisible();
    // Intl puts a narrow no-break space before AM; compare with plain spaces.
    const dates = page
      .getByTestId('price-effective-from')
      .all()
      .map((cell) => cell.element().textContent!.replace(/\s+/gu, ' ').trim());

    expect(dates).toEqual(['Jan 1, 2027, 12:00:00 AM', 'Jun 1, 2026, 12:00:00 AM', 'Jan 1, 2026, 12:00:00 AM']);
  });

  it('marks only the current price, with an icon and text', async () => {
    const rows = page.getByTestId('price-row');

    await expect.element(rows.nth(1).getByTestId('current-chip')).toMatchTextContent(/check\s*Current/);
    await expect.element(rows.nth(1)).toMatchTextContent(/300\.00/);
    expect(page.getByTestId('current-chip').all()).toHaveLength(1);
  });
});
