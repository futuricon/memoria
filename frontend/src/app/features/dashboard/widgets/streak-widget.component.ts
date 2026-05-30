import { Component, inject, resource } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { ApiClient } from '../../../core/api/api-client';

@Component({
  selector: 'app-streak-widget',
  standalone: true,
  template: `
    <section class="bg-white border border-slate-200 rounded-lg p-5">
      <h2 class="text-sm font-medium text-slate-500 uppercase tracking-wide mb-3">
        Streak
      </h2>
      @if (data.isLoading()) {
        <p class="text-sm text-slate-400">Loading…</p>
      } @else if (data.error()) {
        <p class="text-sm text-rose-600">Failed to load.</p>
      } @else if (data.value(); as d) {
        <div class="flex items-baseline gap-6">
          <div>
            <p class="text-3xl font-semibold text-slate-900">
              {{ d.current }}
              <span class="text-sm font-normal text-slate-500">
                {{ d.current === 1 ? 'day' : 'days' }}
              </span>
            </p>
            <p class="text-xs text-slate-500">current streak</p>
          </div>
          <div>
            <p class="text-xl font-medium text-slate-700">{{ d.longest }}</p>
            <p class="text-xs text-slate-500">longest</p>
          </div>
        </div>
        @if (d.current === 0 && d.longest === 0) {
          <p class="mt-2 text-xs text-slate-400">Review a card today to start your streak.</p>
        } @else if (d.current === 0) {
          <p class="mt-2 text-xs text-amber-600">
            Streak broken — review a card today to restart.
          </p>
        }
      }
    </section>
  `,
})
export class StreakWidgetComponent {
  private readonly api = inject(ApiClient);

  readonly data = resource({
    loader: () => firstValueFrom(this.api.streak()),
  });
}
