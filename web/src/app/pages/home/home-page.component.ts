import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';
import { EmptyStateComponent } from '../../shared/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../shared/page-header/page-header.component';

/** Placeholder landing page; the catalogue arrives in the next milestone. */
@Component({
  selector: 'app-home-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [EmptyStateComponent, PageHeaderComponent],
  template: `
    <app-page-header title="Shop" [subtitle]="'Signed in as ' + (auth.email() ?? '')" />
    <app-empty-state
      icon="coffee"
      title="The catalogue is on its way"
      message="Fresh roasts will appear here in the next release."
    />
  `,
})
export class HomePageComponent {
  protected readonly auth = inject(AuthService);
}
