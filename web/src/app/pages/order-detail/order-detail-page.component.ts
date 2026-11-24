import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, input, LOCALE_ID, signal } from '@angular/core';
import { rxResource, takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { RouterLink } from '@angular/router';
import { catchError, filter, Observable, of, switchMap } from 'rxjs';
import { cancelOrder } from '../../api/fn/orders/cancel-order';
import { getOrder } from '../../api/fn/orders/get-order';
import { payOrder } from '../../api/fn/orders/pay-order';
import { OrderResponse } from '../../api/models/order-response';
import { CatalogService, describeSku, grindLabel } from '../../core/catalog/catalog.service';
import { ApiRx } from '../../core/http/api-rx.service';
import { InvoiceButtonComponent } from '../../features/orders/invoice-button/invoice-button.component';
import { canCancel, canPay, hasInvoice } from '../../features/orders/order-summary';
import { OrderTimelineComponent } from '../../features/orders/order-timeline/order-timeline.component';
import { confirmAction } from '../../shared/confirm-dialog/confirm-dialog.component';
import { ErrorBannerComponent } from '../../shared/error-banner/error-banner.component';
import { OrderStatusBadgeComponent } from '../../shared/order-status-badge/order-status-badge.component';
import { PageHeaderComponent } from '../../shared/page-header/page-header.component';
import { PriceBadgeComponent } from '../../shared/price-badge/price-badge.component';

type Action = 'pay' | 'cancel';

@Component({
  selector: 'app-order-detail-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ErrorBannerComponent,
    InvoiceButtonComponent,
    MatButtonModule,
    MatDialogModule,
    MatIconModule,
    MatProgressSpinnerModule,
    OrderStatusBadgeComponent,
    OrderTimelineComponent,
    PageHeaderComponent,
    PriceBadgeComponent,
    RouterLink,
  ],
  template: `
    <a class="back" routerLink="/orders"><mat-icon aria-hidden="true">arrow_back</mat-icon> All orders</a>
    <app-page-header [title]="'Order #' + id()" [subtitle]="placedAt()">
      @if (order.hasValue()) {
        <app-order-status-badge [status]="order.value().status" />
      }
    </app-page-header>
    <app-error-banner class="error" [error]="actionError() ?? order.error() ?? null" />

    @if (order.isLoading() && !order.hasValue()) {
      <div class="loading"><mat-spinner diameter="40" aria-label="Loading order" /></div>
    } @else if (order.hasValue()) {
      <div class="layout">
        <section aria-labelledby="lines-title">
          <h2 id="lines-title">Items</h2>
          <ul class="lines" data-testid="order-lines">
            @for (line of order.value().lines; track line.skuId) {
              <li class="line" data-testid="order-line">
                <div class="info">
                  <span class="name">{{ lineName(+line.skuId) }}</span>
                  <span class="muted">
                    {{ line.qty }} ×
                    <app-price-badge [amount]="+line.unitPrice" [currency]="order.value().currency" />
                  </span>
                </div>
                <app-price-badge [amount]="+line.lineTotal" [currency]="order.value().currency" />
              </li>
            }
          </ul>
          <p class="total">
            <span>Total</span>
            <span data-testid="order-total">
              <app-price-badge [amount]="+order.value().total" [currency]="order.value().currency" />
            </span>
          </p>
          <p class="muted note">Prices were fixed when the order was placed.</p>
        </section>

        <section aria-labelledby="progress-title">
          <h2 id="progress-title">Progress</h2>
          <app-order-timeline [status]="order.value().status" />
          <div class="actions">
            @if (showPay()) {
              <button
                mat-flat-button
                type="button"
                data-testid="pay-button"
                [disabled]="!payEnabled()"
                [attr.aria-busy]="pending() === 'pay'"
                (click)="pay()"
              >
                @if (pending() === 'pay') {
                  <mat-spinner diameter="20" aria-hidden="true" />
                } @else {
                  <mat-icon aria-hidden="true">payments</mat-icon>
                }
                Pay
              </button>
            }
            @if (showCancel()) {
              <button
                mat-stroked-button
                type="button"
                data-testid="cancel-button"
                [disabled]="!cancelEnabled()"
                [attr.aria-busy]="pending() === 'cancel'"
                (click)="cancel()"
              >
                @if (pending() === 'cancel') {
                  <mat-spinner diameter="20" aria-hidden="true" />
                } @else {
                  <mat-icon aria-hidden="true">block</mat-icon>
                }
                Cancel order
              </button>
            }
            @if (showInvoice()) {
              <app-invoice-button [orderId]="id()" (failed)="actionError.set($event)" />
            }
          </div>
        </section>
      </div>
    }
  `,
  styles: `
    @use 'styles/breakpoints' as bp;
    .back {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      min-height: 44px;
      margin-bottom: 8px;
    }
    .error {
      display: block;
      margin-bottom: 16px;
    }
    .loading {
      display: flex;
      justify-content: center;
      padding: 32px;
    }
    .layout {
      display: grid;
      gap: 24px;
      @include bp.expanded {
        grid-template-columns: minmax(0, 1fr) 320px;
        gap: 32px;
      }
    }
    h2 {
      margin: 0 0 8px;
      font: var(--mat-sys-title-large);
    }
    .lines {
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .line,
    .total {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 12px;
      padding: 8px 0;
      margin: 0;
    }
    .line + .line {
      border-top: 1px solid var(--mat-sys-outline-variant);
    }
    .info {
      display: grid;
      min-width: 0;
      overflow-wrap: anywhere;
    }
    .name {
      font: var(--mat-sys-title-medium);
    }
    .total {
      border-top: 2px solid var(--mat-sys-outline);
      font: var(--mat-sys-title-medium);
    }
    .muted {
      color: var(--mat-sys-on-surface-variant);
    }
    .note {
      margin: 4px 0 0;
      font: var(--mat-sys-body-medium);
    }
    .actions {
      display: flex;
      flex-wrap: wrap;
      gap: 12px;
      margin-top: 16px;
    }
    .actions button {
      min-height: 44px;
    }
    mat-spinner {
      display: inline-block;
      margin-inline-end: 8px;
    }
  `,
})
export class OrderDetailPageComponent {
  private readonly api = inject(ApiRx);
  private readonly dialog = inject(MatDialog);
  private readonly destroyRef = inject(DestroyRef);
  private readonly datePipe = new DatePipe(inject(LOCALE_ID));

  /** Route parameter :id. */
  readonly idParam = input.required<string>({ alias: 'id' });
  protected readonly id = computed(() => Number(this.idParam()));

  protected readonly order = rxResource({
    params: () => ({ id: this.id() }),
    stream: ({ params }) => this.api.body(getOrder, { id: params.id }),
  });
  // Names are a nicety: without the catalogue the lines still show their SKU.
  private readonly products = toSignal(inject(CatalogService).products().pipe(catchError(() => of([]))));

  protected readonly pending = signal<Action | null>(null);
  protected readonly actionError = signal<unknown>(null);

  private readonly status = computed(() => (this.order.hasValue() ? this.order.value().status : null));
  protected readonly showPay = computed(() => this.status() !== null && canPay(this.status()!));
  protected readonly showCancel = computed(() => this.status() !== null && canCancel(this.status()!));
  protected readonly showInvoice = computed(() => this.status() !== null && hasInvoice(this.status()!));
  protected readonly payEnabled = computed(() => this.showPay() && this.pending() === null);
  protected readonly cancelEnabled = computed(() => this.showCancel() && this.pending() === null);
  protected readonly placedAt = computed(() =>
    this.order.hasValue() ? `Placed ${this.datePipe.transform(this.order.value().createdAt, 'medium')}` : '',
  );

  protected lineName(skuId: number): string {
    const sku = describeSku(this.products(), skuId);
    return sku ? `${sku.name} · ${grindLabel(sku.grind)} · ${sku.sizeG} g` : `SKU ${skuId}`;
  }

  protected pay(): void {
    this.run('pay', of(true), () => this.api.body(payOrder, { id: this.id() }));
  }

  protected cancel(): void {
    const confirmed = confirmAction(this.dialog, {
      title: `Cancel order #${this.id()}?`,
      message: 'The reserved coffee goes back to stock. This cannot be undone.',
      confirmLabel: 'Cancel order',
      cancelLabel: 'Keep order',
    });
    this.run('cancel', confirmed, () => this.api.body(cancelOrder, { id: this.id() }));
  }

  private run(action: Action, proceed: Observable<boolean>, request: () => Observable<OrderResponse>): void {
    if (this.pending() !== null) return;
    proceed
      .pipe(
        filter(Boolean),
        switchMap(() => {
          this.pending.set(action);
          this.actionError.set(null);
          return request();
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (order) => {
          this.pending.set(null);
          this.order.set(order);
        },
        error: (error: unknown) => {
          this.pending.set(null);
          this.actionError.set(error);
          this.order.reload();
        },
      });
  }
}
