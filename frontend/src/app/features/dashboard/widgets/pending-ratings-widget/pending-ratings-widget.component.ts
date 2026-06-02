import { ChangeDetectionStrategy, Component, computed, inject, resource, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { IconComponent, type IconName } from '../../../../shared/components/icon/icon.component';
import { relativeTime } from '../../../../shared/utils/relative-time';
import { RemindersApiService } from '../../../practice/services/reminders-api.service';
import { ReviewsApiService } from '../../../practice/services/reviews-api.service';
import type { Rating } from '../../../practice/models/review.model';

interface RatingButton {
  readonly rating: Rating;
  readonly icon: IconName;
  readonly cssClass: string;
}

/**
 * Surfaces reminders the bot already delivered to the user via Telegram
 * but which never got Confirmed or Skipped — the "I read it, got
 * distracted, never rated" pile. Lets the user clear them inline with one
 * click instead of going back to the Telegram message.
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
  private readonly reviewsApi = inject(ReviewsApiService);

  /** Per-reminder grading-in-flight flag — disables that row's buttons. */
  readonly busyReminderId = signal<string | null>(null);
  /** Surfaces last error inline (rare; e.g. network drop). */
  readonly error = signal<string | null>(null);

  readonly data = resource({
    loader: () => firstValueFrom(this.remindersApi.pendingRatings(5)),
  });

  readonly rows = computed(() =>
    (this.data.value() ?? []).map((r) => ({
      ...r,
      waitingFor: relativeTime(r.scheduledAt),
    })),
  );

  readonly ratingButtons: ReadonlyArray<RatingButton> = [
    { rating: 'Forgot', icon: 'x-circle', cssClass: 'text-danger hover:bg-danger/10' },
    { rating: 'Hard', icon: 'circle-help', cssClass: 'text-warning hover:bg-warning/10' },
    { rating: 'Good', icon: 'check', cssClass: 'text-brand hover:bg-brand/10' },
    { rating: 'Easy', icon: 'sparkles', cssClass: 'text-success hover:bg-success/10' },
  ];

  async grade(cardId: string, reminderId: string, rating: Rating): Promise<void> {
    if (this.busyReminderId() !== null) return;
    this.busyReminderId.set(reminderId);
    this.error.set(null);
    try {
      await firstValueFrom(
        this.reviewsApi.recordReview(cardId, { reminderId, rating }),
      );
      // Refetch the list — the just-rated reminder will fall out of the
      // Sent bucket on the server side.
      this.data.reload();
    } catch {
      this.error.set('Failed to record rating. Try again.');
    } finally {
      this.busyReminderId.set(null);
    }
  }
}
