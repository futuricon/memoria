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

type Phase =
  | 'loading-queue'
  | 'loading-card'
  | 'note-question'    // Note: title shown, awaiting "Show answer"
  | 'note-revealed'    // Note: answer shown, awaiting rating
  | 'question-prompt'  // Question: title shown, textarea + Submit
  | 'question-grading' // Question: AI grading in flight
  | 'question-graded'  // Question: AI result shown, awaiting accept/override
  | 'submitting'       // POST review in flight
  | 'done';            // all reminders handled

interface CurrentCard {
  reminder: DueReminderDto;
  card: CardDto;
}

@Component({
  selector: 'app-practice',
  standalone: true,
  imports: [FormsModule, RouterLink],
  template: `
    <header class="mb-6 flex items-end justify-between gap-4">
      <div>
        <h1 class="text-2xl font-semibold">Practice</h1>
        <p class="text-sm text-slate-500">
          Review the cards due today, one at a time.
        </p>
      </div>
      @if (queue().length > 0) {
        <div class="text-sm text-slate-500 whitespace-nowrap">
          {{ position() }} of {{ queue().length }}
        </div>
      }
    </header>

    @if (phase() === 'loading-queue' || phase() === 'loading-card') {
      <p class="text-sm text-slate-400">Loading…</p>
    } @else if (phase() === 'done') {
      <div class="bg-white border border-slate-200 rounded-lg p-8 text-center">
        @if (queue().length === 0) {
          <p class="text-slate-500">Nothing due today. Enjoy your day.</p>
        } @else {
          <p class="text-2xl mb-2">🎉</p>
          <p class="font-medium text-slate-900">All done.</p>
          <p class="text-sm text-slate-500 mt-1">
            Reviewed {{ reviewedCount() }} card{{ reviewedCount() === 1 ? '' : 's' }}, skipped {{ skippedCount() }}.
          </p>
        }
        <a routerLink="/" class="inline-block mt-4 text-sm text-slate-600 hover:text-slate-900">
          ← Back to dashboard
        </a>
      </div>
    } @else if (current(); as cur) {
      <div class="bg-white border border-slate-200 rounded-lg p-6 max-w-2xl">
        <div class="flex items-center gap-2 mb-3">
          <span
            class="text-xs px-2 py-0.5 rounded font-medium"
            [class.bg-blue-100]="cur.card.type === 'Question'"
            [class.text-blue-700]="cur.card.type === 'Question'"
            [class.bg-slate-100]="cur.card.type === 'Note'"
            [class.text-slate-700]="cur.card.type === 'Note'"
          >{{ cur.card.type === 'Question' ? '❓ Question' : '📝 Note' }}</span>
          <span class="text-xs text-slate-400">stage {{ cur.reminder.stageNumber }}</span>
        </div>

        <h2 class="text-lg font-semibold text-slate-900 mb-3">{{ cur.card.title }}</h2>

        @if (cur.card.tags.length > 0) {
          <div class="mb-4 flex flex-wrap gap-1">
            @for (t of cur.card.tags; track t) {
              <span class="text-xs text-slate-500">#{{ t }}</span>
            }
          </div>
        }

        <!-- ===== Note flow ===== -->
        @if (phase() === 'note-question') {
          <p class="text-sm text-slate-600 italic">
            Recall the answer, then reveal it to rate yourself.
          </p>
          <div class="mt-5 flex items-center gap-2">
            <button
              type="button"
              (click)="onReveal()"
              [disabled]="actionBusy()"
              class="px-3 py-1.5 text-sm rounded bg-slate-900 text-white hover:bg-slate-800 disabled:opacity-50"
            >📖 Show answer</button>
            <button
              type="button"
              (click)="onSkip()"
              [disabled]="actionBusy()"
              class="px-3 py-1.5 text-sm rounded border border-slate-300 bg-white hover:bg-slate-100 disabled:opacity-40"
            >⏭ Skip</button>
          </div>
        }

        @if (phase() === 'note-revealed') {
          <div class="mt-4 p-4 rounded bg-slate-50 border border-slate-200 whitespace-pre-wrap text-sm text-slate-800 font-mono">{{ cur.card.body }}</div>
          <p class="mt-4 text-sm text-slate-600">How well did you remember?</p>
          <div class="mt-2 grid grid-cols-4 gap-2">
            <button
              type="button"
              (click)="onRate('Forgot')"
              [disabled]="actionBusy()"
              class="px-2 py-2 text-sm rounded border border-rose-200 bg-white hover:bg-rose-50 text-rose-700 disabled:opacity-40"
            >😕 Forgot</button>
            <button
              type="button"
              (click)="onRate('Hard')"
              [disabled]="actionBusy()"
              class="px-2 py-2 text-sm rounded border border-amber-200 bg-white hover:bg-amber-50 text-amber-700 disabled:opacity-40"
            >🤔 Hard</button>
            <button
              type="button"
              (click)="onRate('Good')"
              [disabled]="actionBusy()"
              class="px-2 py-2 text-sm rounded border border-emerald-200 bg-white hover:bg-emerald-50 text-emerald-700 disabled:opacity-40"
            >🙂 Good</button>
            <button
              type="button"
              (click)="onRate('Easy')"
              [disabled]="actionBusy()"
              class="px-2 py-2 text-sm rounded border border-sky-200 bg-white hover:bg-sky-50 text-sky-700 disabled:opacity-40"
            >😎 Easy</button>
          </div>
        }

        <!-- ===== Question flow ===== -->
        @if (phase() === 'question-prompt' || phase() === 'question-grading') {
          <label class="block text-sm">
            <span class="text-slate-600">Your answer</span>
            <textarea
              [(ngModel)]="userAnswer"
              name="answer"
              rows="6"
              [maxlength]="2000"
              [disabled]="phase() === 'question-grading'"
              class="mt-1 w-full px-3 py-2 border border-slate-300 rounded focus:outline-none focus:ring-2 focus:ring-slate-400 disabled:bg-slate-50"
              placeholder="Type your answer. The AI will grade it against the reference."
            ></textarea>
            <span class="mt-1 block text-right text-xs text-slate-400">
              {{ userAnswer.length }} / 2000
            </span>
          </label>
          <div class="mt-3 flex items-center gap-2">
            <button
              type="button"
              (click)="onGrade()"
              [disabled]="actionBusy() || userAnswer.trim().length === 0"
              class="px-3 py-1.5 text-sm rounded bg-slate-900 text-white hover:bg-slate-800 disabled:opacity-50"
            >{{ phase() === 'question-grading' ? 'Grading…' : 'Submit answer' }}</button>
            <button
              type="button"
              (click)="onSkip()"
              [disabled]="actionBusy()"
              class="px-3 py-1.5 text-sm rounded border border-slate-300 bg-white hover:bg-slate-100 disabled:opacity-40"
            >⏭ Skip</button>
          </div>
        }

        @if (phase() === 'question-graded' && grade(); as g) {
          <div
            class="mt-4 p-4 rounded border"
            [class.bg-emerald-50]="g.verdict === 'Correct'"
            [class.border-emerald-200]="g.verdict === 'Correct'"
            [class.bg-amber-50]="g.verdict === 'Partial'"
            [class.border-amber-200]="g.verdict === 'Partial'"
            [class.bg-rose-50]="g.verdict === 'Incorrect'"
            [class.border-rose-200]="g.verdict === 'Incorrect'"
          >
            <div class="flex items-baseline justify-between gap-3 mb-2">
              <p class="text-sm font-semibold">
                {{ verdictIcon(g.verdict) }} {{ g.score }} / 100 — {{ g.verdict }}
              </p>
              <span class="text-xs text-slate-500">suggested: {{ suggestedRating(g.score) }}</span>
            </div>
            <p class="text-sm text-slate-700 whitespace-pre-wrap">{{ g.feedback }}</p>
          </div>

          <details class="mt-3">
            <summary class="text-xs text-slate-500 cursor-pointer">Show reference answer</summary>
            <div class="mt-2 p-3 rounded bg-slate-50 border border-slate-200 whitespace-pre-wrap text-sm text-slate-700 font-mono">{{ cur.card.body }}</div>
          </details>

          <p class="mt-4 text-sm text-slate-600">
            Accept the AI rating, or override with your own:
          </p>
          <div class="mt-2 grid grid-cols-4 gap-2">
            <button
              type="button"
              (click)="onRate('Forgot')"
              [disabled]="actionBusy()"
              [class.ring-2]="suggestedRating(g.score) === 'Forgot'"
              [class.ring-rose-400]="suggestedRating(g.score) === 'Forgot'"
              class="px-2 py-2 text-sm rounded border border-rose-200 bg-white hover:bg-rose-50 text-rose-700 disabled:opacity-40"
            >😕 Forgot</button>
            <button
              type="button"
              (click)="onRate('Hard')"
              [disabled]="actionBusy()"
              [class.ring-2]="suggestedRating(g.score) === 'Hard'"
              [class.ring-amber-400]="suggestedRating(g.score) === 'Hard'"
              class="px-2 py-2 text-sm rounded border border-amber-200 bg-white hover:bg-amber-50 text-amber-700 disabled:opacity-40"
            >🤔 Hard</button>
            <button
              type="button"
              (click)="onRate('Good')"
              [disabled]="actionBusy()"
              [class.ring-2]="suggestedRating(g.score) === 'Good'"
              [class.ring-emerald-400]="suggestedRating(g.score) === 'Good'"
              class="px-2 py-2 text-sm rounded border border-emerald-200 bg-white hover:bg-emerald-50 text-emerald-700 disabled:opacity-40"
            >🙂 Good</button>
            <button
              type="button"
              (click)="onRate('Easy')"
              [disabled]="actionBusy()"
              [class.ring-2]="suggestedRating(g.score) === 'Easy'"
              [class.ring-sky-400]="suggestedRating(g.score) === 'Easy'"
              class="px-2 py-2 text-sm rounded border border-sky-200 bg-white hover:bg-sky-50 text-sky-700 disabled:opacity-40"
            >😎 Easy</button>
          </div>
        }

        @if (error()) {
          <p class="mt-3 text-sm text-rose-600">{{ error() }}</p>
        }
      </div>
    }
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
      // Skip to next instead of getting stuck.
      this.advance();
    }
  }

  async onReveal(): Promise<void> {
    const cur = this.current();
    if (!cur) return;
    this.actionBusy.set(true);
    this.error.set(null);
    try {
      // Reveal returns the body which we already have on the loaded card;
      // call the endpoint anyway so server-side validation runs (status
      // check, ownership) — this surfaces forbidden / not-found cleanly.
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
    // Mirrors AnswerRatingMapper on the backend.
    if (score >= 85) return 'Easy';
    if (score >= 65) return 'Good';
    if (score >= 40) return 'Hard';
    return 'Forgot';
  }

  verdictIcon(v: GradingResult['verdict']): string {
    if (v === 'Correct') return '✅';
    if (v === 'Partial') return '🟡';
    return '❌';
  }

  private describe(e: unknown, fallback: string): string {
    if (e && typeof e === 'object' && 'error' in e) {
      const err = (e as { error?: { message?: string } }).error;
      if (err?.message) return err.message;
    }
    return fallback;
  }
}
