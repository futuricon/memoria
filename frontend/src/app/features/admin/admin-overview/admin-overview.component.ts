import { DecimalPipe, PercentPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, resource } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { IconComponent } from '../../../shared/components/icon/icon.component';
import { AdminApiService } from '../services/admin-api.service';

@Component({
  selector: 'app-admin-overview',
  standalone: true,
  imports: [DecimalPipe, PercentPipe, IconComponent, RouterLink],
  templateUrl: './admin-overview.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminOverviewComponent {
  private readonly api = inject(AdminApiService);

  readonly overview = resource({
    loader: () => firstValueFrom(this.api.overview()),
  });

  // Funnel conversion ratios — denominators differ at each step, so we
  // compute them once here instead of repeating math in the template.
  readonly funnelRates = computed(() => {
    const f = this.overview.value()?.activationFunnel;
    if (!f || f.signups === 0) return null;
    return {
      linkedRate: f.telegramLinked / f.signups,
      cardRate: f.hasCard / f.signups,
      reviewRate: f.hasReview / f.signups,
    };
  });

  readonly retentionRates = computed(() => {
    const r = this.overview.value()?.retention;
    if (!r || r.signups === 0) return null;
    return {
      d1: r.d1Retained / r.signups,
      d7: r.d7Retained / r.signups,
      d30: r.d30Retained / r.signups,
    };
  });

  readonly ratingShares = computed(() => {
    const g = this.overview.value()?.globalRatings;
    if (!g || g.total === 0) return null;
    return {
      forgot: g.forgot / g.total,
      hard: g.hard / g.total,
      good: g.good / g.total,
      easy: g.easy / g.total,
    };
  });

  readonly skipShares = computed(() => {
    const s = this.overview.value()?.reminderSkipRate;
    if (!s || s.total === 0) return null;
    return {
      confirmedRate: s.confirmed / s.total,
      skippedRate: s.skipped / s.total,
      sentRate: s.sent / s.total,
      failedRate: s.failed / s.total,
    };
  });
}
