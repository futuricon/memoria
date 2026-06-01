import { Component, inject, resource } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { ApiClient } from '../../../core/api/api-client';
import { IconComponent } from '../../../core/ui/icon.component';

@Component({
  selector: 'app-streak-widget',
  standalone: true,
  imports: [IconComponent],
  templateUrl: './streak-widget.component.html',
})
export class StreakWidgetComponent {
  private readonly api = inject(ApiClient);

  readonly data = resource({
    loader: () => firstValueFrom(this.api.streak()),
  });
}
