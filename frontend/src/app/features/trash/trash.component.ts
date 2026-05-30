import { Dialog } from '@angular/cdk/dialog';
import { DatePipe } from '@angular/common';
import { Component, inject, resource, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { ApiClient } from '../../core/api/api-client';
import { TrashedCardDto } from '../../core/api/dto';
import { openConfirm } from '../../core/ui/confirm-dialog.component';

@Component({
  selector: 'app-trash',
  standalone: true,
  imports: [DatePipe],
  template: `
    <header class="mb-6">
      <h1 class="text-2xl font-semibold">Trash</h1>
      <p class="text-sm text-slate-500">
        Soft-deleted cards. Restore to bring them back, or delete permanently to
        free up the slot.
      </p>
    </header>

    @if (page.isLoading()) {
      <p class="text-sm text-slate-400">Loading…</p>
    } @else if (page.error()) {
      <p class="text-sm text-rose-600">Failed to load.</p>
    } @else {
      @let items = page.value()?.items ?? [];
      @if (items.length === 0) {
        <div class="bg-white border border-slate-200 rounded-lg p-8 text-center text-sm text-slate-400">
          Trash is empty.
        </div>
      } @else {
        <div class="bg-white border border-slate-200 rounded-lg divide-y divide-slate-100">
          @for (card of items; track card.id) {
            <div class="p-4 flex items-start justify-between gap-4">
              <div class="min-w-0 flex-1">
                <div class="font-medium text-slate-900 truncate">{{ card.title }}</div>
                <div class="mt-1 flex items-center gap-3 text-xs text-slate-500">
                  <span>Deleted {{ card.deletedAt | date: 'medium' }}</span>
                  <span>·</span>
                  <span>{{ card.reviewsCount }} review{{ card.reviewsCount === 1 ? '' : 's' }}</span>
                </div>
                @if (card.tags.length > 0) {
                  <div class="mt-1 flex flex-wrap gap-1">
                    @for (t of card.tags; track t) {
                      <span class="text-xs text-slate-500">#{{ t }}</span>
                    }
                  </div>
                }
              </div>

              <div class="flex items-center gap-1 ml-2">
                <button
                  type="button"
                  (click)="onRestore(card)"
                  [disabled]="actionBusy() === card.id"
                  class="px-2 py-1 text-xs rounded border border-slate-300 bg-white hover:bg-slate-100 disabled:opacity-40"
                >Restore</button>
                <button
                  type="button"
                  (click)="onPurge(card)"
                  [disabled]="actionBusy() === card.id"
                  class="px-2 py-1 text-xs rounded border border-rose-300 bg-white text-rose-700 hover:bg-rose-50 disabled:opacity-40"
                >Delete forever</button>
              </div>
            </div>
          }
        </div>

        <div class="mt-4 flex items-center justify-between text-sm">
          <button
            type="button"
            (click)="prev()"
            [disabled]="pageNum() === 1"
            class="px-3 py-1.5 rounded border border-slate-300 bg-white disabled:opacity-40"
          >← Prev</button>
          <span class="text-slate-500">Page {{ pageNum() }} of {{ totalPages() }}</span>
          <button
            type="button"
            (click)="next()"
            [disabled]="pageNum() >= totalPages()"
            class="px-3 py-1.5 rounded border border-slate-300 bg-white disabled:opacity-40"
          >Next →</button>
        </div>
      }

      @if (actionError()) {
        <p class="mt-3 text-sm text-rose-600">{{ actionError() }}</p>
      }
    }
  `,
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
