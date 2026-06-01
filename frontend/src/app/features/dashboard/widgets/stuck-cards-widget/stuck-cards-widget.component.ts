import { ChangeDetectionStrategy, Component, inject, resource } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { IconComponent } from '../../../../shared/components/icon/icon.component';
import { AnalyticsApiService } from '../../services/analytics-api.service';

@Component({
  selector: 'app-stuck-cards-widget',
  standalone: true,
  imports: [IconComponent],
  templateUrl: './stuck-cards-widget.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StuckCardsWidgetComponent {
  private readonly api = inject(AnalyticsApiService);

  readonly data = resource({
    loader: () => firstValueFrom(this.api.stuckCards(5)),
  });
}
