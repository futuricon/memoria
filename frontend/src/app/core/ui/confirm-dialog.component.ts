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
    <div class="bg-white rounded-lg shadow-xl border border-slate-200">
      <div class="p-6">
        <h2 class="text-lg font-semibold text-slate-900 mb-2">{{ data.title }}</h2>
        <p class="text-sm text-slate-600">{{ data.message }}</p>
      </div>
      <div class="flex justify-end gap-2 px-6 py-4 bg-slate-50 rounded-b-lg">
        <button
          type="button"
          (click)="cancel()"
          class="px-3 py-1.5 text-sm rounded border border-slate-300 bg-white hover:bg-slate-100"
        >{{ data.cancelLabel ?? 'Cancel' }}</button>
        <button
          type="button"
          (click)="confirm()"
          class="px-3 py-1.5 text-sm rounded text-white"
          [class.bg-rose-600]="data.destructive"
          [class.hover:bg-rose-700]="data.destructive"
          [class.bg-slate-900]="!data.destructive"
          [class.hover:bg-slate-800]="!data.destructive"
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
