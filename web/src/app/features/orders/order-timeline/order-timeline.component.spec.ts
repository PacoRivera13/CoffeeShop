import { TestBed } from '@angular/core/testing';
import { page } from 'vitest/browser';
import { OrderTimelineComponent, timelineSteps } from './order-timeline.component';

describe('OrderTimelineComponent', () => {
  async function render(status: string) {
    const fixture = TestBed.createComponent(OrderTimelineComponent);
    fixture.componentRef.setInput('status', status);
    await fixture.whenStable();
    return fixture;
  }

  it('marks reached steps done and the current one with aria-current', async () => {
    await render('Preparing');

    const steps = page.getByTestId('timeline-step');
    expect(steps.all().map((s) => s.element().getAttribute('data-state'))).toEqual([
      'done',
      'done',
      'current',
      'upcoming',
      'upcoming',
    ]);
    await expect.element(steps.nth(2)).toHaveAttribute('aria-current', 'step');
    await expect.element(steps.nth(2)).toMatchTextContent(/Preparing.*current step/);
    await expect.element(steps.nth(0)).toMatchTextContent(/Placed.*completed/);
  });

  it('ends a cancelled order after it was placed', async () => {
    await render('Cancelled');

    await expect.element(page.getByTestId('timeline-step').nth(1)).toMatchTextContent(/Cancelled.*order cancelled/);
    expect(page.getByTestId('timeline-step').all()).toHaveLength(2);
  });

  it('treats a delivered order as complete', () => {
    expect(timelineSteps('Delivered').every((s) => s.state === 'done')).toBe(true);
    expect(timelineSteps('Unknown').every((s) => s.state === 'upcoming')).toBe(true);
  });
});
