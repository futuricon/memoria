import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, resource } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { IconComponent } from '../../../../core/ui/icon/icon.component';
import { AnalyticsApiService } from '../../services/analytics-api.service';

function bandColor(score: number): string {
  if (score >= 75) return 'var(--color-rating-good)';
  if (score >= 40) return 'var(--color-rating-hard)';
  return 'var(--color-rating-again)';
}

function bandBackground(score: number): string {
  return `color-mix(in srgb, ${bandColor(score)} 14%, transparent)`;
}

@Component({
  selector: 'app-hardest-tags-widget',
  standalone: true,
  imports: [DecimalPipe, IconComponent],
  templateUrl: './hardest-tags-widget.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HardestTagsWidgetComponent {
  private readonly api = inject(AnalyticsApiService);

  readonly data = resource({
    loader: () => firstValueFrom(this.api.tagAverages(5, 3)),
  });

  band(score: number): string {
    return bandColor(score);
  }

  bandBg(score: number): string {
    return bandBackground(score);
  }
}
