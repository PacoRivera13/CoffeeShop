import { TestBed } from '@angular/core/testing';
import { page } from 'vitest/browser';
import { priceHistory } from '../../../../../testing/fixtures';
import { PriceHistoryCardsComponent } from './price-history-cards.component';

describe('PriceHistoryCardsComponent', () => {
  beforeEach(async () => {
    const fixture = TestBed.createComponent(PriceHistoryCardsComponent);
    fixture.componentRef.setInput('history', priceHistory);
    await fixture.whenStable();
  });

  it('stacks the prices as cards, newest first', async () => {
    const cards = page.getByTestId('price-card');
    expect(cards.all()).toHaveLength(3);

    await expect.element(cards.nth(0)).toMatchTextContent(/320\.00/);
    await expect.element(cards.nth(0).getByTestId('price-effective-from')).toMatchTextContent(/Jan 1, 2027/);
    await expect.element(cards.nth(2)).toMatchTextContent(/280\.00/);
  });

  it('marks only the current price, with an icon and text', async () => {
    await expect.element(page.getByTestId('price-card').nth(1).getByTestId('current-chip')).toMatchTextContent(
      /check\s*Current/,
    );
    expect(page.getByTestId('current-chip').all()).toHaveLength(1);
  });
});
