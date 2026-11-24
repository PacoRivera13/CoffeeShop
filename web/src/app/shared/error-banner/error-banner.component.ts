import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { toDisplayProblem } from '../../core/http/problem';

/** Shows an API failure (ProblemDetails, 409, 422, validation errors) as an alert; renders nothing without one. */
@Component({
  selector: 'app-error-banner',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule],
  template: `
    @if (problem(); as problem) {
      <div class="banner" role="alert" data-testid="error-banner">
        <mat-icon aria-hidden="true">error</mat-icon>
        <div class="text">
          <strong data-testid="error-title">{{ problem.title }}</strong>
          @if (problem.detail) {
            <p data-testid="error-detail">{{ problem.detail }}</p>
          }
          @if (problem.errors.length > 0) {
            <ul>
              @for (message of problem.errors; track $index) {
                <li>{{ message }}</li>
              }
            </ul>
          }
        </div>
      </div>
    }
  `,
  styles: `
    .banner {
      display: flex;
      gap: 12px;
      align-items: flex-start;
      padding: 12px 16px;
      border-radius: 12px;
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
      overflow-wrap: anywhere;
    }
    .text {
      min-width: 0;
    }
    p,
    ul {
      margin: 4px 0 0;
    }
  `,
})
export class ErrorBannerComponent {
  /** An HttpErrorResponse, a ProblemDetails body, or null for no banner. */
  readonly error = input<unknown>(null);

  protected readonly problem = computed(() => toDisplayProblem(this.error()));
}
