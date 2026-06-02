import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, resource } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { IconComponent } from '../../../shared/components/icon/icon.component';
import { relativeTime } from '../../../shared/utils/relative-time';
import { AdminApiService } from '../services/admin-api.service';

@Component({
  selector: 'app-admin-user-detail',
  standalone: true,
  imports: [DatePipe, DecimalPipe, IconComponent, RouterLink],
  templateUrl: './admin-user-detail.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminUserDetailComponent {
  private readonly api = inject(AdminApiService);

  /** Bound from the `:id` route param via `withComponentInputBinding()`. */
  readonly id = input.required<string>();

  // Phase 4 reuses the list endpoint to find this user's row, since a
  // dedicated /admin/users/{id} endpoint isn't built yet. Bound by id so
  // navigation between detail pages refetches.
  readonly summary = resource({
    params: () => ({ id: this.id() }),
    loader: async ({ params }) => {
      // Page through 1000 users to find the row by id. Acceptable while
      // the user base is small; a dedicated endpoint is the right fix
      // once the count grows.
      const page = await firstValueFrom(this.api.listUsers({ page: 1, pageSize: 100 }));
      return page.items.find((u) => u.id === params.id) ?? null;
    },
  });

  readonly relTimes = computed(() => {
    const s = this.summary.value();
    if (!s) return null;
    return {
      createdRel: relativeTime(s.createdAt),
      lastSeenRel: s.lastSeenAt ? relativeTime(s.lastSeenAt) : null,
      lastCallRel: s.lastCallAt ? relativeTime(s.lastCallAt) : null,
    };
  });
}
