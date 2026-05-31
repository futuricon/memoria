import { Dialog } from '@angular/cdk/dialog';
import { DatePipe } from '@angular/common';
import { Component, inject, resource, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { ApiClient } from '../../core/api/api-client';
import { TrashedCardDto } from '../../core/api/dto';
import { openConfirm } from '../../core/ui/confirm-dialog.component';
import { IconComponent } from '../../core/ui/icon.component';

@Component({
  selector: 'app-trash',
  standalone: true,
  imports: [DatePipe, IconComponent],
  template: `
    <div class="px-4 md:px-8 py-6 md:py-8 max-w-5xl mx-auto">
      <header class="mb-6">
        <h1 class="text-2xl font-semibold text-fg tracking-tight">Trash</h1>
        <p class="text-sm text-fg-muted mt-1">
          Soft-deleted cards. Restore to bring them back, or delete permanently.
        </p>
      </header>

      @if (page.isLoading()) {
        <div class="bg-surface border border-default rounded-xl divide-y divide-default">
          @for (i of [0,1,2]; track i) {
            <div class="p-4 space-y-2">
              <div class="skeleton h-4 w-2/3"></div>
              <div class="skeleton h-3 w-1/3"></div>
            </div>
          }
        </div>
      } @else if (page.error()) {
        <p class="text-sm text-danger">Failed to load.</p>
      } @else {
        @let items = page.value()?.items ?? [];
        @if (items.length === 0) {
          <div class="bg-surface border border-default rounded-xl p-12 text-center">
            <div class="inline-flex w-12 h-12 rounded-full bg-surface-raised items-center justify-center mb-3">
              <app-icon name="trash-2" [size]="22" class="text-fg-muted" />
            </div>
            <p class="text-sm text-fg-muted">Trash is empty.</p>
          </div>
        } @else {
          <div class="bg-surface border border-default rounded-xl divide-y divide-default overflow-hidden">
            @for (card of items; track card.id) {
              <div class="p-4 md:p-5 flex flex-col sm:flex-row sm:items-start sm:justify-between gap-3">
                <div class="min-w-0 flex-1">
                  <div class="font-medium text-fg truncate">{{ card.title }}</div>
                  <div class="mt-1 flex items-center gap-2 text-xs text-fg-muted flex-wrap">
                    <span>Deleted {{ card.deletedAt | date: 'medium' }}</span>
                    <span class="text-fg-muted/50">·</span>
                    <span>{{ card.reviewsCount }} review{{ card.reviewsCount === 1 ? '' : 's' }}</span>
                  </div>
                  @if (card.tags.length > 0) {
                    <div class="mt-2 flex flex-wrap gap-1.5">
                      @for (t of card.tags; track t) {
                        <span class="tag-pill">#{{ t }}</span>
                      }
                    </div>
                  }
                </div>

                <div class="flex items-center gap-2 shrink-0">
                  <button
                    type="button"
                    (click)="onRestore(card)"
                    [disabled]="actionBusy() === card.id"
                    class="h-9 px-3 rounded-md text-xs font-medium border border-default text-fg hover:bg-surface-hover disabled:opacity-40 inline-flex items-center gap-1.5"
                  >
                    <app-icon name="rotate-ccw" [size]="14" />
                    Restore
                  </button>
                  <button
                    type="button"
                    (click)="onPurge(card)"
                    [disabled]="actionBusy() === card.id"
                    class="h-9 px-3 rounded-md text-xs font-medium disabled:opacity-40 inline-flex items-center gap-1.5"
                    [style.color]="'var(--color-rating-again)'"
                    [style.background]="'color-mix(in srgb, var(--color-rating-again) 10%, transparent)'"
                    [style.border]="'1px solid color-mix(in srgb, var(--color-rating-again) 40%, transparent)'"
                  >
                    <app-icon name="trash-2" [size]="14" />
                    Delete forever
                  </button>
                </div>
              </div>
            }
          </div>

          <div class="mt-4 flex items-center justify-between text-sm">
            <button
              type="button"
              (click)="prev()"
              [disabled]="pageNum() === 1"
              class="h-9 px-3 rounded-md border border-default text-fg-secondary hover:bg-surface-hover hover:text-fg disabled:opacity-40 inline-flex items-center gap-1.5"
            >
              <app-icon name="chevron-left" [size]="14" />
              Prev
            </button>
            <span class="text-fg-muted text-xs tabular-nums">Page {{ pageNum() }} of {{ totalPages() }}</span>
            <button
              type="button"
              (click)="next()"
              [disabled]="pageNum() >= totalPages()"
              class="h-9 px-3 rounded-md border border-default text-fg-secondary hover:bg-surface-hover hover:text-fg disabled:opacity-40 inline-flex items-center gap-1.5"
            >
              Next
              <app-icon name="chevron-right" [size]="14" />
            </button>
          </div>
        }

        @if (actionError()) {
          <p class="mt-3 text-sm text-danger">{{ actionError() }}</p>
        }
      }
    </div>
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
