import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ThemeService } from '../../core/theme/theme.service';

@Component({
  selector: 'app-theme-toggle',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule],
  template: `
    <button mat-icon-button type="button" data-testid="theme-toggle" [attr.aria-label]="label()" (click)="theme.toggle()">
      <mat-icon aria-hidden="true">{{ theme.scheme() === 'dark' ? 'light_mode' : 'dark_mode' }}</mat-icon>
    </button>
  `,
})
export class ThemeToggleComponent {
  protected readonly theme = inject(ThemeService);
  protected readonly label = computed(() =>
    this.theme.scheme() === 'dark' ? 'Switch to light theme' : 'Switch to dark theme',
  );
}
