import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { PriceBadgeComponent } from '../../../shared/price-badge/price-badge.component';

/** The estimated total and the checkout button; the server prices the order from effective prices. */
@Component({
  selector: 'app-cart-summary',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, MatProgressSpinnerModule, PriceBadgeComponent],
  template: `
    <section class="summary" aria-labelledby="summary-title" data-testid="cart-summary">
      <h2 id="summary-title">Summary</h2>
      <p class="row">
        <span>Items</span>
        <span class="tabular-nums" data-testid="summary-count">{{ count() }}</span>
      </p>
      <p class="row total">
        <span>Estimated total</span>
        <span data-testid="summary-total"><app-price-badge [amount]="total()" [currency]="currency()" /></span>
      </p>
      <p class="note" data-testid="summary-note">
        <mat-icon aria-hidden="true">info</mat-icon>
        <span>The final total is calculated from current prices when you place the order.</span>
      </p>
      <button
        mat-flat-button
        type="button"
        class="checkout"
        data-testid="checkout-button"
        [disabled]="pending() || count() === 0"
        [attr.aria-busy]="pending()"
        (click)="checkout.emit()"
      >
        @if (pending()) {
          <mat-spinner diameter="20" aria-hidden="true" />
        } @else {
          <mat-icon aria-hidden="true">shopping_bag</mat-icon>
        }
        {{ pending() ? 'Placing order…' : 'Checkout' }}
      </button>
    </section>
  `,
  styles: `
    .summary {
      display: grid;
      gap: 8px;
      padding: 16px;
      border-radius: 16px;
      background: var(--mat-sys-surface-container-low);
    }
    h2 {
      margin: 0 0 4px;
      font: var(--mat-sys-title-large);
    }
    .row {
      display: flex;
      justify-content: space-between;
      gap: 12px;
      margin: 0;
    }
    .total {
      font: var(--mat-sys-title-medium);
    }
    .note {
      display: flex;
      gap: 8px;
      margin: 0;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-medium);
    }
    .checkout {
      width: 100%;
      min-height: 48px;
      margin-top: 8px;
    }
    mat-spinner {
      display: inline-block;
      margin-inline-end: 8px;
    }
  `,
})
export class CartSummaryComponent {
  readonly count = input.required<number>();
  readonly total = input.required<number>();
  readonly currency = input.required<string>();
  readonly pending = input(false);
  readonly checkout = output<void>();
}
