import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { ApiClient } from '../../core/api/api-client';
import {
  CardDto,
  DueReminderDto,
  GradingResult,
  Rating,
} from '../../core/api/dto';
import { IconComponent, type IconName } from '../../core/ui/icon.component';

type Phase =
  | 'loading-queue'
  | 'loading-card'
  | 'note-question'
  | 'note-revealed'
  | 'question-prompt'
  | 'question-grading'
  | 'question-graded'
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
  template: `
    <div class="px-4 md:px-8 py-6 md:py-8 max-w-3xl mx-auto">
      <header class="mb-6 flex items-end justify-between gap-4">
        <div>
          <h1 class="text-2xl font-semibold text-fg tracking-tight">Practice</h1>
          <p class="text-sm text-fg-muted mt-1">
            Review the cards due today, one at a time.
          </p>
        </div>
        @if (queue().length > 0 && phase() !== 'done') {
          <div class="text-xs text-fg-muted whitespace-nowrap tabular-nums px-2.5 py-1 rounded-md bg-surface border border-default">
            {{ position() }} / {{ queue().length }}
          </div>
        }
      </header>

      @if (phase() === 'loading-queue' || phase() === 'loading-card') {
        <div class="bg-surface border border-default rounded-xl p-6 space-y-3">
          <div class="skeleton h-5 w-1/3"></div>
          <div class="skeleton h-6 w-3/4"></div>
          <div class="skeleton h-24 w-full"></div>
        </div>
      } @else if (phase() === 'done') {
        <div class="bg-surface border border-default rounded-xl shadow-card p-10 text-center">
          @if (queue().length === 0) {
            <div class="inline-flex w-14 h-14 rounded-full items-center justify-center mb-4"
                 [style.background]="'color-mix(in srgb, var(--color-brand-500) 14%, transparent)'">
              <app-icon name="check-circle" [size]="28" class="text-brand" />
            </div>
            <p class="text-lg font-semibold text-fg">Nothing due today.</p>
            <p class="text-sm text-fg-muted mt-1">Enjoy your day — come back tomorrow.</p>
          } @else {
            <div class="inline-flex w-14 h-14 rounded-full items-center justify-center mb-4"
                 [style.background]="'color-mix(in srgb, var(--color-brand-500) 14%, transparent)'">
              <app-icon name="party-popper" [size]="28" class="text-brand" />
            </div>
            <p class="text-lg font-semibold text-fg">All done.</p>
            <p class="text-sm text-fg-muted mt-1">
              Reviewed <span class="text-fg font-medium">{{ reviewedCount() }}</span> card{{ reviewedCount() === 1 ? '' : 's' }},
              skipped <span class="text-fg font-medium">{{ skippedCount() }}</span>.
            </p>
          }
          <a routerLink="/" class="inline-flex items-center gap-1.5 mt-6 text-sm text-brand hover:underline">
            <app-icon name="arrow-left" [size]="14" />
            Back to dashboard
          </a>
        </div>
      } @else if (current(); as cur) {
        <article class="bg-surface border border-default rounded-xl shadow-card p-5 md:p-7">
          <div class="flex items-center gap-2 mb-4">
            <span
              class="inline-flex items-center gap-1 text-[11px] font-medium tracking-wide"
              [class]="cur.card.type === 'Question' ? 'chip-question' : 'chip-note'"
            >
              <app-icon [name]="cur.card.type === 'Question' ? 'help-circle' : 'file-text'" [size]="11" />
              {{ cur.card.type }}
            </span>
            <span class="text-[11px] text-fg-muted">stage {{ cur.reminder.stageNumber }}</span>
          </div>

          <h2 class="text-lg md:text-xl font-semibold text-fg leading-snug mb-3">{{ cur.card.title }}</h2>

          @if (cur.card.tags.length > 0) {
            <div class="mb-5 flex flex-wrap gap-1.5">
              @for (t of cur.card.tags; track t) {
                <span class="tag-pill">#{{ t }}</span>
              }
            </div>
          }

          <!-- ===== Note flow ===== -->
          @if (phase() === 'note-question') {
            <p class="text-sm text-fg-secondary italic">
              Recall the answer, then reveal it to rate yourself.
            </p>
            <div class="mt-5 flex flex-wrap items-center gap-2">
              <button
                type="button"
                (click)="onReveal()"
                [disabled]="actionBusy()"
                class="h-10 px-4 rounded-md text-sm font-medium bg-brand text-brand-on hover:bg-brand-hover disabled:opacity-50 inline-flex items-center gap-2"
              >
                <app-icon name="eye" [size]="16" />
                Show answer
              </button>
              <button
                type="button"
                (click)="onSkip()"
                [disabled]="actionBusy()"
                class="h-10 px-4 rounded-md text-sm border border-default text-fg-secondary hover:bg-surface-hover hover:text-fg disabled:opacity-40 inline-flex items-center gap-2"
              >
                <app-icon name="skip-forward" [size]="14" />
                Skip
              </button>
            </div>
          }

          @if (phase() === 'note-revealed') {
            <div class="mt-4 p-4 rounded-md bg-surface-raised border border-default whitespace-pre-wrap text-sm text-fg font-mono">{{ cur.card.body }}</div>
            <p class="mt-5 text-sm text-fg-secondary">How well did you remember?</p>
            <div class="mt-3 grid grid-cols-2 sm:grid-cols-4 gap-2">
              @for (b of ratingButtons; track b.rating) {
                <button
                  type="button"
                  (click)="onRate(b.rating)"
                  [disabled]="actionBusy()"
                  class="rating-btn"
                  [class]="b.class"
                >
                  <app-icon [name]="b.icon" [size]="16" />
                  <span>{{ b.rating }}</span>
                </button>
              }
            </div>
          }

          <!-- ===== Question flow ===== -->
          @if (phase() === 'question-prompt' || phase() === 'question-grading') {
            <label class="block">
              <span class="block text-xs font-medium text-fg-secondary mb-1.5">Your answer</span>
              <textarea
                [(ngModel)]="userAnswer"
                name="answer"
                rows="6"
                [maxlength]="2000"
                [disabled]="phase() === 'question-grading'"
                class="w-full px-3 py-2 bg-surface-raised border border-default rounded-md text-sm text-fg placeholder:text-fg-muted focus:outline-none focus:ring-2 focus:ring-brand/40 focus:border-brand disabled:opacity-60"
                placeholder="Type your answer. The AI will grade it against the reference."
              ></textarea>
              <span class="mt-1.5 block text-right text-xs text-fg-muted tabular-nums">
                {{ userAnswer.length }} / 2000
              </span>
            </label>
            <div class="mt-3 flex flex-wrap items-center gap-2">
              <button
                type="button"
                (click)="onGrade()"
                [disabled]="actionBusy() || userAnswer.trim().length === 0"
                class="h-10 px-4 rounded-md text-sm font-medium bg-brand text-brand-on hover:bg-brand-hover disabled:opacity-50 inline-flex items-center gap-2"
              >
                @if (phase() === 'question-grading') {
                  <app-icon name="loader" [size]="14" class="animate-spin" />
                  Grading…
                } @else {
                  <app-icon name="sparkles" [size]="14" />
                  Submit answer
                }
              </button>
              <button
                type="button"
                (click)="onSkip()"
                [disabled]="actionBusy()"
                class="h-10 px-4 rounded-md text-sm border border-default text-fg-secondary hover:bg-surface-hover hover:text-fg disabled:opacity-40 inline-flex items-center gap-2"
              >
                <app-icon name="skip-forward" [size]="14" />
                Skip
              </button>
            </div>
          }

          @if (phase() === 'question-graded' && grade(); as g) {
            <div
              class="mt-4 p-4 rounded-lg border"
              [style.color]="verdictColor(g.verdict)"
              [style.background]="verdictBg(g.verdict)"
              [style.border-color]="verdictBorder(g.verdict)"
            >
              <div class="flex items-baseline justify-between gap-3 mb-2">
                <p class="text-sm font-semibold flex items-center gap-2">
                  <app-icon [name]="verdictIcon(g.verdict)" [size]="16" />
                  <span class="tabular-nums">{{ g.score }} / 100</span>
                  <span class="opacity-80">— {{ g.verdict }}</span>
                </p>
                <span class="text-xs opacity-80">suggested: {{ suggestedRating(g.score) }}</span>
              </div>
              <p class="text-sm whitespace-pre-wrap" style="color: var(--color-fg)">{{ g.feedback }}</p>
            </div>

            <details class="mt-3">
              <summary class="text-xs text-fg-muted cursor-pointer hover:text-fg-secondary">Show reference answer</summary>
              <div class="mt-2 p-3 rounded-md bg-surface-raised border border-default whitespace-pre-wrap text-sm text-fg font-mono">{{ cur.card.body }}</div>
            </details>

            <p class="mt-5 text-sm text-fg-secondary">
              Accept the AI rating, or override with your own:
            </p>
            <div class="mt-3 grid grid-cols-2 sm:grid-cols-4 gap-2">
              @for (b of ratingButtons; track b.rating) {
                <button
                  type="button"
                  (click)="onRate(b.rating)"
                  [disabled]="actionBusy()"
                  class="rating-btn relative"
                  [class]="b.class"
                  [class.is-suggested]="suggestedRating(g.score) === b.rating"
                >
                  <app-icon [name]="b.icon" [size]="16" />
                  <span>{{ b.rating }}</span>
                </button>
              }
            </div>
          }

          @if (error()) {
            <p class="mt-4 text-sm text-danger">{{ error() }}</p>
          }
        </article>
      }
    </div>
  `,
})
export class PracticeComponent {
  private readonly api = inject(ApiClient);
  private readonly router = inject(Router);

  readonly phase = signal<Phase>('loading-queue');
  readonly queue = signal<DueReminderDto[]>([]);
  readonly queueIndex = signal(0);
  readonly current = signal<CurrentCard | null>(null);
  readonly grade = signal<GradingResult | null>(null);
  readonly actionBusy = signal(false);
  readonly error = signal<string | null>(null);

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
      const due = await firstValueFrom(this.api.dueToday());
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
    this.error.set(null);

    const reminder = this.queue()[this.queueIndex()];
    try {
      const card = await firstValueFrom(this.api.getCard(reminder.cardId));
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
      await firstValueFrom(this.api.revealReminder(cur.reminder.reminderId));
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
        this.api.gradeAnswer(cur.card.id, this.userAnswer));
      this.grade.set(result);
      this.phase.set('question-graded');
    } catch (e) {
      this.error.set(this.describe(e, 'AI grading is unavailable right now. Try again or skip.'));
      this.phase.set('question-prompt');
    } finally {
      this.actionBusy.set(false);
    }
  }

  async onRate(rating: Rating): Promise<void> {
    const cur = this.current();
    if (!cur) return;
    this.actionBusy.set(true);
    this.error.set(null);
    const prevPhase = this.phase();
    this.phase.set('submitting');

    const g = this.grade();
    const wasGraded = cur.card.type === 'Question' && g !== null;
    const autoGraded = wasGraded && this.suggestedRating(g!.score) === rating;

    try {
      await firstValueFrom(this.api.recordReview(cur.card.id, {
        reminderId: cur.reminder.reminderId,
        rating,
        answerText: wasGraded ? this.userAnswer : null,
        aiScore: wasGraded ? g!.score : null,
        aiFeedback: wasGraded ? g!.feedback : null,
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
      await firstValueFrom(this.api.skipReminder(cur.reminder.reminderId));
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
