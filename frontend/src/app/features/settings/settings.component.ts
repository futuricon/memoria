import { DatePipe } from '@angular/common';
import { Component, inject, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { ApiClient } from '../../core/api/api-client';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [DatePipe, FormsModule],
  template: `
    <header class="mb-6">
      <h1 class="text-2xl font-semibold">Settings</h1>
      <p class="text-sm text-slate-500">Account, preferences and integrations.</p>
    </header>

    <div class="space-y-6 max-w-2xl">
      <section class="bg-white border border-slate-200 rounded-lg p-5">
        <h2 class="text-sm font-medium text-slate-500 uppercase tracking-wide mb-3">Account</h2>
        @if (me.isLoading()) {
          <p class="text-sm text-slate-400">Loading…</p>
        } @else if (me.error()) {
          <p class="text-sm text-rose-600">Failed to load.</p>
        } @else if (me.value(); as u) {
          <dl class="text-sm grid grid-cols-3 gap-y-2">
            <dt class="text-slate-500">Display name</dt>
            <dd class="col-span-2 text-slate-900">{{ u.displayName }}</dd>
            <dt class="text-slate-500">Email</dt>
            <dd class="col-span-2 text-slate-900">{{ u.email ?? '—' }}</dd>
            <dt class="text-slate-500">Joined</dt>
            <dd class="col-span-2 text-slate-900">
              {{ u.createdAt | date: 'medium' }}
            </dd>
          </dl>
        }
      </section>

      <section class="bg-white border border-slate-200 rounded-lg p-5">
        <h2 class="text-sm font-medium text-slate-500 uppercase tracking-wide mb-3">
          Linked identities
        </h2>
        @if (identities.isLoading()) {
          <p class="text-sm text-slate-400">Loading…</p>
        } @else if (identities.value()?.length === 0) {
          <p class="text-sm text-slate-400">No linked identities.</p>
        } @else {
          <ul class="flex flex-wrap gap-2">
            @for (i of identities.value() ?? []; track i.provider + i.externalId) {
              <li
                class="px-2 py-1 text-xs rounded bg-slate-100 text-slate-700"
                [title]="i.externalId + ' · linked ' + (i.linkedAt | date: 'shortDate')"
              >{{ i.provider }}</li>
            }
          </ul>
        }
      </section>

      <section class="bg-white border border-slate-200 rounded-lg p-5">
        <h2 class="text-sm font-medium text-slate-500 uppercase tracking-wide mb-3">
          Preferences
        </h2>

        <div class="space-y-3 text-sm">
          <label class="block">
            <span class="text-slate-600">Timezone</span>
            <input
              type="text"
              list="timezones"
              [(ngModel)]="timeZoneId"
              name="timeZoneId"
              class="mt-1 w-full px-3 py-2 border border-slate-300 rounded focus:outline-none focus:ring-2 focus:ring-slate-400"
              placeholder="Europe/Tashkent"
            />
            <datalist id="timezones">
              @for (tz of timeZones; track tz) {
                <option [value]="tz"></option>
              }
            </datalist>
            <span class="block mt-1 text-xs text-slate-400">
              IANA name. Affects scheduling and quiet-hours interpretation.
            </span>
          </label>

          <div class="grid grid-cols-2 gap-3">
            <label class="block">
              <span class="text-slate-600">Quiet hours — start</span>
              <input
                type="time"
                [(ngModel)]="quietStart"
                name="quietStart"
                class="mt-1 w-full px-3 py-2 border border-slate-300 rounded focus:outline-none focus:ring-2 focus:ring-slate-400"
              />
            </label>
            <label class="block">
              <span class="text-slate-600">Quiet hours — end</span>
              <input
                type="time"
                [(ngModel)]="quietEnd"
                name="quietEnd"
                class="mt-1 w-full px-3 py-2 border border-slate-300 rounded focus:outline-none focus:ring-2 focus:ring-slate-400"
              />
            </label>
          </div>
          <button
            type="button"
            (click)="clearQuietHours()"
            class="text-xs text-slate-500 hover:text-slate-700 underline"
          >Clear quiet hours</button>

          <div class="flex items-center gap-3 pt-2">
            <button
              type="button"
              (click)="save()"
              [disabled]="savingPrefs()"
              class="px-3 py-1.5 text-sm rounded bg-slate-900 text-white hover:bg-slate-800 disabled:opacity-50"
            >{{ savingPrefs() ? 'Saving…' : 'Save preferences' }}</button>
            @if (saveStatus()) {
              <span class="text-xs text-emerald-600">{{ saveStatus() }}</span>
            }
            @if (saveError()) {
              <span class="text-xs text-rose-600">{{ saveError() }}</span>
            }
          </div>
        </div>
      </section>

      <section class="bg-white border border-slate-200 rounded-lg p-5">
        <h2 class="text-sm font-medium text-slate-500 uppercase tracking-wide mb-3">
          Telegram
        </h2>
        <p class="text-sm text-slate-600 mb-3">
          Link your Telegram account so the bot can send you reminders. The link is
          one-time and expires after 5 minutes.
        </p>
        <p class="text-xs text-slate-500 mb-3">
          If this Telegram chat already has a Memoria account, opening the link will
          <span class="font-medium">merge it into your current account</span>
          (cards, reviews, history). The original Telegram-only account is deactivated.
        </p>
        <button
          type="button"
          (click)="generateTelegramLink()"
          [disabled]="generatingLink()"
          class="px-3 py-1.5 text-sm rounded border border-slate-300 bg-white hover:bg-slate-100 disabled:opacity-50"
        >{{ generatingLink() ? 'Generating…' : 'Generate link' }}</button>

        @if (telegramLink(); as link) {
          <div class="mt-3 p-3 rounded bg-slate-50 border border-slate-200 text-sm">
            <a
              [href]="link"
              target="_blank"
              rel="noopener"
              class="text-slate-900 hover:underline break-all font-mono"
            >{{ link }}</a>
            <p class="mt-2 text-xs text-slate-500">
              Open the link in Telegram, then press <code>Start</code> in the bot.
            </p>
          </div>
        }

        @if (telegramError()) {
          <p class="mt-3 text-sm text-rose-600">{{ telegramError() }}</p>
        }
      </section>
    </div>
  `,
})
export class SettingsComponent {
  private readonly api = inject(ApiClient);

  readonly timeZones = getSupportedTimeZones();

  readonly me = resource({
    loader: async () => {
      const u = await firstValueFrom(this.api.getMe());
      this.timeZoneId = u.timeZoneId;
      this.quietStart = u.quietHoursStart ?? '';
      this.quietEnd = u.quietHoursEnd ?? '';
      return u;
    },
  });

  readonly identities = resource({
    loader: () => firstValueFrom(this.api.getIdentities()),
  });

  timeZoneId = 'UTC';
  quietStart = '';
  quietEnd = '';

  readonly savingPrefs = signal(false);
  readonly saveStatus = signal<string | null>(null);
  readonly saveError = signal<string | null>(null);

  readonly generatingLink = signal(false);
  readonly telegramLink = signal<string | null>(null);
  readonly telegramError = signal<string | null>(null);

  clearQuietHours(): void {
    this.quietStart = '';
    this.quietEnd = '';
  }

  async save(): Promise<void> {
    this.savingPrefs.set(true);
    this.saveStatus.set(null);
    this.saveError.set(null);
    try {
      await firstValueFrom(
        this.api.updateMe({
          timeZoneId: this.timeZoneId.trim() || 'UTC',
          quietHoursStart: this.quietStart || null,
          quietHoursEnd: this.quietEnd || null,
        }),
      );
      this.saveStatus.set('Saved.');
      this.me.reload();
    } catch (e) {
      this.saveError.set(this.describe(e, 'Could not save.'));
    } finally {
      this.savingPrefs.set(false);
    }
  }

  async generateTelegramLink(): Promise<void> {
    this.generatingLink.set(true);
    this.telegramError.set(null);
    this.telegramLink.set(null);
    try {
      const res = await firstValueFrom(this.api.startTelegramLinking());
      this.telegramLink.set(res.deepLink);
    } catch (e) {
      this.telegramError.set(this.describe(e, 'Could not generate link.'));
    } finally {
      this.generatingLink.set(false);
    }
  }

  private describe(e: unknown, fallback: string): string {
    if (e && typeof e === 'object' && 'error' in e) {
      const err = (e as { error?: { message?: string } }).error;
      if (err?.message) return err.message;
    }
    return fallback;
  }
}

function getSupportedTimeZones(): string[] {
  const intl = Intl as unknown as { supportedValuesOf?: (key: 'timeZone') => string[] };
  if (typeof intl.supportedValuesOf === 'function') {
    return intl.supportedValuesOf('timeZone');
  }
  // Minimal fallback for environments without supportedValuesOf.
  return [
    'UTC', 'Europe/Tashkent', 'Europe/Moscow', 'Europe/Berlin', 'Europe/London',
    'America/New_York', 'America/Los_Angeles', 'Asia/Tokyo', 'Asia/Singapore',
  ];
}
