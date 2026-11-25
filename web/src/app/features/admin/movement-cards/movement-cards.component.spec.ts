import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { page } from 'vitest/browser';
import { movement } from '../../../../../testing/fixtures';
import { MovementCardsComponent } from './movement-cards.component';

describe('MovementCardsComponent', () => {
  beforeEach(async () => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(MovementCardsComponent);
    fixture.componentRef.setInput('movements', [
      movement({ id: 2, deltaG: -1000, reason: 'Reserve', orderId: 7 }),
      movement({ id: 1, deltaG: 20000, reason: 'Restock' }),
    ]);
    await fixture.whenStable();
  });

  it('shows the same ledger data as cards', async () => {
    const cards = page.getByTestId('movement-card');
    expect(cards.all()).toHaveLength(2);

    await expect.element(cards.nth(0).getByTestId('movement-delta')).toHaveTextContent('−1,000 g');
    await expect.element(cards.nth(0).getByTestId('movement-reason')).toMatchTextContent(/Reserved for order/);
    await expect.element(cards.nth(0).getByRole('link', { name: 'Order #7' })).toHaveAttribute('href', '/orders/7');
    await expect.element(cards.nth(1).getByTestId('movement-delta')).toHaveTextContent('+20,000 g');
    await expect.element(cards.nth(1).getByTestId('movement-order')).not.toBeInTheDocument();
  });
});
