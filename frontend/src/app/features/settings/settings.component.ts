import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { UsersApiService } from '../../core/services/users-api.service';
import { IconComponent } from '../../core/ui/icon/icon.component';
import { TimeZonePickerComponent } from '../../core/ui/timezone-picker/timezone-picker.component';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [DatePipe, FormsModule, IconComponent, TimeZonePickerComponent],
  templateUrl: './settings.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SettingsComponent {
  private readonly api = inject(UsersApiService);

  readonly timeZones = resource({
    loader: () => firstValueFrom(this.api.listTimeZones()),
  });

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
          quietHoursStart: toTimeOnlyWire(this.quietStart),
          quietHoursEnd: toTimeOnlyWire(this.quietEnd),
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

/**
 * <input type="time"> emits "HH:mm" but the backend's TimeOnly converter
 * requires the full "HH:mm:ss" form. Pad the seconds so the request body
 * deserializes; an empty value becomes null (the column is nullable).
 */
function toTimeOnlyWire(v: string): string | null {
  if (!v) return null;
  return /^\d{2}:\d{2}$/.test(v) ? `${v}:00` : v;
}
