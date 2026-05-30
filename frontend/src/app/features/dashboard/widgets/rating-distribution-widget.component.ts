import { Component, computed, inject, resource } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { ApiClient } from '../../../core/api/api-client';

interface Row {
  label: string;
  count: number;
  pct: number;
  bar: string;
  text: string;
}

@Component({
  selector: 'app-rating-distribution-widget',
  standalone: true,
  template: `
    <section class="bg-white border border-slate-200 rounded-lg p-5">
      <h2 class="text-sm font-medium text-slate-500 uppercase tracking-wide mb-3">
        Ratings · last 30 days
      </h2>
      @if (data.isLoading()) {
        <p class="text-sm text-slate-400">Loading…</p>
      } @else if (data.error()) {
        <p class="text-sm text-rose-600">Failed to load.</p>
      } @else if (data.value(); as d) {
        @if (d.total === 0) {
          <p class="text-sm text-slate-400">No reviews in this window yet.</p>
        } @else {
          <p class="text-xs text-slate-500 mb-2">{{ d.total }} reviews total</p>
          <ul class="space-y-1.5">
            @for (row of rows(); track row.label) {
              <li class="text-xs">
                <div class="flex items-center justify-between mb-0.5">
                  <span class="text-slate-700">{{ row.label }}</span>
                  <span class="text-slate-500">{{ row.count }} · {{ row.pct }}%</span>
                </div>
                <div class="h-2 bg-slate-100 rounded overflow-hidden">
                  <div [style.width.%]="row.pct" [class]="row.bar" class="h-full"></div>
                </div>
              </li>
            }
          </ul>
        }
      }
    </section>
  `,
})
export class RatingDistributionWidgetComponent {
  private readonly api = inject(ApiClient);

  readonly data = resource({
    loader: () => firstValueFrom(this.api.ratingDistribution(30)),
  });

  readonly rows = computed<Row[]>(() => {
    const d = this.data.value();
    if (!d || d.total === 0) return [];
    return [
      this.row('Forgot', d.forgot, d.total, 'bg-rose-400', 'text-rose-700'),
      this.row('Hard', d.hard, d.total, 'bg-amber-400', 'text-amber-700'),
      this.row('Good', d.good, d.total, 'bg-emerald-400', 'text-emerald-700'),
      this.row('Easy', d.easy, d.total, 'bg-sky-400', 'text-sky-700'),
    ];
  });

  private row(label: string, count: number, total: number, bar: string, text: string): Row {
    return {
      label,
      count,
      pct: Math.round((count / total) * 100),
      bar,
      text,
    };
  }
}
