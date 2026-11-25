import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { NavLink } from '../app-shell/app-shell.component';

/** Expanded screens: the admin pages behind one "Admin" menu. Loaded lazily, only for admins. */
@Component({
  selector: 'app-admin-menu',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, MatMenuModule, RouterLink, RouterLinkActive],
  template: `
    <button mat-button type="button" data-testid="admin-menu-button" [matMenuTriggerFor]="menu">
      Admin
      <mat-icon iconPositionEnd aria-hidden="true">expand_more</mat-icon>
    </button>
    <mat-menu #menu="matMenu" aria-label="Admin">
      @for (link of links(); track link.path) {
        <a
          mat-menu-item
          data-testid="admin-menu-link"
          [routerLink]="link.path"
          routerLinkActive="active"
          ariaCurrentWhenActive="page"
        >
          {{ link.label }}
        </a>
      }
    </mat-menu>
  `,
})
export class AdminMenuComponent {
  readonly links = input.required<readonly NavLink[]>();
}
