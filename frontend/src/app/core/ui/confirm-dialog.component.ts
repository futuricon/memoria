import { Dialog, DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { Component, inject } from '@angular/core';

export interface ConfirmDialogData {
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  destructive?: boolean;
}

@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  template: `
    <div class="bg-surface border border-default rounded-xl shadow-overlay">
      <div class="p-6">
        <h2 class="text-lg font-semibold text-fg mb-2">{{ data.title }}</h2>
        <p class="text-sm text-fg-secondary leading-relaxed">{{ data.message }}</p>
      </div>
      <div class="flex justify-end gap-2 px-6 py-4 border-t border-default rounded-b-xl bg-surface-raised">
        <button
          type="button"
          (click)="cancel()"
          class="px-3 h-9 text-sm rounded-md border border-default text-fg-secondary hover:bg-surface-hover transition-colors"
        >{{ data.cancelLabel ?? 'Cancel' }}</button>
        <button
          type="button"
          (click)="confirm()"
          class="px-3 h-9 text-sm font-medium rounded-md text-white transition-colors"
          [class.bg-brand]="!data.destructive"
          [class.text-brand-on]="!data.destructive"
          [class.hover:bg-brand-400]="!data.destructive"
          [style.background]="data.destructive ? 'var(--color-rating-again)' : null"
        >{{ data.confirmLabel ?? 'OK' }}</button>
      </div>
    </div>
  `,
})
export class ConfirmDialogComponent {
  readonly data = inject<ConfirmDialogData>(DIALOG_DATA);
  private readonly ref = inject<DialogRef<boolean, ConfirmDialogComponent>>(DialogRef);

  confirm(): void {
    this.ref.close(true);
  }

  cancel(): void {
    this.ref.close(false);
  }
}

export function openConfirm(dialog: Dialog, data: ConfirmDialogData) {
  return dialog.open<boolean, ConfirmDialogData, ConfirmDialogComponent>(
    ConfirmDialogComponent,
    {
      data,
      panelClass: 'app-confirm-dialog',
      backdropClass: 'app-overlay-backdrop',
      disableClose: false,
    },
  );
}
