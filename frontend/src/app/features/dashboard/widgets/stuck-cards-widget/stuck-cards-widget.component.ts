import { Component, inject, resource } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { ApiClient } from '../../../../core/api/api-client';
import { IconComponent } from '../../../../core/ui/icon/icon.component';

@Component({
  selector: 'app-stuck-cards-widget',
  standalone: true,
  imports: [IconComponent],
  templateUrl: './stuck-cards-widget.component.html',
})
export class StuckCardsWidgetComponent {
  private readonly api = inject(ApiClient);

  readonly data = resource({
    loader: () => firstValueFrom(this.api.stuckCards(5)),
  });
}
