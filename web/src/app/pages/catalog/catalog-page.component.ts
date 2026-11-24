import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { CartItem, CartService, MAX_QTY } from '../../core/cart/cart.service';
import { CatalogService } from '../../core/catalog/catalog.service';
import { ProductCardComponent } from '../../features/catalog/product-card/product-card.component';
import { EmptyStateComponent } from '../../shared/empty-state/empty-state.component';
import { ErrorBannerComponent } from '../../shared/error-banner/error-banner.component';

@Component({
  selector: 'app-catalog-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [EmptyStateComponent, ErrorBannerComponent, MatProgressSpinnerModule, MatSnackBarModule, ProductCardComponent],
  template: `
    <section class="hero" aria-labelledby="catalog-title">
      <h1 id="catalog-title">Shop</h1>
      <p class="tagline">Single-origin beans, roasted in small batches and sold by the gram.</p>
    </section>
    <app-error-banner [error]="products.error() ?? null" />
    @if (products.isLoading()) {
      <div class="loading"><mat-spinner diameter="40" aria-label="Loading products" /></div>
    } @else if (products.hasValue()) {
      @if (products.value().length === 0) {
        <app-empty-state icon="coffee" title="No coffee yet" message="New roasts will appear here soon." />
      } @else {
        <ul class="grid" aria-label="Products">
          @for (product of products.value(); track product.id) {
            <li><app-product-card [product]="product" [asOf]="asOf()" (added)="addToCart($event)" /></li>
          }
        </ul>
      }
    }
  `,
  styles: `
    .hero {
      margin-bottom: 24px;
      padding: 24px;
      border-radius: 24px;
      background: var(--mat-sys-primary-container);
      color: var(--mat-sys-on-primary-container);
      overflow-wrap: anywhere;
    }
    h1 {
      margin: 0;
      font: var(--mat-sys-display-small);
      font-family: var(--mat-sys-headline-medium-font);
      color: inherit;
    }
    .tagline {
      margin: 8px 0 0;
      font: var(--mat-sys-body-large);
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(min(280px, 100%), 1fr));
      gap: 16px;
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .loading {
      display: flex;
      justify-content: center;
      padding: 32px;
    }
  `,
})
export class CatalogPageComponent {
  private readonly cart = inject(CartService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly catalog = inject(CatalogService);

  /** Optional ?asOf= query parameter: shows prices as of that instant. */
  readonly asOf = input<string | undefined>(undefined);

  protected readonly products = rxResource({ stream: () => this.catalog.products() });

  protected addToCart(item: CartItem): void {
    const undo = this.cart.add(item);
    if (undo === null) {
      this.snackBar.open(`${item.name} is already at the maximum of ${MAX_QTY} in your cart`, undefined, {
        duration: 5000,
      });
      return;
    }
    this.snackBar
      .open(`${item.name} added to cart`, 'Undo', { duration: 5000 })
      .onAction()
      .subscribe(() => undo());
  }
}
