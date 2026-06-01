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
  templateUrl: './heatmap-widget.component.html',
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
