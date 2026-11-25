import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { MovementResponse } from '../../../api/models/movement-response';
import { movementView } from '../admin-rules';

/** Compact screens: the same ledger as stacked cards. */
@Component({
  selector: 'app-movement-cards',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, MatCardModule, MatIconModule, RouterLink],
  template: `
    <ul class="list" aria-label="Stock movements" data-testid="movement-cards">
      @for (movement of movements(); track movement.id) {
        <li>
          <mat-card appearance="outlined" data-testid="movement-card">
            <mat-card-content class="content">
              <div class="row">
                <span class="reason" data-testid="movement-reason">
                  <mat-icon aria-hidden="true">{{ view(movement).icon }}</mat-icon>
                  {{ view(movement).label }}
                </span>
                <span class="delta tabular-nums" data-testid="movement-delta">{{ view(movement).delta }}</span>
              </div>
              <p class="meta">
                {{ movement.createdAt | date: 'medium' }}
                @if (movement.orderId !== null) {
                  · <a [routerLink]="['/orders', movement.orderId]" data-testid="movement-order">Order #{{ movement.orderId }}</a>
                }
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
    .row {
      display: flex;
      flex-wrap: wrap;
      justify-content: space-between;
      gap: 8px;
    }
    .reason {
      display: inline-flex;
      align-items: center;
      gap: 8px;
    }
    .delta {
      font: var(--mat-sys-title-medium);
    }
    .meta {
      margin: 0;
      color: var(--mat-sys-on-surface-variant);
    }
    .meta a {
      display: inline-flex;
      align-items: center;
      min-height: 44px;
    }
  `,
})
export class MovementCardsComponent {
  readonly movements = input.required<readonly MovementResponse[]>();

  protected readonly view = movementView;
}
