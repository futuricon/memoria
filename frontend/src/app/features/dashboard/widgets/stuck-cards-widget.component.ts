import { Component, inject, resource } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { ApiClient } from '../../../core/api/api-client';

@Component({
  selector: 'app-stuck-cards-widget',
  standalone: true,
  template: `
    <section class="bg-white border border-slate-200 rounded-lg p-5">
      <h2 class="text-sm font-medium text-slate-500 uppercase tracking-wide mb-3">
        Stuck cards
      </h2>
      @if (data.isLoading()) {
        <p class="text-sm text-slate-400">Loading…</p>
      } @else if (data.error()) {
        <p class="text-sm text-rose-600">Failed to load.</p>
      } @else {
        @let items = data.value() ?? [];
        @if (items.length === 0) {
          <p class="text-sm text-slate-400">No stuck cards — you're recalling everything cleanly. Nice.</p>
        } @else {
          <p class="text-xs text-slate-500 mb-2">
            Cards you forgot 3+ times in a row at early stages.
          </p>
          <ul class="divide-y divide-slate-100">
            @for (c of items; track c.cardId) {
              <li class="py-2 flex items-center justify-between gap-3">
                <span class="text-sm text-slate-900 truncate">{{ c.title }}</span>
                <span class="text-xs text-rose-600 whitespace-nowrap">
                  ✕ {{ c.consecutiveForgotCount }}
                  @if (c.currentStage !== null) {
                    · stage {{ c.currentStage }}
                  }
                </span>
              </li>
            }
          </ul>
        }
      }
    </section>
  `,
})
export class StuckCardsWidgetComponent {
  private readonly api = inject(ApiClient);

  readonly data = resource({
    loader: () => firstValueFrom(this.api.stuckCards(5)),
  });
}
