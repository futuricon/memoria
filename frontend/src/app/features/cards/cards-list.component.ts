import { Dialog } from '@angular/cdk/dialog';
import { Component, inject, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { ApiClient } from '../../core/api/api-client';
import { CardSummaryDto } from '../../core/api/dto';
import { openConfirm } from '../../core/ui/confirm-dialog.component';
import { GradePillComponent } from '../../core/ui/grade-pill.component';
import { IconComponent } from '../../core/ui/icon.component';
import { openAddDrawer } from './add-card-drawer.component';
import { openEditDrawer } from './edit-card-drawer.component';

const EDIT_WINDOW_MS = 24 * 60 * 60 * 1000;

@Component({
  selector: 'app-cards-list',
  standalone: true,
  imports: [FormsModule, GradePillComponent, IconComponent],
  template: `
    <div class="px-4 md:px-8 py-6 md:py-8 max-w-6xl mx-auto">
      <header class="mb-5 flex items-end justify-between gap-3 flex-wrap">
        <div>
          <h1 class="text-2xl md:text-3xl font-semibold tracking-tight">Cards</h1>
          @if (page.value(); as p) {
            <p class="text-fg-secondary text-sm">{{ p.totalCount }} total</p>
          } @else {
            <p class="text-fg-secondary text-sm">Search and browse your library.</p>
          }
        </div>
        <button
          type="button"
          (click)="onAdd()"
          class="inline-flex items-center gap-1.5 px-3 h-9 bg-brand text-brand-on text-sm font-medium rounded-md hover:bg-brand-400 transition-colors"
        >
          <app-icon name="plus" [size]="14" />
          New card
        </button>
      </header>

      <!-- Search -->
      <div class="flex flex-col md:flex-row md:items-center gap-3 mb-4">
        <div class="relative flex-1">
          <span class="absolute left-3 top-1/2 -translate-y-1/2 text-fg-muted" aria-hidden="true">
            <app-icon name="search" [size]="16" />
          </span>
          <input
            type="search"
            placeholder="Search title or body…"
            [ngModel]="search()"
            (ngModelChange)="onSearchChange($event)"
            name="search"
            class="w-full h-10 pl-9 pr-3 rounded-md bg-surface border border-default text-sm placeholder:text-fg-muted focus:outline-none focus:border-brand-soft focus:ring-brand-soft"
          />
        </div>
      </div>

      <!-- Tag chips -->
      @if (tags.value(); as availableTags) {
        @if (availableTags.length > 0) {
          <div class="flex flex-wrap gap-1.5 mb-4">
            @for (t of availableTags; track t) {
              <button
                type="button"
                class="px-2.5 py-1 text-xs rounded-full border transition-colors"
                [class.bg-brand-soft]="isTagActive(t)"
                [class.border-brand-soft]="isTagActive(t)"
                [class.text-brand]="isTagActive(t)"
                [class.font-medium]="isTagActive(t)"
                [class.bg-surface]="!isTagActive(t)"
                [class.border-default]="!isTagActive(t)"
                [class.text-fg-secondary]="!isTagActive(t)"
                [class.hover:bg-surface-hover]="!isTagActive(t)"
                (click)="toggleTag(t)"
              >#{{ t }}</button>
            }
          </div>
        }
      }

      @if (page.isLoading()) {
        <div class="bg-surface border border-default rounded-xl p-4 space-y-3">
          <div class="skeleton h-4 w-2/3"></div>
          <div class="skeleton h-4 w-1/2"></div>
          <div class="skeleton h-4 w-3/4"></div>
          <div class="skeleton h-4 w-1/3"></div>
        </div>
      } @else if (page.error()) {
        <p class="text-sm text-danger">Failed to load.</p>
      } @else {
        @let items = page.value()?.items ?? [];
        @if (items.length === 0) {
          <div class="bg-surface border border-default rounded-xl shadow-card p-10 md:p-14 text-center">
            <div class="mx-auto w-20 h-20 rounded-full grid place-items-center mb-4 text-brand"
                 style="background: color-mix(in srgb, var(--color-brand-500) 10%, transparent);">
              <app-icon name="search" [size]="28" />
            </div>
            <h2 class="text-lg font-semibold mb-1">No cards match your filters</h2>
            <p class="text-fg-secondary text-sm max-w-md mx-auto">Clear the search or pick different tags to widen the net.</p>
          </div>
        } @else {
          <div class="bg-surface border border-default rounded-xl shadow-card divide-y divide-default overflow-hidden">
            @for (card of items; track card.id) {
              <div class="px-4 py-3 md:px-5 md:py-4 flex items-start gap-3">
                <div class="min-w-0 flex-1">
                  <div class="flex items-center gap-2 flex-wrap">
                    <span [class]="card.type === 'Question' ? 'chip-question' : 'chip-note'">
                      <app-icon
                        [name]="card.type === 'Question' ? 'help-circle' : 'file-text'"
                        [size]="11"
                      />
                      {{ card.type }}
                    </span>
                    <h3 class="text-sm font-medium text-fg truncate">{{ card.title }}</h3>
                    @if (card.isPaused) {
                      <span
                        class="text-[10px] px-1.5 py-0.5 rounded-full inline-flex items-center gap-1"
                        [style.color]="'var(--color-state-hard)'"
                        [style.background]="'color-mix(in srgb, var(--color-state-hard) 14%, transparent)'"
                        [title]="'Paused at stage ' + (card.pausedAtStage ?? 'start')"
                      >
                        <app-icon name="pause" [size]="10" />
                        paused
                      </span>
                    }
                  </div>
                  @if (card.tags.length > 0) {
                    <div class="mt-1 flex items-center gap-x-2 gap-y-1 flex-wrap text-xs text-fg-muted">
                      @for (t of card.tags; track t; let last = $last) {
                        <span>#{{ t }}</span>
                        @if (!last) { <span aria-hidden="true">·</span> }
                      }
                      <span aria-hidden="true">·</span>
                      <span>{{ card.reviewCount }} review{{ card.reviewCount === 1 ? '' : 's' }}</span>
                    </div>
                  } @else {
                    <div class="mt-1 text-xs text-fg-muted">
                      {{ card.reviewCount }} review{{ card.reviewCount === 1 ? '' : 's' }}
                    </div>
                  }
                </div>

                <!-- Desktop actions -->
                <div class="hidden md:flex items-center gap-2">
                  <app-grade-pill
                    [type]="card.type"
                    [avgRating]="card.avgRating"
                    [avgAiScore]="card.avgAiScore"
                    [reviewCount]="card.reviewCount"
                  />
                  <button
                    type="button"
                    (click)="onEdit(card)"
                    [disabled]="!isEditable(card) || actionBusy() === card.id"
                    class="inline-flex items-center gap-1 px-2 h-8 text-xs rounded-md border border-default text-fg-secondary hover:bg-surface-hover disabled:opacity-40 disabled:cursor-not-allowed"
                    [title]="isEditable(card) ? 'Edit' : 'Edit window (24 h) is closed'"
                  >
                    <app-icon name="pencil" [size]="12" />
                    Edit
                  </button>
                  @if (card.isPaused) {
                    <button
                      type="button"
                      (click)="onUnpause(card)"
                      [disabled]="actionBusy() === card.id"
                      class="inline-flex items-center gap-1 px-2 h-8 text-xs rounded-md border border-default text-fg-secondary hover:bg-surface-hover disabled:opacity-40"
                    >
                      <app-icon name="play" [size]="12" />
                      Unpause
                    </button>
                  } @else {
                    <button
                      type="button"
                      (click)="onPause(card)"
                      [disabled]="actionBusy() === card.id"
                      class="inline-flex items-center gap-1 px-2 h-8 text-xs rounded-md border border-default text-fg-secondary hover:bg-surface-hover disabled:opacity-40"
                    >
                      <app-icon name="pause" [size]="12" />
                      Pause
                    </button>
                  }
                  <button
                    type="button"
                    (click)="onDelete(card)"
                    [disabled]="actionBusy() === card.id"
                    class="inline-flex items-center gap-1 px-2 h-8 text-xs rounded-md border hover:bg-surface-hover disabled:opacity-40"
                    [style.color]="'var(--color-rating-again)'"
                    [style.borderColor]="'color-mix(in srgb, var(--color-rating-again) 35%, var(--color-border))'"
                  >
                    <app-icon name="trash-2" [size]="12" />
                    Delete
                  </button>
                </div>

                <!-- Mobile: kebab -->
                <button
                  type="button"
                  (click)="toggleMenu(card.id, $event)"
                  class="md:hidden w-9 h-9 rounded-md text-fg-muted hover:bg-surface-hover flex items-center justify-center -mr-1 relative"
                  aria-label="Card actions"
                >
                  <app-icon name="more-horizontal" [size]="18" />
                  @if (openMenu() === card.id) {
                    <div
                      class="absolute right-0 top-full mt-1 z-20 w-40 rounded-md bg-surface border border-default shadow-overlay py-1 text-left"
                      (click)="$event.stopPropagation()"
                    >
                      <button
                        type="button"
                        (click)="onEdit(card); closeMenu()"
                        [disabled]="!isEditable(card) || actionBusy() === card.id"
                        class="w-full px-3 py-2 text-xs text-fg hover:bg-surface-hover flex items-center gap-2 disabled:opacity-40 disabled:cursor-not-allowed"
                      ><app-icon name="pencil" [size]="14" /> Edit</button>
                      @if (card.isPaused) {
                        <button
                          type="button"
                          (click)="onUnpause(card); closeMenu()"
                          class="w-full px-3 py-2 text-xs text-fg hover:bg-surface-hover flex items-center gap-2"
                        ><app-icon name="play" [size]="14" /> Unpause</button>
                      } @else {
                        <button
                          type="button"
                          (click)="onPause(card); closeMenu()"
                          class="w-full px-3 py-2 text-xs text-fg hover:bg-surface-hover flex items-center gap-2"
                        ><app-icon name="pause" [size]="14" /> Pause</button>
                      }
                      <button
                        type="button"
                        (click)="onDelete(card); closeMenu()"
                        class="w-full px-3 py-2 text-xs hover:bg-surface-hover flex items-center gap-2"
                        [style.color]="'var(--color-rating-again)'"
                      ><app-icon name="trash-2" [size]="14" /> Delete</button>
                    </div>
                  }
                </button>
              </div>
            }
          </div>

          <div class="mt-4 flex items-center justify-between text-sm">
            <button
              type="button"
              (click)="prev()"
              [disabled]="pageNum() === 1"
              class="inline-flex items-center gap-1 px-3 h-9 rounded-md border border-default bg-surface text-fg-secondary hover:bg-surface-hover disabled:opacity-40"
            >
              <app-icon name="chevron-left" [size]="14" />
              Prev
            </button>
            <span class="text-fg-muted">Page {{ pageNum() }} of {{ totalPages() }}</span>
            <button
              type="button"
              (click)="next()"
              [disabled]="pageNum() >= totalPages()"
              class="inline-flex items-center gap-1 px-3 h-9 rounded-md border border-default bg-surface text-fg-secondary hover:bg-surface-hover disabled:opacity-40"
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
  readonly openMenu = signal<string | null>(null);

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

  toggleMenu(id: string, evt: Event): void {
    evt.stopPropagation();
    this.openMenu.update((cur) => (cur === id ? null : id));
  }

  closeMenu(): void {
    this.openMenu.set(null);
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
      message: `"${card.title}" will be moved to trash. You can restore it later from the trash page.`,
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
