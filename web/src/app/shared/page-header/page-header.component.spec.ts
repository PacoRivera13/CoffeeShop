import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { page } from 'vitest/browser';
import { PageHeaderComponent } from './page-header.component';

@Component({
  imports: [PageHeaderComponent],
  template: `<app-page-header title="Orders"><button type="button">New order</button></app-page-header>`,
})
class WithActionHost {}

describe('PageHeaderComponent', () => {
  it('renders the title as the page heading with an optional subtitle', async () => {
    const fixture = TestBed.createComponent(PageHeaderComponent);
    fixture.componentRef.setInput('title', 'Orders');
    fixture.componentRef.setInput('subtitle', 'Newest first');
    await fixture.whenStable();

    await expect.element(page.getByRole('heading', { level: 1, name: 'Orders' })).toBeVisible();
    await expect.element(page.getByTestId('page-subtitle')).toHaveTextContent('Newest first');
  });

  it('omits the subtitle when there is none', async () => {
    const fixture = TestBed.createComponent(PageHeaderComponent);
    fixture.componentRef.setInput('title', 'Orders');
    await fixture.whenStable();

    await expect.element(page.getByTestId('page-subtitle')).not.toBeInTheDocument();
  });

  it('projects page actions', async () => {
    const fixture = TestBed.createComponent(WithActionHost);
    await fixture.whenStable();

    await expect.element(page.getByRole('button', { name: 'New order' })).toBeVisible();
  });
});
