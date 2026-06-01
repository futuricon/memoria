import { Dialog } from "@angular/cdk/dialog";
import { ChangeDetectionStrategy, Component, computed, inject, resource, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { firstValueFrom } from "rxjs";

import { CardSummaryDto } from "../models/card.model";
import { ButtonComponent } from "../../../shared/components/button/button.component";
import { openConfirm } from "../../../shared/components/confirm-dialog/confirm-dialog.component";
import { GradePillComponent } from "../../../shared/components/grade-pill/grade-pill.component";
import { IconComponent } from "../../../shared/components/icon/icon.component";
import { CardsApiService } from "../services/cards-api.service";
import { TagsApiService } from "../services/tags-api.service";
import { openAddDrawer } from "../add-card-drawer/add-card-drawer.component";
import { openEditDrawer } from "../edit-card-drawer/edit-card-drawer.component";

const EDIT_WINDOW_MS = 24 * 60 * 60 * 1000;

@Component({
  selector: "app-cards-list",
  standalone: true,
  imports: [ButtonComponent, FormsModule, GradePillComponent, IconComponent],
  templateUrl: "./cards-list.component.html",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CardsListComponent {
  private readonly cardsApi = inject(CardsApiService);
  private readonly tagsApi = inject(TagsApiService);
  private readonly dialog = inject(Dialog);

  readonly search = signal("");
  readonly selectedTags = signal<string[]>([]);
  /** O(1) lookup for template chips — beats calling isTagActive() N×8 per CD. */
  readonly activeTagSet = computed<ReadonlySet<string>>(
    () => new Set(this.selectedTags()),
  );
  readonly pageNum = signal(1);
  readonly refreshTick = signal(0);
  readonly pageSize = 10;

  readonly actionBusy = signal<string | null>(null);
  readonly actionError = signal<string | null>(null);
  readonly openMenu = signal<string | null>(null);

  private searchTimer: ReturnType<typeof setTimeout> | null = null;

  readonly tags = resource({
    loader: () => firstValueFrom(this.tagsApi.listPopularTags(5)),
  });

  readonly page = resource({
    params: () => ({
      search: this.search(),
      tags: this.selectedTags(),
      page: this.pageNum(),
      _tick: this.refreshTick(),
    }),
    loader: ({ params }) =>
      firstValueFrom(
        this.cardsApi.listCards({
          search: params.search,
          tags: params.tags,
          page: params.page,
          pageSize: this.pageSize,
        }),
      ),
  });

  totalPages(): number {
    const p = this.page.value();
    if (!p) return 1;
    return Math.max(1, Math.ceil(p.totalCount / p.pageSize));
  }

  toggleTag(tag: string): void {
    const cur = this.selectedTags();
    const next = cur.includes(tag)
      ? cur.filter((t) => t !== tag)
      : [...cur, tag];
    this.selectedTags.set(next);
    this.pageNum.set(1);
  }

  onSearchChange(value: string): void {
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => {
      this.search.set(value);
      this.pageNum.set(1);
    }, 250);
  }

  prev(): void {
    if (this.pageNum() > 1) this.pageNum.update((n) => n - 1);
  }

  next(): void {
    if (this.pageNum() < this.totalPages()) this.pageNum.update((n) => n + 1);
  }

  isEditable(card: CardSummaryDto): boolean {
    return Date.now() - new Date(card.createdAt).getTime() < EDIT_WINDOW_MS;
  }

  toggleMenu(id: string, evt: Event): void {
    evt.stopPropagation();
    this.openMenu.update((cur) => (cur === id ? null : id));
  }

  closeMenu(): void {
    this.openMenu.set(null);
  }

  onAdd(): void {
    const ref = openAddDrawer(this.dialog);
    ref.closed.subscribe((created) => {
      if (created) this.refresh();
    });
  }

  async onEdit(card: CardSummaryDto): Promise<void> {
    this.actionError.set(null);
    try {
      const full = await firstValueFrom(this.cardsApi.getCard(card.id));
      const ref = openEditDrawer(this.dialog, { card: full });
      ref.closed.subscribe((updated) => {
        if (updated) this.refresh();
      });
    } catch (e) {
      this.actionError.set(this.describe(e, "Could not open the card."));
    }
  }

  async onPause(card: CardSummaryDto): Promise<void> {
    this.actionError.set(null);
    this.actionBusy.set(card.id);
    try {
      await firstValueFrom(this.cardsApi.pauseCard(card.id));
      this.refresh();
    } catch (e) {
      this.actionError.set(this.describe(e, "Could not pause the card."));
    } finally {
      this.actionBusy.set(null);
    }
  }

  async onUnpause(card: CardSummaryDto): Promise<void> {
    this.actionError.set(null);
    this.actionBusy.set(card.id);
    try {
      await firstValueFrom(this.cardsApi.unpauseCard(card.id));
      this.refresh();
    } catch (e) {
      this.actionError.set(this.describe(e, "Could not unpause the card."));
    } finally {
      this.actionBusy.set(null);
    }
  }

  onDelete(card: CardSummaryDto): void {
    const ref = openConfirm(this.dialog, {
      title: "Delete card?",
      message: `"${card.title}" will be moved to trash. You can restore it later from the trash page.`,
      confirmLabel: "Delete",
      destructive: true,
    });

    ref.closed.subscribe(async (confirmed) => {
      if (!confirmed) return;
      this.actionError.set(null);
      this.actionBusy.set(card.id);
      try {
        await firstValueFrom(this.cardsApi.softDeleteCard(card.id));
        this.refresh();
      } catch (e) {
        this.actionError.set(this.describe(e, "Could not delete the card."));
      } finally {
        this.actionBusy.set(null);
      }
    });
  }

  private refresh(): void {
    this.refreshTick.update((n) => n + 1);
  }

  private describe(e: unknown, fallback: string): string {
    if (e && typeof e === "object" && "error" in e) {
      const err = (e as { error?: { message?: string } }).error;
      if (err?.message) return err.message;
    }
    return fallback;
  }
}
