import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import {
  CardDto,
  DueReminderDto,
  GradingResult,
  Rating,
} from '../../core/api/dto';
import { IconComponent, type IconName } from '../../core/ui/icon/icon.component';
import { CardsApiService } from '../cards/services/cards-api.service';
import { RemindersApiService } from './services/reminders-api.service';
import { ReviewsApiService } from './services/reviews-api.service';

type Phase =
  | 'loading-queue'
  | 'loading-card'
  | 'note-question'
  | 'note-revealed'
  | 'question-prompt'
  | 'question-grading'
  | 'question-graded'
  | 'question-self-grade'
  | 'submitting'
  | 'done';

interface CurrentCard {
  reminder: DueReminderDto;
  card: CardDto;
}

@Component({
  selector: 'app-practice',
  standalone: true,
  imports: [FormsModule, RouterLink, IconComponent],
  templateUrl: './practice.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PracticeComponent {
  private readonly cardsApi = inject(CardsApiService);
  private readonly remindersApi = inject(RemindersApiService);
  private readonly reviewsApi = inject(ReviewsApiService);
  private readonly router = inject(Router);

  readonly phase = signal<Phase>('loading-queue');
  readonly queue = signal<DueReminderDto[]>([]);
  readonly queueIndex = signal(0);
  readonly current = signal<CurrentCard | null>(null);
  readonly grade = signal<GradingResult | null>(null);
  readonly actionBusy = signal(false);
  readonly error = signal<string | null>(null);
  // Whether we landed in question-self-grade because AI threw, vs the user
  // opting out via "Grade myself". Drives the in-card warning banner.
  readonly aiFailed = signal(false);

  readonly reviewedCount = signal(0);
  readonly skippedCount = signal(0);

  readonly position = computed(() => this.queueIndex() + 1);

  userAnswer = '';

  readonly ratingButtons: Array<{ rating: Rating; icon: IconName; class: string }> = [
    { rating: 'Forgot', icon: 'x-circle', class: 'rating-again' },
    { rating: 'Hard', icon: 'circle-help', class: 'rating-hard' },
    { rating: 'Good', icon: 'check', class: 'rating-good' },
    { rating: 'Easy', icon: 'sparkles', class: 'rating-easy' },
  ];

  constructor() {
    void this.loadQueue();
  }

  async loadQueue(): Promise<void> {
    this.phase.set('loading-queue');
    this.error.set(null);
    try {
      const due = await firstValueFrom(this.remindersApi.dueToday());
      this.queue.set(due);
      this.queueIndex.set(0);
      if (due.length === 0) {
        this.phase.set('done');
      } else {
        await this.loadCurrent();
      }
    } catch (e) {
      this.error.set(this.describe(e, 'Could not load your due reminders.'));
      this.phase.set('done');
    }
  }

  private async loadCurrent(): Promise<void> {
    this.phase.set('loading-card');
    this.userAnswer = '';
    this.grade.set(null);
    this.aiFailed.set(false);
    this.error.set(null);

    const reminder = this.queue()[this.queueIndex()];
    try {
      const card = await firstValueFrom(this.cardsApi.getCard(reminder.cardId));
      this.current.set({ reminder, card });
      this.phase.set(card.type === 'Question' ? 'question-prompt' : 'note-question');
    } catch (e) {
      this.error.set(this.describe(e, 'Could not load the card.'));
      this.advance();
    }
  }

  async onReveal(): Promise<void> {
    const cur = this.current();
    if (!cur) return;
    this.actionBusy.set(true);
    this.error.set(null);
    try {
      await firstValueFrom(this.remindersApi.revealReminder(cur.reminder.reminderId));
      this.phase.set('note-revealed');
    } catch (e) {
      this.error.set(this.describe(e, 'Could not reveal the answer.'));
    } finally {
      this.actionBusy.set(false);
    }
  }

  async onGrade(): Promise<void> {
    const cur = this.current();
    if (!cur || this.userAnswer.trim().length === 0) return;
    this.actionBusy.set(true);
    this.error.set(null);
    this.phase.set('question-grading');
    try {
      const result = await firstValueFrom(
        this.reviewsApi.gradeAnswer(cur.card.id, this.userAnswer));
      this.grade.set(result);
      this.aiFailed.set(false);
      this.phase.set('question-graded');
    } catch (e) {
      // Don't strand the user — drop into self-grading with the reference
      // answer revealed. They can still record a review (without an AI
      // score) or hit "Try AI again" later.
      this.grade.set(null);
      this.aiFailed.set(true);
      this.error.set(this.describe(e, 'AI grading is unavailable right now.'));
      this.phase.set('question-self-grade');
    } finally {
      this.actionBusy.set(false);
    }
  }

  /** User chose to skip AI grading and grade themselves. */
  onGradeMyself(): void {
    this.grade.set(null);
    this.aiFailed.set(false);
    this.error.set(null);
    this.phase.set('question-self-grade');
  }

  async onRate(rating: Rating): Promise<void> {
    const cur = this.current();
    if (!cur) return;
    this.actionBusy.set(true);
    this.error.set(null);
    const prevPhase = this.phase();
    this.phase.set('submitting');

    const g = this.grade();
    const isQuestion = cur.card.type === 'Question';
    const typedAnswer = this.userAnswer.trim().length > 0;
    // For Question cards we always capture what the user typed (even if AI
    // never ran). aiScore/aiFeedback are populated only when AI returned a
    // grade; autoGraded is true only if the user accepted that suggestion.
    const aiGraded = isQuestion && g !== null;
    const autoGraded = aiGraded && this.suggestedRating(g!.score) === rating;

    try {
      await firstValueFrom(this.reviewsApi.recordReview(cur.card.id, {
        reminderId: cur.reminder.reminderId,
        rating,
        answerText: isQuestion && typedAnswer ? this.userAnswer : null,
        aiScore: aiGraded ? g!.score : null,
        aiFeedback: aiGraded ? g!.feedback : null,
        autoGraded,
      }));
      this.reviewedCount.update((n) => n + 1);
      this.advance();
    } catch (e) {
      this.error.set(this.describe(e, 'Could not record your review.'));
      this.phase.set(prevPhase);
    } finally {
      this.actionBusy.set(false);
    }
  }

  async onSkip(): Promise<void> {
    const cur = this.current();
    if (!cur) return;
    this.actionBusy.set(true);
    this.error.set(null);
    try {
      await firstValueFrom(this.remindersApi.skipReminder(cur.reminder.reminderId));
      this.skippedCount.update((n) => n + 1);
      this.advance();
    } catch (e) {
      this.error.set(this.describe(e, 'Could not skip the reminder.'));
    } finally {
      this.actionBusy.set(false);
    }
  }

  private advance(): void {
    const next = this.queueIndex() + 1;
    if (next >= this.queue().length) {
      this.phase.set('done');
      this.current.set(null);
      return;
    }
    this.queueIndex.set(next);
    void this.loadCurrent();
  }

  suggestedRating(score: number): Rating {
    if (score >= 85) return 'Easy';
    if (score >= 65) return 'Good';
    if (score >= 40) return 'Hard';
    return 'Forgot';
  }

  verdictIcon(v: GradingResult['verdict']): IconName {
    if (v === 'Correct') return 'check-circle';
    if (v === 'Partial') return 'info';
    return 'x-circle';
  }

  verdictColor(v: GradingResult['verdict']): string {
    if (v === 'Correct') return 'var(--color-rating-good)';
    if (v === 'Partial') return 'var(--color-rating-hard)';
    return 'var(--color-rating-again)';
  }

  verdictBg(v: GradingResult['verdict']): string {
    return `color-mix(in srgb, ${this.verdictColor(v)} 12%, transparent)`;
  }

  verdictBorder(v: GradingResult['verdict']): string {
    return `color-mix(in srgb, ${this.verdictColor(v)} 35%, transparent)`;
  }

  private describe(e: unknown, fallback: string): string {
    if (e && typeof e === 'object' && 'error' in e) {
      const err = (e as { error?: { message?: string } }).error;
      if (err?.message) return err.message;
    }
    return fallback;
  }
}
