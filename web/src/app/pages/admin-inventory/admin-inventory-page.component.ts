import { BreakpointObserver } from '@angular/cdk/layout';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, input, signal, viewChild } from '@angular/core';
import { rxResource, takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { Router } from '@angular/router';
import { map } from 'rxjs';
import { getInventoryMovements } from '../../api/fn/admin-inventory/get-inventory-movements';
import { listInventory } from '../../api/fn/admin-inventory/list-inventory';
import { restockProduct } from '../../api/fn/admin-inventory/restock-product';
import { ApiRx } from '../../core/http/api-rx.service';
import { breakpointQueries } from '../../core/layout/breakpoints';
import { formatGrams } from '../../features/admin/admin-rules';
import { MovementCardsComponent } from '../../features/admin/movement-cards/movement-cards.component';
import { MovementTableComponent } from '../../features/admin/movement-table/movement-table.component';
import { RestockFormComponent } from '../../features/admin/restock-form/restock-form.component';
import { StockRowComponent } from '../../features/admin/stock-row/stock-row.component';
import { EmptyStateComponent } from '../../shared/empty-state/empty-state.component';
import { ErrorBannerComponent } from '../../shared/error-banner/error-banner.component';
import { PageHeaderComponent } from '../../shared/page-header/page-header.component';
import { PageChange, PagerComponent } from '../../shared/pager/pager.component';

export const MOVEMENTS_PAGE_SIZE = 10;

@Component({
  selector: 'app-admin-inventory-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    EmptyStateComponent,
    ErrorBannerComponent,
    MatCardModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MovementCardsComponent,
    MovementTableComponent,
    PageHeaderComponent,
    PagerComponent,
    RestockFormComponent,
    StockRowComponent,
  ],
  template: `
    <app-page-header title="Inventory" subtitle="Stock per product, in grams." />
    <app-error-banner class="error" [error]="error() ?? inventory.error() ?? movements.error() ?? null" />

    @if (inventory.isLoading() && !inventory.hasValue()) {
      <div class="loading"><mat-spinner diameter="40" aria-label="Loading inventory" /></div>
    } @else if (inventory.hasValue()) {
      @if (inventory.value().length === 0) {
        <app-empty-state icon="inventory_2" title="No products" message="Products appear here once they exist." />
      } @else {
        <ul class="stock" aria-label="Stock">
          @for (item of inventory.value(); track item.productId) {
            <li>
              <app-stock-row [item]="item" [selected]="+item.productId === productId()" (choose)="selectProduct(+item.productId)" />
            </li>
          }
        </ul>

        @if (selected(); as product) {
          <mat-card appearance="outlined" class="manage">
            <mat-card-content>
              <h2 id="manage-title">{{ product.name }}</h2>
              <app-restock-form [productName]="product.name" [pending]="restocking()" (restock)="restock($event)" />

              <h3>Movements</h3>
              @if (movements.isLoading() && !movements.hasValue()) {
                <div class="loading"><mat-spinner diameter="32" aria-label="Loading movements" /></div>
              } @else if (movements.hasValue()) {
                @if (movements.value().items.length === 0) {
                  <p class="muted">No movements on this page.</p>
                } @else if (compact()) {
                  <app-movement-cards [movements]="movements.value().items" />
                } @else {
                  <app-movement-table [movements]="movements.value().items" />
                }
                <app-pager
                  [length]="+movements.value().totalCount"
                  [pageIndex]="pageNumber() - 1"
                  [pageSize]="pageSize"
                  [pageSizeOptions]="[pageSize]"
                  (pageChange)="goTo($event)"
                />
              }
            </mat-card-content>
          </mat-card>
        }
      }
    }
  `,
  styles: `
    .error {
      display: block;
      margin-bottom: 16px;
    }
    .loading {
      display: flex;
      justify-content: center;
      padding: 24px;
    }
    .stock {
      display: grid;
      gap: 4px;
      margin: 0 0 24px;
      padding: 0;
      list-style: none;
    }
    .stock li + li {
      border-top: 1px solid var(--mat-sys-outline-variant);
    }
    h2 {
      margin: 0 0 12px;
      font: var(--mat-sys-title-large);
      overflow-wrap: anywhere;
    }
    h3 {
      margin: 16px 0 8px;
      font: var(--mat-sys-title-medium);
    }
    .muted {
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class AdminInventoryPageComponent {
  private readonly api = inject(ApiRx);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  private readonly restockForm = viewChild(RestockFormComponent);

  /** ?product= and ?page= query parameters. */
  readonly product = input<string | undefined>(undefined);
  readonly page = input<string | undefined>(undefined);

  protected readonly pageSize = MOVEMENTS_PAGE_SIZE;
  protected readonly inventory = rxResource({ stream: () => this.api.body(listInventory, undefined) });

  protected readonly productId = computed(() => {
    const fromUrl = Number(this.product());
    if (Number.isInteger(fromUrl) && fromUrl > 0) return fromUrl;
    const first = this.inventory.value()?.[0];
    return first ? Number(first.productId) : null;
  });
  protected readonly selected = computed(
    () => this.inventory.value()?.find((item) => Number(item.productId) === this.productId()) ?? null,
  );
  protected readonly pageNumber = computed(() => {
    const page = Number.parseInt(this.page() ?? '1', 10);
    return Number.isFinite(page) && page >= 1 ? page : 1;
  });

  protected readonly movements = rxResource({
    params: () => (this.productId() === null ? undefined : { productId: this.productId()!, page: this.pageNumber() }),
    stream: ({ params }) =>
      this.api.body(getInventoryMovements, { ...params, pageSize: MOVEMENTS_PAGE_SIZE }),
  });

  protected readonly compact = toSignal(
    inject(BreakpointObserver)
      .observe(breakpointQueries().compact)
      .pipe(map((state) => state.matches)),
    { initialValue: false },
  );

  protected readonly restocking = signal(false);
  protected readonly error = signal<unknown>(null);

  protected selectProduct(productId: number): void {
    this.error.set(null);
    void this.router.navigate([], { queryParams: { product: productId, page: null } });
  }

  protected goTo(change: PageChange): void {
    void this.router.navigate([], { queryParams: { product: this.productId(), page: change.pageIndex + 1 } });
  }

  protected restock(grams: number): void {
    const product = this.selected();
    if (!product || this.restocking()) return;
    this.restocking.set(true);
    this.error.set(null);
    this.api
      .body(restockProduct, { productId: Number(product.productId), body: { grams } })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.restocking.set(false);
          this.inventory.update((items) =>
            items?.map((item) =>
              Number(item.productId) === Number(result.productId)
                ? { ...item, stockG: result.stockG, lastMovementAt: result.movement.createdAt }
                : item,
            ),
          );
          this.restockForm()?.reset();
          this.movements.reload();
          this.snackBar.open(`Added ${formatGrams(grams)} to ${product.name}`, undefined, { duration: 4000 });
        },
        error: (error: unknown) => {
          this.restocking.set(false);
          this.error.set(error);
        },
      });
  }
}
