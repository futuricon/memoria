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

const DAYS_BACK = 84; // 12 weeks

@Component({
  selector: 'app-heatmap-widget',
  standalone: true,
  template: `
    <section class="bg-surface border border-default rounded-xl shadow-card p-5">
      <div class="flex items-baseline justify-between mb-3">
        <h2 class="text-[11px] uppercase tracking-wider text-fg-muted font-medium">
          Activity · last 12 weeks
        </h2>
      </div>

      @if (data.isLoading()) {
        <div class="flex gap-[3px]">
          @for (i of placeholderRange; track i) {
            <div class="flex flex-col gap-[3px]">
              @for (j of placeholderCol; track j) {
                <div class="skeleton w-3 h-3"></div>
              }
            </div>
          }
        </div>
      } @else if (data.error()) {
        <p class="text-sm text-danger">Failed to load.</p>
      } @else {
        <div class="overflow-x-auto -mx-1 px-1">
          <div class="flex gap-[3px] min-w-max">
            @for (week of weeks(); track $index) {
              <div class="flex flex-col gap-[3px]">
                @for (cell of week.cells; track cell.date) {
                  <div
                    class="hm-cell"
                    [class.is-today]="cell.isToday"
                    [attr.data-level]="cell.level || null"
                    [title]="cell.date + ': ' + cell.count + (cell.count === 1 ? ' review' : ' reviews')"
                  ></div>
                }
              </div>
            }
          </div>
        </div>
        <div class="mt-3 flex items-center gap-1.5 text-[11px] text-fg-muted">
          <span>Less</span>
          <span class="hm-cell"></span>
          <span class="hm-cell" data-level="1"></span>
          <span class="hm-cell" data-level="2"></span>
          <span class="hm-cell" data-level="3"></span>
          <span class="hm-cell" data-level="4"></span>
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

  readonly placeholderRange = Array.from({ length: 12 }, (_, i) => i);
  readonly placeholderCol = Array.from({ length: 7 }, (_, i) => i);

  readonly weeks = computed<Week[]>(() => {
    const map = new Map<string, number>();
    for (const d of this.data.value() ?? []) map.set(d.dateUtc, d.count);

    const today = startOfUtcDay(new Date());
    const todayKey = isoDate(today);
    const start = addDays(today, -(DAYS_BACK - 1));
    const startDow = (start.getUTCDay() + 6) % 7;
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
