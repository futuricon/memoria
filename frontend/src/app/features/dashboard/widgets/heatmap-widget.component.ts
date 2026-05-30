import { Component, computed, inject, resource } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { ApiClient } from '../../../core/api/api-client';

interface Week {
  cells: Cell[];
}
interface Cell {
  date: string;
  count: number;
  level: 0 | 1 | 2 | 3 | 4;
  isToday: boolean;
}

const DAYS_BACK = 84; // 12 weeks — fits comfortably in the widget width

@Component({
  selector: 'app-heatmap-widget',
  standalone: true,
  template: `
    <section class="bg-white border border-slate-200 rounded-lg p-5">
      <h2 class="text-sm font-medium text-slate-500 uppercase tracking-wide mb-3">
        Activity · last 12 weeks
      </h2>
      @if (data.isLoading()) {
        <p class="text-sm text-slate-400">Loading…</p>
      } @else if (data.error()) {
        <p class="text-sm text-rose-600">Failed to load.</p>
      } @else {
        <div class="flex gap-0.5">
          @for (week of weeks(); track $index) {
            <div class="flex flex-col gap-0.5">
              @for (cell of week.cells; track cell.date) {
                <div
                  class="w-3 h-3 rounded-sm"
                  [class.bg-slate-100]="cell.level === 0"
                  [class.bg-emerald-200]="cell.level === 1"
                  [class.bg-emerald-300]="cell.level === 2"
                  [class.bg-emerald-500]="cell.level === 3"
                  [class.bg-emerald-700]="cell.level === 4"
                  [class.ring-1]="cell.isToday"
                  [class.ring-slate-700]="cell.isToday"
                  [title]="cell.date + ': ' + cell.count + (cell.count === 1 ? ' review' : ' reviews')"
                ></div>
              }
            </div>
          }
        </div>
        <div class="mt-3 flex items-center gap-1.5 text-xs text-slate-500">
          <span>Less</span>
          <div class="w-3 h-3 rounded-sm bg-slate-100"></div>
          <div class="w-3 h-3 rounded-sm bg-emerald-200"></div>
          <div class="w-3 h-3 rounded-sm bg-emerald-300"></div>
          <div class="w-3 h-3 rounded-sm bg-emerald-500"></div>
          <div class="w-3 h-3 rounded-sm bg-emerald-700"></div>
          <span>More</span>
        </div>
      }
    </section>
  `,
})
export class HeatmapWidgetComponent {
  private readonly api = inject(ApiClient);

  readonly data = resource({
    loader: () => firstValueFrom(this.api.activityHeatmap(DAYS_BACK)),
  });

  readonly weeks = computed<Week[]>(() => {
    const map = new Map<string, number>();
    for (const d of this.data.value() ?? []) {
      map.set(d.dateUtc, d.count);
    }

    const today = startOfUtcDay(new Date());
    const todayKey = isoDate(today);
    const start = addDays(today, -(DAYS_BACK - 1));
    // Align grid to a Monday so columns stay tidy.
    const startDow = (start.getUTCDay() + 6) % 7; // 0 = Mon
    const gridStart = addDays(start, -startDow);

    const max = Math.max(0, ...map.values());
    const bucket = (n: number): 0 | 1 | 2 | 3 | 4 => {
      if (n <= 0) return 0;
      if (max <= 1) return 4;
      const pct = n / max;
      if (pct < 0.25) return 1;
      if (pct < 0.5) return 2;
      if (pct < 0.75) return 3;
      return 4;
    };

    const weeks: Week[] = [];
    let cursor = gridStart;
    while (cursor <= today) {
      const cells: Cell[] = [];
      for (let i = 0; i < 7; i++) {
        const key = isoDate(cursor);
        const inRange = cursor >= start && cursor <= today;
        const count = inRange ? map.get(key) ?? 0 : 0;
        cells.push({
          date: key,
          count,
          level: inRange ? bucket(count) : 0,
          isToday: key === todayKey,
        });
        cursor = addDays(cursor, 1);
      }
      weeks.push({ cells });
    }
    return weeks;
  });
}

function startOfUtcDay(d: Date): Date {
  return new Date(Date.UTC(d.getUTCFullYear(), d.getUTCMonth(), d.getUTCDate()));
}

function addDays(d: Date, days: number): Date {
  const out = new Date(d);
  out.setUTCDate(out.getUTCDate() + days);
  return out;
}

function isoDate(d: Date): string {
  return d.toISOString().slice(0, 10);
}
