import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, resource, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { ApiClient } from '../../core/api/api-client';
import { GradePillComponent } from '../../core/ui/grade-pill.component';
import { relativeTime } from '../../core/ui/relative-time';

const TELEGRAM_BANNER_DISMISS_KEY = 'memoria.telegramBannerDismissed';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [DecimalPipe, GradePillComponent, RouterLink],
  template: `
    <header class="mb-6">
      <h1 class="text-2xl font-semibold">Dashboard</h1>
      <p class="text-sm text-slate-500">A snapshot of what your brain wants next.</p>
    </header>

    @if (showTelegramBanner()) {
      <div class="mb-6 flex items-start gap-3 p-4 rounded-lg border border-amber-200 bg-amber-50 text-sm">
        <span class="text-lg leading-none">💬</span>
        <div class="flex-1">
          <div class="font-medium text-amber-900">Already use &#64;memoria_bot?</div>
          <p class="text-amber-800 mt-0.5">
            Link your Telegram in
            <a routerLink="/settings" class="underline font-medium">Settings</a>
            — we'll merge your existing bot data into this account automatically.
          </p>
        </div>
        <button
          type="button"
          (click)="dismissTelegramBanner()"
          class="text-amber-700 hover:text-amber-900 text-xs underline"
        >Dismiss</button>
      </div>
    }

    <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
      <section class="bg-white border border-slate-200 rounded-lg p-5">
        <h2 class="text-sm font-medium text-slate-500 uppercase tracking-wide mb-3">
          Hardest card
        </h2>
        @if (worstCard.isLoading()) {
          <p class="text-sm text-slate-400">Loading…</p>
        } @else if (worstCard.error()) {
          <p class="text-sm text-rose-600">Failed to load.</p>
        } @else {
          @let card = worstCard.value();
          @if (card) {
            <a [routerLink]="['/cards']" class="block">
              <div class="flex items-start justify-between gap-3">
                <div class="font-medium text-slate-900 truncate">{{ card.title }}</div>
                <app-grade-pill
                  [type]="card.type"
                  [avgRating]="card.avgRating"
                  [avgAiScore]="card.avgAiScore"
                  [reviewCount]="card.reviewCount"
                />
              </div>
              <p class="text-xs text-slate-500 mt-1">
                {{ card.reviewCount }} review{{ card.reviewCount === 1 ? '' : 's' }}
              </p>
            </a>
          } @else {
            <p class="text-sm text-slate-400">
              Need at least 3 reviews on a card to rank it.
            </p>
          }
        }
      </section>

      <section class="bg-white border border-slate-200 rounded-lg p-5">
        <h2 class="text-sm font-medium text-slate-500 uppercase tracking-wide mb-3">
          Due today
        </h2>
        @if (dueToday.isLoading()) {
          <p class="text-sm text-slate-400">Loading…</p>
        } @else if (dueToday.error()) {
          <p class="text-sm text-rose-600">Failed to load.</p>
        } @else {
          @let count = dueToday.value()?.length ?? 0;
          <p class="text-3xl font-semibold text-slate-900">{{ count }}</p>
          <p class="text-sm text-slate-500 mt-1">
            {{ count === 0 ? 'Nothing scheduled — enjoy your day.' : 'cards waiting' }}
          </p>
        }
      </section>

      <section class="bg-white border border-slate-200 rounded-lg p-5 md:col-span-2">
        <h2 class="text-sm font-medium text-slate-500 uppercase tracking-wide mb-3">
          Coming up
        </h2>
        @if (upcoming.isLoading()) {
          <p class="text-sm text-slate-400">Loading…</p>
        } @else if (upcoming.error()) {
          <p class="text-sm text-rose-600">Failed to load.</p>
        } @else {
          @let items = upcoming.value() ?? [];
          @if (items.length === 0) {
            <p class="text-sm text-slate-400">No pending reminders.</p>
          } @else {
            <ul class="divide-y divide-slate-100">
              @for (r of items; track r.reminderId) {
                <li class="py-2 flex items-center justify-between gap-3">
                  <span class="text-sm text-slate-900 truncate">{{ r.cardTitle }}</span>
                  <span class="text-xs text-slate-500 whitespace-nowrap">
                    stage {{ r.stageNumber }} · {{ relTime(r.scheduledAt) }}
                  </span>
                </li>
              }
            </ul>
          }
        }
      </section>

      <section class="bg-white border border-slate-200 rounded-lg p-5 md:col-span-2">
        <h2 class="text-sm font-medium text-slate-500 uppercase tracking-wide mb-3">
          Library
        </h2>
        @if (firstPage.isLoading()) {
          <p class="text-sm text-slate-400">Loading…</p>
        } @else if (firstPage.error()) {
          <p class="text-sm text-rose-600">Failed to load.</p>
        } @else {
          <div class="flex items-baseline gap-6">
            <div>
              <p class="text-3xl font-semibold text-slate-900">
                {{ firstPage.value()?.totalCount ?? 0 }}
              </p>
              <p class="text-sm text-slate-500">total cards</p>
            </div>
            <div>
              <p class="text-3xl font-semibold text-slate-900">
                @if (accountAverage() !== null) {
                  {{ accountAverage()! | number: '1.0-0' }}
                } @else {
                  —
                }
              </p>
              <p class="text-sm text-slate-500">avg score on this page</p>
            </div>
            <a routerLink="/cards" class="ml-auto text-sm text-slate-600 hover:text-slate-900">
              Browse →
            </a>
          </div>
        }
      </section>
    </div>
  `,
})
export class DashboardComponent {
  private readonly api = inject(ApiClient);

  readonly worstCard = resource({
    loader: async () => {
      const list = await firstValueFrom(this.api.worst(1, 3));
      return list[0] ?? null;
    },
  });

  readonly upcoming = resource({
    loader: () => firstValueFrom(this.api.upcoming(5)),
  });

  readonly dueToday = resource({
    loader: () => firstValueFrom(this.api.dueToday()),
  });

  readonly firstPage = resource({
    loader: () => firstValueFrom(this.api.listCards({ page: 1, pageSize: 20 })),
  });

  readonly accountAverage = computed<number | null>(() => {
    const page = this.firstPage.value();
    if (!page || page.items.length === 0) return null;
    const scored = page.items
      .map((c) => (c.type === 'Question' && c.avgAiScore !== null ? c.avgAiScore : c.avgRating))
      .filter((v): v is number => v !== null);
    if (scored.length === 0) return null;
    return scored.reduce((a, b) => a + b, 0) / scored.length;
  });

  readonly identities = resource({
    loader: () => firstValueFrom(this.api.getIdentities()),
  });

  /** Local dismissal flag — survives page reloads but not localStorage clear. */
  readonly bannerDismissed = signal<boolean>(
    typeof localStorage !== 'undefined'
      && localStorage.getItem(TELEGRAM_BANNER_DISMISS_KEY) === 'true',
  );

  /** Banner is visible only when the user has no Telegram identity yet AND hasn't dismissed it. */
  readonly showTelegramBanner = computed<boolean>(() => {
    if (this.bannerDismissed()) return false;
    const list = this.identities.value();
    if (!list) return false; // still loading — hide so it doesn't flash
    return !list.some((i) => i.provider === 'Telegram');
  });

  dismissTelegramBanner(): void {
    this.bannerDismissed.set(true);
    try {
      localStorage.setItem(TELEGRAM_BANNER_DISMISS_KEY, 'true');
    } catch {
      // localStorage may be unavailable (private mode etc.) — banner just
      // re-appears next session, which is acceptable.
    }
  }

  relTime(iso: string): string {
    return relativeTime(iso);
  }
}
