import { BreakpointObserver } from '@angular/cdk/layout';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, input, signal, viewChild } from '@angular/core';
import { rxResource, takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { Router } from '@angular/router';
import { map } from 'rxjs';
import { createPrice } from '../../api/fn/prices/create-price';
import { getPriceHistory } from '../../api/fn/prices/get-price-history';
import { CatalogService, describeSku, grindLabel } from '../../core/catalog/catalog.service';
import { ApiRx } from '../../core/http/api-rx.service';
import { breakpointQueries } from '../../core/layout/breakpoints';
import { NewPrice, PriceDateConflict, PriceFormComponent } from '../../features/admin/price-form/price-form.component';
import { PriceHistoryCardsComponent } from '../../features/admin/price-history-cards/price-history-cards.component';
import { PriceHistoryTableComponent } from '../../features/admin/price-history-table/price-history-table.component';
import { EmptyStateComponent } from '../../shared/empty-state/empty-state.component';
import { ErrorBannerComponent } from '../../shared/error-banner/error-banner.component';
import { PageHeaderComponent } from '../../shared/page-header/page-header.component';

@Component({
  selector: 'app-admin-prices-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    EmptyStateComponent,
    ErrorBannerComponent,
    MatFormFieldModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatSnackBarModule,
    PageHeaderComponent,
    PriceFormComponent,
    PriceHistoryCardsComponent,
    PriceHistoryTableComponent,
  ],
  template: `
    <app-page-header title="Prices" subtitle="Schedule new prices; past prices stay as they were." />
    <app-error-banner class="error" [error]="error() ?? products.error() ?? history.error() ?? null" />

    <mat-form-field class="sku-select">
      <mat-label>SKU</mat-label>
      <mat-select data-testid="sku-select" [value]="skuId()" (valueChange)="selectSku($event)">
        @for (product of products.value() ?? []; track product.id) {
          <mat-optgroup [label]="product.name">
            @for (sku of product.skus; track sku.id) {
              <mat-option [value]="+sku.id">{{ product.name }} · {{ grindName(sku.grind) }} · {{ sku.sizeG }} g</mat-option>
            }
          </mat-optgroup>
        }
      </mat-select>
    </mat-form-field>

    @if (skuId() !== null) {
      <div class="layout">
        <section aria-labelledby="history-title">
          <h2 id="history-title">History · {{ skuName() }}</h2>
          @if (history.isLoading()) {
            <div class="loading"><mat-spinner diameter="32" aria-label="Loading price history" /></div>
          } @else if (history.hasValue()) {
            @if (history.value().length === 0) {
              <app-empty-state icon="sell" title="No prices yet" message="Add the first price for this SKU." />
            } @else if (compact()) {
              <app-price-history-cards [history]="history.value()" />
            } @else {
              <app-price-history-table [history]="history.value()" />
            }
          }
        </section>
        <app-price-form [pending]="saving()" [conflict]="conflict()" (save)="create($event)" />
      </div>
    } @else if (!products.isLoading()) {
      <app-empty-state icon="sell" title="No SKUs" message="There are no SKUs to price yet." />
    }
  `,
  styles: `
    @use 'styles/breakpoints' as bp;
    .error {
      display: block;
      margin-bottom: 16px;
    }
    .sku-select {
      width: 100%;
      max-width: 480px;
    }
    .layout {
      display: grid;
      gap: 24px;
      @include bp.expanded {
        grid-template-columns: minmax(0, 1fr) 360px;
        align-items: start;
      }
    }
    h2 {
      margin: 0 0 8px;
      font: var(--mat-sys-title-large);
      overflow-wrap: anywhere;
    }
    .loading {
      display: flex;
      justify-content: center;
      padding: 24px;
    }
  `,
})
export class AdminPricesPageComponent {
  private readonly api = inject(ApiRx);
  private readonly catalog = inject(CatalogService);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  private readonly form = viewChild(PriceFormComponent);

  /** ?sku= query parameter; defaults to the first SKU of the catalogue. */
  readonly sku = input<string | undefined>(undefined);

  protected readonly products = rxResource({ stream: () => this.catalog.products() });
  protected readonly skuId = computed(() => {
    const fromUrl = Number(this.sku());
    if (Number.isInteger(fromUrl) && fromUrl > 0) return fromUrl;
    const first = this.products.value()?.[0]?.skus[0];
    return first ? Number(first.id) : null;
  });
  protected readonly skuName = computed(() => {
    const sku = describeSku(this.products.value(), this.skuId() ?? -1);
    return sku ? `${sku.name} · ${grindLabel(sku.grind)} · ${sku.sizeG} g` : `SKU ${this.skuId()}`;
  });

  protected readonly history = rxResource({
    params: () => this.skuId() ?? undefined,
    stream: ({ params }) => this.api.body(getPriceHistory, { id: params }),
  });

  protected readonly compact = toSignal(
    inject(BreakpointObserver)
      .observe(breakpointQueries().compact)
      .pipe(map((state) => state.matches)),
    { initialValue: false },
  );

  protected readonly saving = signal(false);
  protected readonly conflict = signal<PriceDateConflict>(null);
  protected readonly error = signal<unknown>(null);
  protected readonly grindName = grindLabel;

  protected selectSku(skuId: number): void {
    this.error.set(null);
    this.conflict.set(null);
    void this.router.navigate([], { queryParams: { sku: skuId } });
  }

  protected create(price: NewPrice): void {
    const skuId = this.skuId();
    if (skuId === null || this.saving()) return;
    this.saving.set(true);
    this.conflict.set(null);
    this.error.set(null);
    this.api
      .body(createPrice, { body: { skuId, ...price } })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.form()?.reset();
          this.history.reload();
          this.snackBar.open('Price saved', undefined, { duration: 4000 });
        },
        error: (error: unknown) => {
          this.saving.set(false);
          const status = error instanceof HttpErrorResponse ? error.status : null;
          if (status === 409) this.conflict.set('duplicate');
          else if (status === 422) this.conflict.set('past');
          else this.error.set(error);
        },
      });
  }
}
