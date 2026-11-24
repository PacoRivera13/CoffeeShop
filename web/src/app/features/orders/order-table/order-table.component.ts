import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { OrderResponse } from '../../../api/models/order-response';
import { OrderStatusBadgeComponent } from '../../../shared/order-status-badge/order-status-badge.component';
import { PriceBadgeComponent } from '../../../shared/price-badge/price-badge.component';
import { itemCount } from '../order-summary';

/** Expanded screens: the orders as a table. */
@Component({
  selector: 'app-order-table',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, MatTableModule, OrderStatusBadgeComponent, PriceBadgeComponent, RouterLink],
  template: `
    <table mat-table class="table" data-testid="order-table" aria-label="Orders" [dataSource]="orders()">
      <ng-container matColumnDef="order">
        <th mat-header-cell *matHeaderCellDef scope="col">Order</th>
        <td mat-cell *matCellDef="let order">
          <a [routerLink]="['/orders', order.id]" data-testid="order-link">Order #{{ order.id }}</a>
        </td>
      </ng-container>
      <ng-container matColumnDef="placed">
        <th mat-header-cell *matHeaderCellDef scope="col">Placed</th>
        <td mat-cell *matCellDef="let order">{{ order.createdAt | date: 'medium' }}</td>
      </ng-container>
      <ng-container matColumnDef="status">
        <th mat-header-cell *matHeaderCellDef scope="col">Status</th>
        <td mat-cell *matCellDef="let order"><app-order-status-badge [status]="order.status" /></td>
      </ng-container>
      <ng-container matColumnDef="items">
        <th mat-header-cell *matHeaderCellDef scope="col" class="numeric">Items</th>
        <td mat-cell *matCellDef="let order" class="numeric tabular-nums" data-testid="order-items">{{ items(order) }}</td>
      </ng-container>
      <ng-container matColumnDef="total">
        <th mat-header-cell *matHeaderCellDef scope="col" class="numeric">Total</th>
        <td mat-cell *matCellDef="let order" class="numeric">
          <app-price-badge [amount]="+order.total" [currency]="order.currency" />
        </td>
      </ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr>
      <tr mat-row *matRowDef="let row; columns: columns" data-testid="order-row"></tr>
    </table>
  `,
  styles: `
    .table {
      width: 100%;
      background: transparent;
    }
    .numeric {
      text-align: end;
    }
  `,
})
export class OrderTableComponent {
  readonly orders = input.required<readonly OrderResponse[]>();

  protected readonly columns = ['order', 'placed', 'status', 'items', 'total'];
  protected readonly items = itemCount;
}
