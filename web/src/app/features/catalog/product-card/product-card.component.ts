import { ChangeDetectionStrategy, Component, computed, inject, input, linkedSignal, output } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router, RouterLink } from '@angular/router';
import { catchError, combineLatest, map, Observable, of, startWith, switchMap } from 'rxjs';
import { getSkuPrice } from '../../../api/fn/catalog/get-sku-price';
import { PriceResponse } from '../../../api/models/price-response';
import { ProductResponse } from '../../../api/models/product-response';
import { SkuResponse } from '../../../api/models/sku-response';
import { CartItem } from '../../../core/cart/cart.service';
import { ApiRx } from '../../../core/http/api-rx.service';
import { PriceBadgeComponent } from '../../../shared/price-badge/price-badge.component';
import { ProductImageComponent } from '../../../shared/product-image/product-image.component';
import { SkuPickerComponent } from '../../../shared/sku-picker/sku-picker.component';

type PriceState =
  | { status: 'none' }
  | { status: 'loading' }
  | { status: 'ready'; price: PriceResponse }
  | { status: 'unavailable' };

let nextId = 0;

/** A product with its SKU picker and the current price of the chosen SKU. */
@Component({
  selector: 'app-product-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatButtonModule,
    MatCardModule,
    MatChipsModule,
    MatIconModule,
    MatProgressBarModule,
    PriceBadgeComponent,
    ProductImageComponent,
    RouterLink,
    SkuPickerComponent,
  ],
  template: `
    <mat-card appearance="raised" class="card" role="article" data-testid="product-card" [attr.aria-labelledby]="headingId">
      <app-product-image class="image" [name]="product().name" [roast]="product().roast" />
      <mat-card-content class="content">
        <h2 class="name" data-testid="product-name" [id]="headingId">{{ product().name }}</h2>
        <p class="origin">
          <mat-icon aria-hidden="true">public</mat-icon>
          <span data-testid="product-origin">{{ product().origin }}</span>
        </p>
        <mat-chip-set aria-label="Roast">
          <mat-chip disableRipple data-testid="roast-chip">
            <mat-icon matChipAvatar aria-hidden="true">local_fire_department</mat-icon>
            {{ product().roast }} roast
          </mat-chip>
        </mat-chip-set>
        <app-sku-picker [skus]="product().skus" [selectedId]="selectedSkuId()" (skuChange)="selectedSku.set($event)" />
        <div class="price" data-testid="price-slot">
          @switch (price().status) {
            @case ('loading') {
              <mat-progress-bar mode="indeterminate" aria-label="Loading price" data-testid="price-loading" />
            }
            @case ('ready') {
              <app-price-badge [amount]="readyAmount()" [currency]="readyCurrency()" />
            }
            @case ('unavailable') {
              <span class="muted" data-testid="price-unavailable">
                <mat-icon aria-hidden="true">info</mat-icon> Price unavailable
              </span>
            }
            @default {
              @if (product().skus.length > 0) {
                <span class="muted" data-testid="price-none">Choose a grind and size</span>
              }
            }
          }
        </div>
      </mat-card-content>
      <mat-card-actions class="actions">
        @if (canBuy()) {
          <button mat-flat-button type="button" data-testid="add-to-cart" [disabled]="!canAdd()" (click)="add()">
            <mat-icon aria-hidden="true">add_shopping_cart</mat-icon>
            Add to cart
          </button>
        } @else {
          <a mat-stroked-button data-testid="sign-in-to-buy" routerLink="/login" [queryParams]="{ returnUrl: router.url }">
            <mat-icon aria-hidden="true">login</mat-icon>
            Sign in to buy
          </a>
        }
      </mat-card-actions>
    </mat-card>
  `,
  styles: `
    :host {
      display: block;
      min-width: 0;
    }
    .card {
      height: 100%;
      display: flex;
      flex-direction: column;
    }
    .image {
      width: 112px;
      margin: 16px auto 0;
    }
    .content {
      display: grid;
      gap: 12px;
      flex: 1 1 auto;
      overflow-wrap: anywhere;
    }
    .name {
      margin: 8px 0 0;
      font: var(--mat-sys-title-large);
      font-family: var(--mat-sys-headline-medium-font);
    }
    .origin,
    .muted {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      margin: 0;
      color: var(--mat-sys-on-surface-variant);
    }
    .price {
      min-height: 32px;
      display: flex;
      align-items: center;
    }
    mat-progress-bar {
      max-width: 120px;
    }
    .actions {
      padding: 8px 16px 16px;
    }
    .actions button,
    .actions a {
      width: 100%;
      min-height: 44px;
    }
  `,
})
export class ProductCardComponent {
  private readonly api = inject(ApiRx);
  /** The current URL (with ?asOf=) is where "Sign in to buy" returns to. */
  protected readonly router = inject(Router);

  readonly product = input.required<ProductResponse>();
  /** False for anonymous visitors: the card offers "Sign in to buy" instead of "Add to cart". */
  readonly canBuy = input(true);
  /** Prices as of this instant (ISO 8601); omitted means now. */
  readonly asOf = input<string | undefined>(undefined);
  readonly added = output<CartItem>();

  protected readonly headingId = `product-${nextId++}`;
  protected readonly selectedSku = linkedSignal<SkuResponse | null>(() => this.product().skus[0] ?? null);
  protected readonly selectedSkuId = computed(() => {
    const sku = this.selectedSku();
    return sku ? Number(sku.id) : null;
  });

  // switchMap drops the request for a SKU the user has already moved away from.
  protected readonly price = toSignal(
    combineLatest([toObservable(this.selectedSkuId), toObservable(this.asOf)]).pipe(
      switchMap(([skuId, asOf]) => (skuId === null ? of<PriceState>({ status: 'none' }) : this.loadPrice(skuId, asOf))),
    ),
    { initialValue: { status: 'loading' } as PriceState },
  );

  protected readonly readyPrice = computed(() => {
    const state = this.price();
    return state.status === 'ready' ? state.price : null;
  });
  protected readonly readyAmount = computed(() => Number(this.readyPrice()?.amount ?? 0));
  protected readonly readyCurrency = computed(() => this.readyPrice()?.currency ?? '');
  protected readonly canAdd = computed(() => this.readyPrice() !== null && this.selectedSku() !== null);

  protected add(): void {
    const sku = this.selectedSku();
    const price = this.readyPrice();
    if (!sku || !price) return;
    this.added.emit({
      skuId: Number(sku.id),
      productId: Number(this.product().id),
      name: this.product().name,
      roast: this.product().roast,
      grind: sku.grind,
      sizeG: Number(sku.sizeG),
      unitPrice: Number(price.amount),
      currency: price.currency,
    });
  }

  private loadPrice(skuId: number, asOf: string | undefined): Observable<PriceState> {
    return this.api.body(getSkuPrice, { id: skuId, asOf }).pipe(
      map((price): PriceState => ({ status: 'ready', price })),
      catchError(() => of<PriceState>({ status: 'unavailable' })),
      startWith<PriceState>({ status: 'loading' }),
    );
  }
}
