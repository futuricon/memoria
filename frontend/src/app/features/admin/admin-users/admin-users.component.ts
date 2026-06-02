import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, resource, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { ButtonComponent } from '../../../shared/components/button/button.component';
import { IconComponent } from '../../../shared/components/icon/icon.component';
import { relativeTime } from '../../../shared/utils/relative-time';
import { UserSortKey } from '../models/admin-user.model';
import { AdminApiService } from '../services/admin-api.service';

const PAGE_SIZE = 25;

@Component({
  selector: 'app-admin-users',
  standalone: true,
  imports: [ButtonComponent, DatePipe, DecimalPipe, FormsModule, IconComponent, RouterLink],
  templateUrl: './admin-users.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminUsersComponent {
  private readonly api = inject(AdminApiService);

  readonly search = signal('');
  readonly sort = signal<UserSortKey>('CreatedAtDesc');
  readonly pageNum = signal(1);

  private searchTimer: ReturnType<typeof setTimeout> | null = null;

  readonly page = resource({
    params: () => ({
      search: this.search(),
      sort: this.sort(),
      page: this.pageNum(),
    }),
    loader: ({ params }) =>
      firstValueFrom(
        this.api.listUsers({
          page: params.page,
          pageSize: PAGE_SIZE,
          search: params.search || undefined,
          sort: params.sort,
        }),
      ),
  });

  readonly rows = computed(() => this.page.value()?.items ?? []);
  readonly totalCount = computed(() => this.page.value()?.totalCount ?? 0);
  readonly totalPages = computed(() => {
    const total = this.totalCount();
    return Math.max(1, Math.ceil(total / PAGE_SIZE));
  });
  readonly canPrev = computed(() => this.pageNum() > 1);
  readonly canNext = computed(() => this.pageNum() < this.totalPages());

  // Pre-compute "rel time" once per page load so the template doesn't call
  // relativeTime() inside @for (Angular's no-method-calls rule).
  readonly rowsWithRel = computed(() =>
    this.rows().map((u) => ({
      ...u,
      lastSeenRel: u.lastSeenAt ? relativeTime(u.lastSeenAt) : null,
      lastCallRel: u.lastCallAt ? relativeTime(u.lastCallAt) : null,
      shortId: u.id.substring(0, 8),
    })),
  );

  onSearchInput(value: string): void {
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => {
      this.search.set(value.trim());
      this.pageNum.set(1);
    }, 250);
  }

  setSort(key: UserSortKey): void {
    this.sort.set(key);
    this.pageNum.set(1);
  }

  prevPage(): void {
    if (this.canPrev()) this.pageNum.update((n) => n - 1);
  }

  nextPage(): void {
    if (this.canNext()) this.pageNum.update((n) => n + 1);
  }
}
