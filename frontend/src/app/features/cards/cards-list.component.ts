import { Dialog } from '@angular/cdk/dialog';
import { Component, inject, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { ApiClient } from '../../core/api/api-client';
import { CardSummaryDto } from '../../core/api/dto';
import { openConfirm } from '../../core/ui/confirm-dialog.component';
import { GradePillComponent } from '../../core/ui/grade-pill.component';
import { openAddDrawer } from './add-card-drawer.component';
import { openEditDrawer } from './edit-card-drawer.component';

const EDIT_WINDOW_MS = 24 * 60 * 60 * 1000;

@Component({
  selector: 'app-cards-list',
  standalone: true,
  imports: [FormsModule, GradePillComponent],
  template: `
    <header class="mb-6 flex items-end justify-between gap-4">
      <div>
        <h1 class="text-2xl font-semibold">Cards</h1>
        <p class="text-sm text-slate-500">Search and browse your library.</p>
      </div>
      <div class="flex items-center gap-3">
        @if (page.value()) {
          <span class="text-sm text-slate-500">{{ page.value()!.totalCount }} total</span>
        }
        <button
          type="button"
          (click)="onAdd()"
          class="px-3 py-1.5 text-sm rounded bg-slate-900 text-white hover:bg-slate-800"
        >+ New card</button>
      </div>
    </header>

    <div class="flex flex-col gap-4 mb-4 md:flex-row md:items-center">
      <input
        type="search"
        placeholder="Search title or body…"
        [ngModel]="search()"
        (ngModelChange)="onSearchChange($event)"
        name="search"
        class="flex-1 px-3 py-2 border border-slate-300 rounded focus:outline-none focus:ring-2 focus:ring-slate-400 bg-white"
      />
    </div>

    @if (tags.value(); as availableTags) {
      @if (availableTags.length > 0) {
        <div class="flex flex-wrap gap-2 mb-4">
          @for (t of availableTags; track t) {
            <button
              type="button"
              class="px-2 py-1 text-xs rounded border"
              [class.bg-slate-900]="isTagActive(t)"
              [class.text-white]="isTagActive(t)"
              [class.border-slate-900]="isTagActive(t)"
              [class.bg-white]="!isTagActive(t)"
              [class.text-slate-700]="!isTagActive(t)"
              [class.border-slate-300]="!isTagActive(t)"
              (click)="toggleTag(t)"
            >#{{ t }}</button>
          }
        </div>
      }
    }

    @if (page.isLoading()) {
      <p class="text-sm text-slate-400">Loading…</p>
    } @else if (page.error()) {
      <p class="text-sm text-rose-600">Failed to load.</p>
    } @else {
      @let items = page.value()?.items ?? [];
      @if (items.length === 0) {
        <p class="text-sm text-slate-400">No cards match your filters.</p>
      } @else {
        <div class="bg-white border border-slate-200 rounded-lg divide-y divide-slate-100">
          @for (card of items; track card.id) {
            <div class="p-4 flex items-start justify-between gap-4">
              <div class="min-w-0 flex-1">
                <div class="flex items-center gap-2">
                  <div class="font-medium text-slate-900 truncate">{{ card.title }}</div>
                  @if (card.isPaused) {
                    <span
                      class="text-xs px-1.5 py-0.5 rounded bg-amber-100 text-amber-700"
                      [title]="'Paused at stage ' + (card.pausedAtStage ?? 'start')"
                    >⏸ paused</span>
                  }
                </div>
                @if (card.tags.length > 0) {
                  <div class="mt-1 flex flex-wrap gap-1">
                    @for (t of card.tags; track t) {
                      <span class="text-xs text-slate-500">#{{ t }}</span>
                    }
                  </div>
                }
              </div>

              <div class="flex flex-col items-end gap-1">
                <app-grade-pill
                  [type]="card.type"
                  [avgRating]="card.avgRating"
                  [avgAiScore]="card.avgAiScore"
                  [reviewCount]="card.reviewCount"
                />
                <span class="text-xs text-slate-400">
                  {{ card.reviewCount }} review{{ card.reviewCount === 1 ? '' : 's' }}
                </span>
              </div>

              <div class="flex items-center gap-1 ml-2">
                <button
                  type="button"
                  (click)="onEdit(card)"
                  [disabled]="!isEditable(card) || actionBusy() === card.id"
                  class="px-2 py-1 text-xs rounded border border-slate-300 bg-white hover:bg-slate-100 disabled:opacity-40 disabled:cursor-not-allowed"
                  [title]="isEditable(card) ? 'Edit' : 'Edit window (24h) is closed'"
                >Edit</button>
                @if (card.isPaused) {
                  <button
                    type="button"
                    (click)="onUnpause(card)"
                    [disabled]="actionBusy() === card.id"
                    class="px-2 py-1 text-xs rounded border border-slate-300 bg-white hover:bg-slate-100 disabled:opacity-40"
                  >Unpause</button>
                } @else {
                  <button
                    type="button"
                    (click)="onPause(card)"
                    [disabled]="actionBusy() === card.id"
                    class="px-2 py-1 text-xs rounded border border-slate-300 bg-white hover:bg-slate-100 disabled:opacity-40"
                  >Pause</button>
                }
                <button
                  type="button"
                  (click)="onDelete(card)"
                  [disabled]="actionBusy() === card.id"
                  class="px-2 py-1 text-xs rounded border border-rose-300 bg-white text-rose-700 hover:bg-rose-50 disabled:opacity-40"
                >Delete</button>
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
export class CardsListComponent {
  private readonly api = inject(ApiClient);
  private readonly dialog = inject(Dialog);

  readonly search = signal('');
  readonly selectedTags = signal<string[]>([]);
  readonly pageNum = signal(1);
  readonly refreshTick = signal(0);
  readonly pageSize = 10;

  readonly actionBusy = signal<string | null>(null);
  readonly actionError = signal<string | null>(null);

  private searchTimer: ReturnType<typeof setTimeout> | null = null;

  readonly tags = resource({
    loader: () => firstValueFrom(this.api.listTags()),
  });

  readonly page = resource({
    params: () => ({
      search: this.search(),
      tags: this.selectedTags(),
      page: this.pageNum(),
      _tick: this.refreshTick(),
    }),
    loader: ({ params }) =>
      firstValueFrom(
        this.api.listCards({
          search: params.search,
          tags: params.tags,
          page: params.page,
          pageSize: this.pageSize,
        }),
      ),
  });

  totalPages(): number {
    const p = this.page.value();
    if (!p) return 1;
    return Math.max(1, Math.ceil(p.totalCount / p.pageSize));
  }

  isTagActive(tag: string): boolean {
    return this.selectedTags().includes(tag);
  }

  toggleTag(tag: string): void {
    const cur = this.selectedTags();
    const next = cur.includes(tag) ? cur.filter((t) => t !== tag) : [...cur, tag];
    this.selectedTags.set(next);
    this.pageNum.set(1);
  }

  onSearchChange(value: string): void {
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => {
      this.search.set(value);
      this.pageNum.set(1);
    }, 250);
  }

  prev(): void {
    if (this.pageNum() > 1) this.pageNum.update((n) => n - 1);
  }

  next(): void {
    if (this.pageNum() < this.totalPages()) this.pageNum.update((n) => n + 1);
  }

  isEditable(card: CardSummaryDto): boolean {
    return Date.now() - new Date(card.createdAt).getTime() < EDIT_WINDOW_MS;
  }

  onAdd(): void {
    const ref = openAddDrawer(this.dialog);
    ref.closed.subscribe((created) => {
      if (created) this.refresh();
    });
  }

  async onEdit(card: CardSummaryDto): Promise<void> {
    this.actionError.set(null);
    try {
      const full = await firstValueFrom(this.api.getCard(card.id));
      const ref = openEditDrawer(this.dialog, { card: full });
      ref.closed.subscribe((updated) => {
        if (updated) this.refresh();
      });
    } catch (e) {
      this.actionError.set(this.describe(e, 'Could not open the card.'));
    }
  }

  async onPause(card: CardSummaryDto): Promise<void> {
    this.actionError.set(null);
    this.actionBusy.set(card.id);
    try {
      await firstValueFrom(this.api.pauseCard(card.id));
      this.refresh();
    } catch (e) {
      this.actionError.set(this.describe(e, 'Could not pause the card.'));
    } finally {
      this.actionBusy.set(null);
    }
  }

  async onUnpause(card: CardSummaryDto): Promise<void> {
    this.actionError.set(null);
    this.actionBusy.set(card.id);
    try {
      await firstValueFrom(this.api.unpauseCard(card.id));
      this.refresh();
    } catch (e) {
      this.actionError.set(this.describe(e, 'Could not unpause the card.'));
    } finally {
      this.actionBusy.set(null);
    }
  }

  onDelete(card: CardSummaryDto): void {
    const ref = openConfirm(this.dialog, {
      title: 'Delete card?',
      message: `"${card.title}" will be moved to trash. You can restore it later from the bot.`,
      confirmLabel: 'Delete',
      destructive: true,
    });

    ref.closed.subscribe(async (confirmed) => {
      if (!confirmed) return;
      this.actionError.set(null);
      this.actionBusy.set(card.id);
      try {
        await firstValueFrom(this.api.softDeleteCard(card.id));
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
