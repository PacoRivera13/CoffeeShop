import { ChangeDetectionStrategy, Component, inject, input, output, viewChild } from '@angular/core';
import { FormBuilder, FormGroupDirective, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MAX_RESTOCK_GRAMS, MIN_RESTOCK_GRAMS } from '../admin-rules';

/** Adds stock to one product, in whole grams within the API's limits. */
@Component({
  selector: 'app-restock-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    ReactiveFormsModule,
  ],
  template: `
    <form class="form" novalidate data-testid="restock-form" [formGroup]="form" (ngSubmit)="submit()">
      <mat-form-field class="grams">
        <mat-label>Grams to add</mat-label>
        <input
          matInput
          type="number"
          inputmode="numeric"
          step="1"
          [min]="min"
          [max]="max"
          formControlName="grams"
        />
        <span matTextSuffix>g</span>
        @if (form.controls.grams.hasError('required')) {
          <mat-error>Enter the grams to add.</mat-error>
        } @else if (form.controls.grams.hasError('pattern')) {
          <mat-error>Use whole grams.</mat-error>
        } @else if (form.controls.grams.hasError('min') || form.controls.grams.hasError('max')) {
          <mat-error>Restock between 1 and 1,000,000 g.</mat-error>
        }
      </mat-form-field>
      <button mat-flat-button type="submit" class="submit" data-testid="restock-button" [disabled]="pending()">
        @if (pending()) {
          <mat-spinner diameter="20" aria-hidden="true" />
        } @else {
          <mat-icon aria-hidden="true">add_box</mat-icon>
        }
        {{ pending() ? 'Restocking…' : 'Restock ' + productName() }}
      </button>
    </form>
  `,
  styles: `
    .form {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-start;
      gap: 8px 16px;
    }
    .grams {
      flex: 1 1 200px;
      min-width: 0;
    }
    .submit {
      min-height: 48px;
      margin-top: 4px;
    }
    mat-spinner {
      display: inline-block;
      margin-inline-end: 8px;
    }
  `,
})
export class RestockFormComponent {
  readonly productName = input.required<string>();
  readonly pending = input(false);
  readonly restock = output<number>();

  private readonly formDirective = viewChild.required(FormGroupDirective);
  protected readonly min = MIN_RESTOCK_GRAMS;
  protected readonly max = MAX_RESTOCK_GRAMS;
  protected readonly form = inject(FormBuilder).group({
    grams: [
      null as number | null,
      [
        Validators.required,
        Validators.min(MIN_RESTOCK_GRAMS),
        Validators.max(MAX_RESTOCK_GRAMS),
        Validators.pattern(/^\d+$/),
      ],
    ],
  });

  /** resetForm, not reset: it also clears the submitted state, so the empty field is not shown as an error. */
  reset(): void {
    this.formDirective().resetForm();
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.restock.emit(Number(this.form.controls.grams.value));
  }
}
