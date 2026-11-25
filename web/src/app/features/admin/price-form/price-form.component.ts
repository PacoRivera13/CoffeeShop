import { ChangeDetectionStrategy, Component, effect, inject, input, output } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { todayUtc } from '../admin-rules';

export interface NewPrice {
  amount: number;
  currency: string;
  /** yyyy-MM-dd, read by the API as midnight UTC. */
  effectiveFrom: string;
}

/** A server rejection that belongs to the date field. */
export type PriceDateConflict = 'duplicate' | 'past' | null;

/** Creates a price. Prices are append-only, so there is no edit mode. */
@Component({
  selector: 'app-price-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    ReactiveFormsModule,
  ],
  template: `
    <mat-card appearance="outlined" data-testid="price-form">
      <mat-card-header>
        <mat-card-title><h2 class="title">New price</h2></mat-card-title>
        <mat-card-subtitle>Prices are append-only: add a new one to change a price.</mat-card-subtitle>
      </mat-card-header>
      <mat-card-content>
        <form class="form" novalidate [formGroup]="form" (ngSubmit)="submit()">
          <mat-form-field>
            <mat-label>Amount</mat-label>
            <input matInput type="number" inputmode="decimal" min="0.01" step="0.01" formControlName="amount" />
            @if (form.controls.amount.hasError('required')) {
              <mat-error>Amount is required.</mat-error>
            } @else if (form.controls.amount.hasError('min')) {
              <mat-error>Amount must be greater than 0.</mat-error>
            }
          </mat-form-field>
          <mat-form-field>
            <mat-label>Currency</mat-label>
            <input matInput maxlength="3" autocapitalize="characters" formControlName="currency" />
            @if (form.controls.currency.hasError('required')) {
              <mat-error>Currency is required.</mat-error>
            } @else if (form.controls.currency.hasError('pattern')) {
              <mat-error>Use a 3-letter currency code, e.g. MXN.</mat-error>
            }
          </mat-form-field>
          <mat-form-field>
            <mat-label>Effective from (UTC)</mat-label>
            <input matInput type="date" data-testid="effective-from" [min]="minDate" formControlName="effectiveFrom" />
            <mat-hint>Starts at 00:00 UTC on this day.</mat-hint>
            @if (form.controls.effectiveFrom.hasError('required')) {
              <mat-error>Effective date is required.</mat-error>
            } @else if (form.controls.effectiveFrom.hasError('past')) {
              <mat-error>Choose today or a later date.</mat-error>
            } @else if (form.controls.effectiveFrom.hasError('duplicate')) {
              <mat-error>A price already starts on this date.</mat-error>
            }
          </mat-form-field>
          <button mat-flat-button type="submit" class="submit" data-testid="save-price" [disabled]="pending()">
            @if (pending()) {
              <mat-spinner diameter="20" aria-hidden="true" />
            } @else {
              <mat-icon aria-hidden="true">add</mat-icon>
            }
            {{ pending() ? 'Saving…' : 'Add price' }}
          </button>
        </form>
      </mat-card-content>
    </mat-card>
  `,
  styles: `
    .title {
      margin: 0;
      font: var(--mat-sys-title-large);
    }
    .form {
      display: grid;
      gap: 8px;
      margin-top: 16px;
    }
    .submit {
      min-height: 44px;
      justify-self: start;
    }
    mat-spinner {
      display: inline-block;
      margin-inline-end: 8px;
    }
  `,
})
export class PriceFormComponent {
  readonly pending = input(false);
  /** Set by the page from a 409 or 422 response; shown on the date field. */
  readonly conflict = input<PriceDateConflict>(null);
  readonly save = output<NewPrice>();

  protected readonly minDate = todayUtc();
  protected readonly form = inject(FormBuilder).nonNullable.group({
    amount: [null as number | null, [Validators.required, Validators.min(0.01)]],
    currency: ['MXN', [Validators.required, Validators.pattern(/^[A-Za-z]{3}$/)]],
    effectiveFrom: ['', [Validators.required, notBefore(this.minDate)]],
  });

  constructor() {
    effect(() => {
      const conflict = this.conflict();
      if (conflict === null) return;
      const control = this.form.controls.effectiveFrom;
      control.setErrors({ ...control.errors, [conflict]: true });
      control.markAsTouched();
    });
  }

  /** Clears the amount and date after a save; the currency stays. */
  reset(): void {
    this.form.reset({ amount: null, currency: this.form.controls.currency.value, effectiveFrom: '' });
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { amount, currency, effectiveFrom } = this.form.getRawValue();
    this.save.emit({ amount: Number(amount), currency: currency.toUpperCase(), effectiveFrom });
  }
}

/** yyyy-MM-dd strings compare correctly as text. */
function notBefore(min: string) {
  return (control: AbstractControl<string>): ValidationErrors | null =>
    control.value && control.value < min ? { past: true } : null;
}
