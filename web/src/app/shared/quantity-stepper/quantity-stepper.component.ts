import { ChangeDetectionStrategy, Component, input, linkedSignal, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

/** Quantity between min and max (1-50 by default, the API's per-SKU limit). */
@Component({
  selector: 'app-quantity-stepper',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule],
  template: `
    <div class="stepper" role="group" aria-label="Quantity">
      <button
        mat-icon-button
        type="button"
        aria-label="Decrease quantity"
        data-testid="quantity-decrease"
        [disabled]="current() <= min()"
        (click)="step(-1)"
      >
        <mat-icon aria-hidden="true">remove</mat-icon>
      </button>
      <output class="value tabular-nums" aria-live="polite" data-testid="quantity-value">{{ current() }}</output>
      <button
        mat-icon-button
        type="button"
        aria-label="Increase quantity"
        data-testid="quantity-increase"
        [disabled]="current() >= max()"
        (click)="step(1)"
      >
        <mat-icon aria-hidden="true">add</mat-icon>
      </button>
    </div>
  `,
  styles: `
    .stepper {
      display: inline-flex;
      align-items: center;
      gap: 4px;
    }
    .value {
      min-width: 2.5ch;
      text-align: center;
      font: var(--mat-sys-title-medium);
    }
  `,
})
export class QuantityStepperComponent {
  readonly value = input(1);
  readonly min = input(1);
  readonly max = input(50);
  readonly valueChange = output<number>();

  protected readonly current = linkedSignal(() => this.clamp(this.value()));

  protected step(delta: number): void {
    const next = this.clamp(this.current() + delta);
    if (next === this.current()) return;
    this.current.set(next);
    this.valueChange.emit(next);
  }

  private clamp(value: number): number {
    return Math.min(this.max(), Math.max(this.min(), Math.trunc(value)));
  }
}
