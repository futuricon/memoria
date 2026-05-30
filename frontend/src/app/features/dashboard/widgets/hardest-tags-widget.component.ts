import { DecimalPipe } from '@angular/common';
import { Component, inject, resource } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { ApiClient } from '../../../core/api/api-client';

@Component({
  selector: 'app-hardest-tags-widget',
  standalone: true,
  imports: [DecimalPipe],
  template: `
    <section class="bg-white border border-slate-200 rounded-lg p-5">
      <h2 class="text-sm font-medium text-slate-500 uppercase tracking-wide mb-3">
        Hardest tags
      </h2>
      @if (data.isLoading()) {
        <p class="text-sm text-slate-400">Loading…</p>
      } @else if (data.error()) {
        <p class="text-sm text-rose-600">Failed to load.</p>
      } @else {
        @let items = data.value() ?? [];
        @if (items.length === 0) {
          <p class="text-sm text-slate-400">
            Not enough reviewed tagged cards yet. Tag your cards and review at least 3 times.
          </p>
        } @else {
          <p class="text-xs text-slate-500 mb-2">
            Tags ranked by lowest average score (0–100).
          </p>
          <ul class="divide-y divide-slate-100">
            @for (t of items; track t.tag) {
              <li class="py-2 flex items-center justify-between gap-3">
                <span class="text-sm text-slate-900 truncate">#{{ t.tag }}</span>
                <span class="text-xs whitespace-nowrap flex items-center gap-2">
                  <span
                    class="px-2 py-0.5 rounded font-medium"
                    [class.bg-emerald-100]="t.avgScore >= 75"
                    [class.text-emerald-700]="t.avgScore >= 75"
                    [class.bg-amber-100]="t.avgScore >= 40 && t.avgScore < 75"
                    [class.text-amber-700]="t.avgScore >= 40 && t.avgScore < 75"
                    [class.bg-rose-100]="t.avgScore < 40"
                    [class.text-rose-700]="t.avgScore < 40"
                  >{{ t.avgScore | number: '1.0-0' }}</span>
                  <span class="text-slate-400">{{ t.cardCount }} card{{ t.cardCount === 1 ? '' : 's' }}</span>
                </span>
              </li>
            }
          </ul>
        }
      }
    </section>
  `,
})
export class HardestTagsWidgetComponent {
  private readonly api = inject(ApiClient);

  readonly data = resource({
    loader: () => firstValueFrom(this.api.tagAverages(5, 3)),
  });
}
