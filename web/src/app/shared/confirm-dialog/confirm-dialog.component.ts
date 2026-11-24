import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule } from '@angular/material/dialog';
import { map, Observable } from 'rxjs';

export interface ConfirmDialogData {
  title: string;
  message: string;
  confirmLabel: string;
  cancelLabel?: string;
}

/**
 * Asks before an irreversible action. MatDialog traps focus and closes on Escape; focus starts on the safe choice.
 */
@Component({
  selector: 'app-confirm-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatDialogModule],
  template: `
    <h2 mat-dialog-title data-testid="confirm-title">{{ data.title }}</h2>
    <mat-dialog-content>
      <p data-testid="confirm-message">{{ data.message }}</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" data-testid="confirm-cancel" [mat-dialog-close]="false" cdkFocusInitial>
        {{ data.cancelLabel ?? 'Keep it' }}
      </button>
      <button mat-flat-button type="button" data-testid="confirm-accept" [mat-dialog-close]="true">
        {{ data.confirmLabel }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    button {
      min-height: 44px;
    }
  `,
})
export class ConfirmDialogComponent {
  protected readonly data = inject<ConfirmDialogData>(MAT_DIALOG_DATA);
}

/** Opens the dialog; emits true only when the user confirms (Escape, backdrop or cancel give false). */
export function confirmAction(dialog: MatDialog, data: ConfirmDialogData): Observable<boolean> {
  return dialog
    .open<ConfirmDialogComponent, ConfirmDialogData, boolean>(ConfirmDialogComponent, {
      data,
      role: 'alertdialog',
      autoFocus: 'first-tabbable',
      restoreFocus: true,
      maxWidth: 'min(560px, calc(100vw - 32px))',
    })
    .afterClosed()
    .pipe(map((result) => result === true));
}
