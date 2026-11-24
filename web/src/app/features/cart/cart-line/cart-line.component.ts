import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { CartLine, MAX_QTY, MIN_QTY } from '../../../core/cart/cart.service';
import { grindLabel } from '../../../core/catalog/catalog.service';
import { PriceBadgeComponent } from '../../../shared/price-badge/price-badge.component';
import { ProductImageComponent } from '../../../shared/product-image/product-image.component';
import { QuantityStepperComponent } from '../../../shared/quantity-stepper/quantity-stepper.component';

/** One cart line: the item, its quantity (1-50), the estimated line total and a remove button. */
@Component({
  selector: 'app-cart-line',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, PriceBadgeComponent, ProductImageComponent, QuantityStepperComponent],
  template: `
    <div class="line" data-testid="cart-line">
      <app-product-image class="image" [name]="line().name" [roast]="line().roast" />
      <div class="info">
        <h3 class="name" data-testid="cart-line-name">{{ line().name }}</h3>
        <p class="variant" data-testid="cart-line-variant">{{ variant() }}</p>
        <p class="unit">
          <app-price-badge [amount]="line().unitPrice" [currency]="line().currency" /> each
        </p>
      </div>
      <app-quantity-stepper
        class="qty"
        [value]="line().qty"
        [min]="minQty"
        [max]="maxQty"
        [disabled]="disabled()"
        (valueChange)="qtyChange.emit($event)"
      />
      <div class="total" data-testid="cart-line-total">
        <app-price-badge [amount]="lineTotal()" [currency]="line().currency" />
      </div>
      <button
        mat-icon-button
        type="button"
        class="remove"
        data-testid="cart-line-remove"
        [disabled]="disabled()"
        [attr.aria-label]="'Remove ' + line().name + ' from the cart'"
        (click)="remove.emit()"
      >
        <mat-icon aria-hidden="true">delete</mat-icon>
      </button>
    </div>
  `,
  styles: `
    .line {
      display: grid;
      grid-template-columns: 56px minmax(0, 1fr) auto;
      grid-template-areas:
        'image info remove'
        'qty qty total';
      align-items: center;
      gap: 8px 12px;
      padding: 12px 0;
    }
    .image {
      grid-area: image;
      width: 56px;
    }
    .info {
      grid-area: info;
      min-width: 0;
      overflow-wrap: anywhere;
    }
    .qty {
      grid-area: qty;
    }
    .total {
      grid-area: total;
      justify-self: end;
    }
    .remove {
      grid-area: remove;
    }
    .name {
      margin: 0;
      font: var(--mat-sys-title-medium);
    }
    .variant,
    .unit {
      margin: 2px 0 0;
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class CartLineComponent {
  readonly line = input.required<CartLine>();
  /** True while the cart is being checked out: the order already holds these quantities. */
  readonly disabled = input(false);
  readonly qtyChange = output<number>();
  readonly remove = output<void>();

  protected readonly minQty = MIN_QTY;
  protected readonly maxQty = MAX_QTY;
  protected readonly variant = computed(() => `${grindLabel(this.line().grind)} · ${this.line().sizeG} g`);
  protected readonly lineTotal = computed(() => Math.round(this.line().unitPrice * this.line().qty * 100) / 100);
}
