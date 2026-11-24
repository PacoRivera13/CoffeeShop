import { ChangeDetectionStrategy, Component } from '@angular/core';
import { EmptyStateComponent } from '../../shared/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../shared/page-header/page-header.component';

/** Placeholder for the admin area (prices, inventory) built in a later milestone. */
@Component({
  selector: 'app-admin-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [EmptyStateComponent, PageHeaderComponent],
  template: `
    <app-page-header title="Admin" subtitle="Prices and inventory" />
    <app-empty-state
      icon="admin_panel_settings"
      title="Admin tools are on their way"
      message="Price and inventory management will appear here in a later release."
    />
  `,
})
export class AdminPageComponent {}
