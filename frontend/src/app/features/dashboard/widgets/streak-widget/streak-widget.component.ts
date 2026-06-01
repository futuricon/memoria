import { ChangeDetectionStrategy, Component, inject, resource } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { IconComponent } from '../../../../shared/components/icon/icon.component';
import { AnalyticsApiService } from '../../services/analytics-api.service';

@Component({
  selector: 'app-streak-widget',
  standalone: true,
  imports: [IconComponent],
  templateUrl: './streak-widget.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StreakWidgetComponent {
  private readonly api = inject(AnalyticsApiService);

  readonly data = resource({
    loader: () => firstValueFrom(this.api.streak()),
  });
}
