import { ChangeDetectionStrategy, Component, computed, inject, resource } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { IconComponent } from '../../../../shared/components/icon/icon.component';
import { relativeTime } from '../../../../shared/utils/relative-time';
import { RemindersApiService } from '../../../practice/services/reminders-api.service';

/**
 * Surfaces reminders the bot already delivered via Telegram but which
 * never got Confirmed or Skipped — the "I read it, got distracted, never
 * rated" pile. When non-empty the widget switches into an attention
 * style so the user notices it at a glance; clicking through opens
 * /practice, which loads these reminders ahead of the regular due-today
 * queue.
 */
@Component({
  selector: 'app-pending-ratings-widget',
  standalone: true,
  imports: [IconComponent, RouterLink],
  templateUrl: './pending-ratings-widget.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PendingRatingsWidgetComponent {
  private readonly remindersApi = inject(RemindersApiService);

  readonly data = resource({
    loader: () => firstValueFrom(this.remindersApi.pendingRatings(5)),
  });

  readonly rows = computed(() =>
    (this.data.value() ?? []).map((r) => ({
      ...r,
      waitingFor: relativeTime(r.scheduledAt),
    })),
  );

  readonly hasPending = computed(() => this.rows().length > 0);
}
