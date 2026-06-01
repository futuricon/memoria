import { Dialog, DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';

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
  templateUrl: './confirm-dialog.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
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
