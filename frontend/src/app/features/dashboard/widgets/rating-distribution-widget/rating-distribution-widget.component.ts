import { ChangeDetectionStrategy, Component, computed, inject, resource } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { ApiClient } from '../../../../core/api/api-client';

interface Row {
  label: string;
  count: number;
  pct: number;
  color: string;
}

@Component({
  selector: 'app-rating-distribution-widget',
  standalone: true,
  templateUrl: './rating-distribution-widget.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
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
      this.row('Again', d.forgot, d.total, 'var(--color-rating-again)'),
      this.row('Hard', d.hard, d.total, 'var(--color-rating-hard)'),
      this.row('Good', d.good, d.total, 'var(--color-rating-good)'),
      this.row('Easy', d.easy, d.total, 'var(--color-rating-easy)'),
    ];
  });

  private row(label: string, count: number, total: number, color: string): Row {
    return { label, count, pct: Math.round((count / total) * 100), color };
  }
}
