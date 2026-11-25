import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { PriceHistoryResponse } from '../../../api/models/price-history-response';
import { PriceBadgeComponent } from '../../../shared/price-badge/price-badge.component';
import { sortByEffectiveFromDesc } from '../admin-rules';

/** Expanded screens: a SKU's price history, newest first, with the current price marked. */
@Component({
  selector: 'app-price-history-table',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, MatChipsModule, MatIconModule, MatTableModule, PriceBadgeComponent],
  template: `
    <table mat-table class="table" aria-label="Price history" data-testid="price-history-table" [dataSource]="rows()">
      <ng-container matColumnDef="effectiveFrom">
        <th mat-header-cell *matHeaderCellDef scope="col">Effective from (UTC)</th>
        <td mat-cell *matCellDef="let price" data-testid="price-effective-from">
          {{ price.effectiveFrom | date: 'medium' : 'UTC' }}
        </td>
      </ng-container>
      <ng-container matColumnDef="amount">
        <th mat-header-cell *matHeaderCellDef scope="col" class="numeric">Price</th>
        <td mat-cell *matCellDef="let price" class="numeric">
          <app-price-badge [amount]="+price.amount" [currency]="price.currency" />
        </td>
      </ng-container>
      <ng-container matColumnDef="status">
        <th mat-header-cell *matHeaderCellDef scope="col">Status</th>
        <td mat-cell *matCellDef="let price">
          @if (price.isCurrent) {
            <mat-chip-set aria-label="Price status">
              <mat-chip disableRipple highlighted data-testid="current-chip">
                <mat-icon matChipAvatar aria-hidden="true">check</mat-icon>
                Current
              </mat-chip>
            </mat-chip-set>
          }
        </td>
      </ng-container>
      <tr mat-header-row *matHeaderRowDef="columns"></tr>
      <tr mat-row *matRowDef="let row; columns: columns" data-testid="price-row"></tr>
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
export class PriceHistoryTableComponent {
  readonly history = input.required<readonly PriceHistoryResponse[]>();

  protected readonly rows = computed(() => sortByEffectiveFromDesc(this.history()));
  protected readonly columns = ['effectiveFrom', 'amount', 'status'];
}
