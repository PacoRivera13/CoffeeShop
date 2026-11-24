import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { OrderResponse } from '../../../api/models/order-response';
import { OrderStatusBadgeComponent } from '../../../shared/order-status-badge/order-status-badge.component';
import { PriceBadgeComponent } from '../../../shared/price-badge/price-badge.component';
import { itemCount } from '../order-summary';

/** Compact screens: the same orders as stacked cards. */
@Component({
  selector: 'app-order-card-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, MatCardModule, MatIconModule, OrderStatusBadgeComponent, PriceBadgeComponent, RouterLink],
  template: `
    <ul class="list" aria-label="Orders" data-testid="order-cards">
      @for (order of orders(); track order.id) {
        <li>
          <mat-card appearance="outlined" class="card" data-testid="order-card">
            <mat-card-content class="content">
              <div class="head">
                <a class="link" [routerLink]="['/orders', order.id]" data-testid="order-link">
                  Order #{{ order.id }}
                  <mat-icon aria-hidden="true">chevron_right</mat-icon>
                </a>
                <app-order-status-badge [status]="order.status" />
              </div>
              <p class="meta">{{ order.createdAt | date: 'medium' }}</p>
              <p class="row">
                <span class="tabular-nums">{{ items(order) }} {{ items(order) === 1 ? 'item' : 'items' }}</span>
                <app-price-badge [amount]="+order.total" [currency]="order.currency" />
              </p>
            </mat-card-content>
          </mat-card>
        </li>
      }
    </ul>
  `,
  styles: `
    .list {
      display: grid;
      gap: 12px;
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .content {
      display: grid;
      gap: 4px;
    }
    .head,
    .row {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 8px;
      margin: 0;
    }
    .link {
      display: inline-flex;
      align-items: center;
      min-height: 44px;
      font: var(--mat-sys-title-medium);
    }
    .meta {
      margin: 0;
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class OrderCardListComponent {
  readonly orders = input.required<readonly OrderResponse[]>();

  protected readonly items = itemCount;
}
