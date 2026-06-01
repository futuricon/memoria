import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, resource, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { ApiClient } from '../../core/api/api-client';
import { GradePillComponent } from '../../core/ui/grade-pill.component';
import { IconComponent } from '../../core/ui/icon.component';
import { relativeTime } from '../../core/ui/relative-time';
import { HardestTagsWidgetComponent } from './widgets/hardest-tags-widget.component';
import { HeatmapWidgetComponent } from './widgets/heatmap-widget.component';
import { RatingDistributionWidgetComponent } from './widgets/rating-distribution-widget.component';
import { StreakWidgetComponent } from './widgets/streak-widget.component';
import { StuckCardsWidgetComponent } from './widgets/stuck-cards-widget.component';

const TELEGRAM_BANNER_DISMISS_KEY = 'memoria.telegramBannerDismissed';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    DecimalPipe,
    GradePillComponent,
    IconComponent,
    RouterLink,
    StreakWidgetComponent,
    RatingDistributionWidgetComponent,
    HeatmapWidgetComponent,
    StuckCardsWidgetComponent,
    HardestTagsWidgetComponent,
  ],
  templateUrl: './dashboard.component.html',
})
export class DashboardComponent {
  private readonly api = inject(ApiClient);

  readonly worstCard = resource({
    loader: async () => {
      const list = await firstValueFrom(this.api.worst(1, 3));
      return list[0] ?? null;
    },
  });

  readonly upcoming = resource({
    loader: () => firstValueFrom(this.api.upcoming(5)),
  });

  readonly dueToday = resource({
    loader: () => firstValueFrom(this.api.dueToday()),
  });

  readonly firstPage = resource({
    loader: () => firstValueFrom(this.api.listCards({ page: 1, pageSize: 20 })),
  });

  readonly accountAverage = computed<number | null>(() => {
    const page = this.firstPage.value();
    if (!page || page.items.length === 0) return null;
    const scored = page.items
      .map((c) => (c.type === 'Question' && c.avgAiScore !== null ? c.avgAiScore : c.avgRating))
      .filter((v): v is number => v !== null);
    if (scored.length === 0) return null;
    return scored.reduce((a, b) => a + b, 0) / scored.length;
  });

  readonly identities = resource({
    loader: () => firstValueFrom(this.api.getIdentities()),
  });

  readonly bannerDismissed = signal<boolean>(
    typeof localStorage !== 'undefined'
      && localStorage.getItem(TELEGRAM_BANNER_DISMISS_KEY) === 'true',
  );

  readonly showTelegramBanner = computed<boolean>(() => {
    if (this.bannerDismissed()) return false;
    const list = this.identities.value();
    if (!list) return false;
    return !list.some((i) => i.provider === 'Telegram');
  });

  dismissTelegramBanner(): void {
    this.bannerDismissed.set(true);
    try {
      localStorage.setItem(TELEGRAM_BANNER_DISMISS_KEY, 'true');
    } catch {
      /* ignored */
    }
  }

  relTime(iso: string): string {
    return relativeTime(iso);
  }
}
