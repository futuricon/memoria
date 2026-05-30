import { Component, inject, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { ApiClient } from '../../core/api/api-client';
import { GradePillComponent } from '../../core/ui/grade-pill.component';

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
      <div class="text-sm text-slate-500">
        @if (page.value()) {
          {{ page.value()!.totalCount }} total
        }
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
                <div class="font-medium text-slate-900 truncate">{{ card.title }}</div>
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
    }
  `,
})
export class CardsListComponent {
  private readonly api = inject(ApiClient);

  readonly search = signal('');
  readonly selectedTags = signal<string[]>([]);
  readonly pageNum = signal(1);
  readonly pageSize = 10;

  private searchTimer: ReturnType<typeof setTimeout> | null = null;

  readonly tags = resource({
    loader: () => firstValueFrom(this.api.listTags()),
  });

  readonly page = resource({
    params: () => ({
      search: this.search(),
      tags: this.selectedTags(),
      page: this.pageNum(),
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
}
