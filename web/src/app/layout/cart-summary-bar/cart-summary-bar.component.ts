import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { PriceBadgeComponent } from '../../shared/price-badge/price-badge.component';

/** Compact screens: a bar fixed to the bottom with the cart size and estimated total, linking to the cart. */
@Component({
  selector: 'app-cart-summary-bar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule, PriceBadgeComponent, RouterLink],
  template: `
    <a class="bar" routerLink="/cart" data-testid="cart-summary-bar">
      <mat-icon aria-hidden="true">shopping_cart</mat-icon>
      <span class="text">
        Cart · <span data-testid="cart-bar-count">{{ itemsLabel() }}</span> · est.
        <app-price-badge [amount]="total()" [currency]="currency()" />
      </span>
      <mat-icon class="chevron" aria-hidden="true">chevron_right</mat-icon>
    </a>
  `,
  styles: `
    :host {
      position: fixed;
      inset: auto 0 0 0;
      z-index: 10;
      padding: 8px 12px calc(8px + env(safe-area-inset-bottom));
      background: var(--mat-sys-surface-container);
      box-shadow: var(--mat-sys-level2);
    }
    .bar {
      display: flex;
      align-items: center;
      gap: 12px;
      min-height: 48px;
      padding: 0 16px;
      border-radius: 24px;
      background: var(--mat-sys-primary);
      color: var(--mat-sys-on-primary);
      text-decoration: none;
      font: var(--mat-sys-label-large);
    }
    .text {
      flex: 1 1 auto;
      min-width: 0;
    }
  `,
})
export class CartSummaryBarComponent {
  readonly count = input.required<number>();
  readonly total = input.required<number>();
  readonly currency = input.required<string>();

  protected readonly itemsLabel = computed(() => `${this.count()} ${this.count() === 1 ? 'item' : 'items'}`);
}
