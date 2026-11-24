import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { firstValueFrom } from 'rxjs';
import { page, userEvent } from 'vitest/browser';
import { confirmAction } from './confirm-dialog.component';

describe('ConfirmDialogComponent', () => {
  function open() {
    const result = firstValueFrom(
      confirmAction(TestBed.inject(MatDialog), {
        title: 'Cancel order #7?',
        message: 'This cannot be undone.',
        confirmLabel: 'Cancel order',
        cancelLabel: 'Keep order',
      }),
    );
    return result;
  }

  afterEach(() => TestBed.inject(MatDialog).closeAll());

  it('asks with a labelled alert dialog and focuses the safe choice', async () => {
    void open();

    const dialog = page.getByRole('alertdialog', { name: 'Cancel order #7?' });
    await expect.element(dialog).toBeVisible();
    await expect.element(dialog).toMatchTextContent(/This cannot be undone\./);
    await expect.element(page.getByRole('button', { name: 'Keep order' })).toHaveFocus();
  });

  it('keeps focus inside the dialog', async () => {
    void open();
    await expect.element(page.getByRole('button', { name: 'Keep order' })).toHaveFocus();

    await userEvent.tab();
    await expect.element(page.getByRole('button', { name: 'Cancel order' })).toHaveFocus();
    await userEvent.tab();

    expect(page.getByRole('alertdialog').element().contains(document.activeElement)).toBe(true);
  });

  it('resolves false when Escape closes it', async () => {
    const result = open();
    await expect.element(page.getByRole('alertdialog')).toBeVisible();

    await userEvent.keyboard('{Escape}');

    expect(await result).toBe(false);
    await expect.element(page.getByRole('alertdialog')).not.toBeInTheDocument();
  });

  it('resolves true when confirmed and false when kept', async () => {
    const confirmed = open();
    await page.getByRole('button', { name: 'Cancel order' }).click();
    expect(await confirmed).toBe(true);

    const kept = open();
    await page.getByRole('button', { name: 'Keep order' }).click();
    expect(await kept).toBe(false);
  });
});
