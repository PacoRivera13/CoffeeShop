import { BreakpointObserver } from '@angular/cdk/layout';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Router, RouterLink } from '@angular/router';
import { map } from 'rxjs';
import { listOrders } from '../../api/fn/orders/list-orders';
import { ApiRx } from '../../core/http/api-rx.service';
import { breakpointQueries } from '../../core/layout/breakpoints';
import { OrderCardListComponent } from '../../features/orders/order-card-list/order-card-list.component';
import { OrderTableComponent } from '../../features/orders/order-table/order-table.component';
import { EmptyStateComponent } from '../../shared/empty-state/empty-state.component';
import { ErrorBannerComponent } from '../../shared/error-banner/error-banner.component';
import { PageHeaderComponent } from '../../shared/page-header/page-header.component';
import { PageChange, PagerComponent } from '../../shared/pager/pager.component';

export const ORDERS_PAGE_SIZE = 10;

@Component({
  selector: 'app-orders-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    EmptyStateComponent,
    ErrorBannerComponent,
    MatButtonModule,
    MatProgressSpinnerModule,
    OrderCardListComponent,
    OrderTableComponent,
    PageHeaderComponent,
    PagerComponent,
    RouterLink,
  ],
  template: `
    <app-page-header title="Orders" subtitle="Newest first" />
    <app-error-banner [error]="orders.error() ?? null" />
    @if (orders.isLoading()) {
      <div class="loading"><mat-spinner diameter="40" aria-label="Loading orders" /></div>
    } @else if (orders.hasValue()) {
      @if (orders.value().totalCount === 0) {
        <app-empty-state icon="receipt_long" title="No orders yet" message="Orders you place will be listed here.">
          <a mat-flat-button routerLink="/">Browse coffee</a>
        </app-empty-state>
      } @else {
        @if (compact()) {
          <app-order-card-list [orders]="orders.value().items" />
        } @else {
          <app-order-table [orders]="orders.value().items" />
        }
        <app-pager
          [length]="+orders.value().totalCount"
          [pageIndex]="pageNumber() - 1"
          [pageSize]="pageSize"
          [pageSizeOptions]="[pageSize]"
          (pageChange)="goTo($event)"
        />
      }
    }
  `,
  styles: `
    .loading {
      display: flex;
      justify-content: center;
      padding: 32px;
    }
    app-pager {
      display: block;
      margin-top: 16px;
    }
  `,
})
export class OrdersPageComponent {
  private readonly api = inject(ApiRx);
  private readonly router = inject(Router);

  /** One-based page number from the ?page= query parameter. */
  readonly page = input<string | undefined>(undefined);

  protected readonly pageSize = ORDERS_PAGE_SIZE;
  protected readonly pageNumber = computed(() => {
    const page = Number.parseInt(this.page() ?? '1', 10);
    return Number.isFinite(page) && page >= 1 ? page : 1;
  });

  protected readonly compact = toSignal(
    inject(BreakpointObserver)
      .observe(breakpointQueries().compact)
      .pipe(map((state) => state.matches)),
    { initialValue: false },
  );

  protected readonly orders = rxResource({
    params: () => ({ page: this.pageNumber() }),
    stream: ({ params }) => this.api.body(listOrders, { page: params.page, pageSize: ORDERS_PAGE_SIZE }),
  });

  protected goTo(change: PageChange): void {
    void this.router.navigate([], { queryParams: { page: change.pageIndex + 1 } });
  }
}
