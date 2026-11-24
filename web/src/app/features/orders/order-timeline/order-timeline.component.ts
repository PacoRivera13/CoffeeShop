import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

export type StepState = 'done' | 'current' | 'upcoming' | 'cancelled';

export interface TimelineStep {
  label: string;
  state: StepState;
}

const FLOW = ['Pending', 'Paid', 'Preparing', 'Shipped', 'Delivered'] as const;
const LABELS: Record<string, string> = {
  Pending: 'Placed',
  Paid: 'Paid',
  Preparing: 'Preparing',
  Shipped: 'Shipped',
  Delivered: 'Delivered',
};
const ICONS: Record<StepState, string> = {
  done: 'check_circle',
  current: 'radio_button_checked',
  upcoming: 'radio_button_unchecked',
  cancelled: 'cancel',
};
const STATE_TEXT: Record<StepState, string> = {
  done: 'completed',
  current: 'current step',
  upcoming: 'not reached yet',
  cancelled: 'order cancelled',
};

/** Where the order is in its life, as icons plus text. A cancelled order ends after being placed. */
export function timelineSteps(status: string): TimelineStep[] {
  if (status === 'Cancelled') {
    return [
      { label: LABELS['Pending'], state: 'done' },
      { label: 'Cancelled', state: 'cancelled' },
    ];
  }
  const index = FLOW.indexOf(status as (typeof FLOW)[number]);
  return FLOW.map((step, i) => ({
    label: LABELS[step],
    state: index < 0 || i > index ? 'upcoming' : i < index || step === 'Delivered' ? 'done' : 'current',
  }));
}

@Component({
  selector: 'app-order-timeline',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule],
  template: `
    <ol class="timeline" aria-label="Order progress" data-testid="order-timeline">
      @for (step of steps(); track step.label) {
        <li
          class="step"
          data-testid="timeline-step"
          [attr.data-state]="step.state"
          [attr.aria-current]="step.state === 'current' ? 'step' : null"
        >
          <mat-icon class="icon" aria-hidden="true">{{ icons[step.state] }}</mat-icon>
          <span class="label">{{ step.label }}</span>
          <span class="cdk-visually-hidden">({{ stateText[step.state] }})</span>
        </li>
      }
    </ol>
  `,
  styles: `
    .timeline {
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .step {
      position: relative;
      display: flex;
      align-items: center;
      gap: 12px;
      min-height: 40px;
      color: var(--mat-sys-on-surface-variant);
    }
    .step + .step::before {
      content: '';
      position: absolute;
      left: 11px;
      top: -12px;
      height: 16px;
      border-left: 2px solid var(--mat-sys-outline-variant);
    }
    .step[data-state='done'] .icon,
    .step[data-state='current'] .icon {
      color: var(--mat-sys-primary);
    }
    .step[data-state='current'] .label {
      font-weight: 600;
      color: var(--mat-sys-on-surface);
    }
    .step[data-state='cancelled'] {
      color: var(--mat-sys-error);
      font-weight: 600;
    }
  `,
})
export class OrderTimelineComponent {
  readonly status = input.required<string>();

  protected readonly steps = computed(() => timelineSteps(this.status()));
  protected readonly icons = ICONS;
  protected readonly stateText = STATE_TEXT;
}
