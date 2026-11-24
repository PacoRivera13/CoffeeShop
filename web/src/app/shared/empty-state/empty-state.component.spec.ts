import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { page } from 'vitest/browser';
import { EmptyStateComponent } from './empty-state.component';

@Component({
  imports: [EmptyStateComponent],
  template: `<app-empty-state title="No orders yet"><button type="button">Browse coffee</button></app-empty-state>`,
})
class WithActionHost {}

describe('EmptyStateComponent', () => {
  it('shows the title and message', async () => {
    const fixture = TestBed.createComponent(EmptyStateComponent);
    fixture.componentRef.setInput('title', 'No orders yet');
    fixture.componentRef.setInput('message', 'Your orders will appear here.');
    await fixture.whenStable();

    await expect.element(page.getByRole('heading', { name: 'No orders yet' })).toBeVisible();
    await expect.element(page.getByTestId('empty-state-message')).toHaveTextContent('Your orders will appear here.');
  });

  it('projects an action', async () => {
    const fixture = TestBed.createComponent(WithActionHost);
    await fixture.whenStable();

    await expect.element(page.getByRole('button', { name: 'Browse coffee' })).toBeVisible();
  });

  it('wraps a very long title instead of overflowing', async () => {
    const fixture = TestBed.createComponent(EmptyStateComponent);
    fixture.componentRef.setInput('title', 'Supercalifragilistic'.repeat(20));
    await fixture.whenStable();

    const section = page.getByTestId('empty-state').element();
    expect(section.scrollWidth).toBeLessThanOrEqual(section.clientWidth);
  });
});
