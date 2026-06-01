import { Dialog } from '@angular/cdk/dialog';
import { DatePipe } from '@angular/common';
import { Component, inject, resource, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { ApiClient } from '../../core/api/api-client';
import { TrashedCardDto } from '../../core/api/dto';
import { openConfirm } from '../../core/ui/confirm-dialog/confirm-dialog.component';
import { IconComponent } from '../../core/ui/icon/icon.component';

@Component({
  selector: 'app-trash',
  standalone: true,
  imports: [DatePipe, IconComponent],
  templateUrl: './trash.component.html',
})
export class TrashComponent {
  private readonly api = inject(ApiClient);
  private readonly dialog = inject(Dialog);

  readonly pageNum = signal(1);
  readonly refreshTick = signal(0);
  readonly pageSize = 10;

  readonly actionBusy = signal<string | null>(null);
  readonly actionError = signal<string | null>(null);

  readonly page = resource({
    params: () => ({ page: this.pageNum(), _tick: this.refreshTick() }),
    loader: ({ params }) =>
      firstValueFrom(this.api.listTrash(params.page, this.pageSize)),
  });

  totalPages(): number {
    const p = this.page.value();
    if (!p) return 1;
    return Math.max(1, Math.ceil(p.totalCount / p.pageSize));
  }

  prev(): void {
    if (this.pageNum() > 1) this.pageNum.update((n) => n - 1);
  }

  next(): void {
    if (this.pageNum() < this.totalPages()) this.pageNum.update((n) => n + 1);
  }

  async onRestore(card: TrashedCardDto): Promise<void> {
    this.actionError.set(null);
    this.actionBusy.set(card.id);
    try {
      await firstValueFrom(this.api.restoreCard(card.id));
      this.refresh();
    } catch (e) {
      this.actionError.set(this.describe(e, 'Could not restore the card.'));
    } finally {
      this.actionBusy.set(null);
    }
  }

  onPurge(card: TrashedCardDto): void {
    const ref = openConfirm(this.dialog, {
      title: 'Delete forever?',
      message: `"${card.title}" will be removed permanently. This cannot be undone. ` +
        `Its ${card.reviewsCount} review record${card.reviewsCount === 1 ? '' : 's'} stay in history.`,
      confirmLabel: 'Delete forever',
      destructive: true,
    });

    ref.closed.subscribe(async (confirmed) => {
      if (!confirmed) return;
      this.actionError.set(null);
      this.actionBusy.set(card.id);
      try {
        await firstValueFrom(this.api.permanentlyDeleteCard(card.id));
        this.refresh();
      } catch (e) {
        this.actionError.set(this.describe(e, 'Could not delete the card.'));
      } finally {
        this.actionBusy.set(null);
      }
    });
  }

  private refresh(): void {
    this.refreshTick.update((n) => n + 1);
  }

  private describe(e: unknown, fallback: string): string {
    if (e && typeof e === 'object' && 'error' in e) {
      const err = (e as { error?: { message?: string } }).error;
      if (err?.message) return err.message;
    }
    return fallback;
  }
}
