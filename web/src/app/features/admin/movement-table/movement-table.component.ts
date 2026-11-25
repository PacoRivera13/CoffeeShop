import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { MovementResponse } from '../../../api/models/movement-response';
import { movementView } from '../admin-rules';

/** Expanded screens: the stock ledger of one product as a table. */
@Component({
  selector: 'app-movement-table',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, MatIconModule, MatTableModule, RouterLink],
  template: `
    <table mat-table class="table" aria-label="Stock movements" data-testid="movement-table" [dataSource]="movements()">
      <ng-container matColumnDef="when">
        <th mat-header-cell *matHeaderCellDef scope="col">When</th>
        <td mat-cell *matCellDef="let movement">{{ movement.createdAt | date: 'medium' }}</td>
      </ng-container>
      <ng-container matColumnDef="reason">
        <th mat-header-cell *matHeaderCellDef scope="col">Reason</th>
        <td mat-cell *matCellDef="let movement" data-testid="movement-reason">
          <span class="reason">
            <mat-icon aria-hidden="true">{{ view(movement).icon }}</mat-icon>
            {{ view(movement).label }}
          </span>
        </td>
      </ng-container>
      <ng-container matColumnDef="delta">
        <th mat-header-cell *matHeaderCellDef scope="col" class="numeric">Change</th>
        <td mat-cell *matCellDef="let movement" class="numeric tabular-nums" data-testid="movement-delta">
          {{ view(movement).delta }}
        </td>
      </ng-container>
      <ng-container matColumnDef="order">
        <th mat-header-cell *matHeaderCellDef scope="col">Order</th>
        <td mat-cell *matCellDef="let movement">
          @if (movement.orderId !== null) {
            <a [routerLink]="['/orders', movement.orderId]" data-testid="movement-order">Order #{{ movement.orderId }}</a>
          }
        </td>
      </ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr>
      <tr mat-row *matRowDef="let row; columns: columns" data-testid="movement-row"></tr>
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
    .reason {
      display: inline-flex;
      align-items: center;
      gap: 8px;
    }
  `,
})
export class MovementTableComponent {
  readonly movements = input.required<readonly MovementResponse[]>();

  protected readonly columns = ['when', 'reason', 'delta', 'order'];
  protected readonly view = movementView;
}
