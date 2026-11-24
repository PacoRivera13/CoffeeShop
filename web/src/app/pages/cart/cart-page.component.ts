import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { Router, RouterLink } from '@angular/router';
import { CartService } from '../../core/cart/cart.service';
import { CheckoutService } from '../../core/orders/checkout.service';
import { CartLineComponent } from '../../features/cart/cart-line/cart-line.component';
import { CartSummaryComponent } from '../../features/cart/cart-summary/cart-summary.component';
import { EmptyStateComponent } from '../../shared/empty-state/empty-state.component';
import { ErrorBannerComponent } from '../../shared/error-banner/error-banner.component';
import { PageHeaderComponent } from '../../shared/page-header/page-header.component';

@Component({
  selector: 'app-cart-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CartLineComponent,
    CartSummaryComponent,
    EmptyStateComponent,
    ErrorBannerComponent,
    MatButtonModule,
    MatSnackBarModule,
    PageHeaderComponent,
    RouterLink,
  ],
  template: `
    <app-page-header title="Cart" />
    @if (cart.lines().length === 0) {
      <app-empty-state icon="shopping_cart" title="Your cart is empty" message="Pick a roast you like and add it here.">
        <a mat-flat-button routerLink="/">Browse coffee</a>
      </app-empty-state>
    } @else {
      <app-error-banner class="error" [error]="error()" />
      <div class="layout">
        <ul class="lines" aria-label="Cart items">
          @for (line of cart.lines(); track line.skuId) {
            <li>
              <app-cart-line
                [line]="line"
                [disabled]="pending()"
                (qtyChange)="cart.setQty(line.skuId, $event)"
                (remove)="cart.remove(line.skuId)"
              />
            </li>
          }
        </ul>
        <app-cart-summary
          class="summary"
          [count]="cart.count()"
          [total]="cart.estimatedTotal()"
          [currency]="cart.currency() ?? ''"
          [pending]="pending()"
          (checkout)="placeOrder()"
        />
      </div>
    }
  `,
  styles: `
    @use 'styles/breakpoints' as bp;
    .error {
      display: block;
      margin-bottom: 16px;
    }
    .layout {
      display: grid;
      gap: 16px;
      @include bp.expanded {
        grid-template-columns: minmax(0, 1fr) 320px;
        align-items: start;
        gap: 32px;
      }
    }
    .lines {
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .lines li + li {
      border-top: 1px solid var(--mat-sys-outline-variant);
    }
    .summary {
      @include bp.expanded {
        position: sticky;
        top: 16px;
      }
    }
  `,
})
export class CartPageComponent {
  protected readonly cart = inject(CartService);
  private readonly checkout = inject(CheckoutService);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly pending = signal(false);
  protected readonly error = signal<unknown>(null);

  protected placeOrder(): void {
    if (this.pending()) return;
    this.pending.set(true);
    this.error.set(null);
    this.checkout
      .placeOrder()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (order) => {
          this.pending.set(false);
          this.snackBar.open(`Order #${order.id} placed`, undefined, { duration: 4000 });
          void this.router.navigate(['/orders', order.id]);
        },
        error: (error: unknown) => {
          this.pending.set(false);
          this.error.set(this.describe(error));
        },
      });
  }

  /** A stock conflict names the product and the grams left; anything else is shown as the API sent it. */
  private describe(error: unknown): unknown {
    if (!(error instanceof HttpErrorResponse) || error.status !== 409) return error;
    const body = error.error as { productId?: unknown; availableG?: unknown; requiredG?: unknown } | null;
    if (typeof body?.productId !== 'number' || typeof body.availableG !== 'number') return error;
    const productId = body.productId;
    const name = this.cart.lines().find((line) => line.productId === productId)?.name ?? `Product ${productId}`;
    const needed = typeof body.requiredG === 'number' ? ` Your cart needs ${body.requiredG} g.` : '';
    return {
      status: 409,
      title: 'Not enough stock',
      detail: `${name}: only ${body.availableG} g available.${needed} Reduce the quantity and try again.`,
    };
  }
}
