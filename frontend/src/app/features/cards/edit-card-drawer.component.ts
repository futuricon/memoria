import { Dialog, DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { ApiClient } from '../../core/api/api-client';
import { CardDto } from '../../core/api/dto';
import { IconComponent } from '../../core/ui/icon.component';

export interface EditCardDrawerData {
  card: CardDto;
}

@Component({
  selector: 'app-edit-card-drawer',
  standalone: true,
  imports: [FormsModule, IconComponent],
  template: `
    <div class="bg-surface h-full flex flex-col">
      <header class="px-5 md:px-6 py-4 border-b border-default flex items-center justify-between">
        <div class="flex items-center gap-2">
          <app-icon name="pencil" [size]="16" class="text-brand" />
          <h2 class="text-base font-semibold text-fg">Edit card</h2>
        </div>
        <button
          type="button"
          (click)="cancel()"
          class="w-9 h-9 rounded-md text-fg-muted hover:text-fg hover:bg-surface-hover flex items-center justify-center"
          aria-label="Close"
        >
          <app-icon name="x" [size]="18" />
        </button>
      </header>

      <div class="flex-1 overflow-y-auto px-5 md:px-6 py-5 space-y-5">
        <label class="block">
          <span class="block text-xs font-medium text-fg-secondary mb-1.5">Title</span>
          <input
            type="text"
            [(ngModel)]="title"
            name="title"
            class="w-full px-3 py-2 bg-surface-raised border border-default rounded-md text-sm text-fg placeholder:text-fg-muted focus:outline-none focus:ring-2 focus:ring-brand/40 focus:border-brand"
          />
        </label>

        <label class="block">
          <span class="block text-xs font-medium text-fg-secondary mb-1.5">Body</span>
          <textarea
            [(ngModel)]="body"
            name="body"
            rows="10"
            class="w-full px-3 py-2 bg-surface-raised border border-default rounded-md text-sm text-fg placeholder:text-fg-muted focus:outline-none focus:ring-2 focus:ring-brand/40 focus:border-brand font-mono"
          ></textarea>
        </label>

        <label class="block">
          <span class="block text-xs font-medium text-fg-secondary mb-1.5">
            Tags <span class="text-fg-muted font-normal">(comma-separated)</span>
          </span>
          <input
            type="text"
            [(ngModel)]="tagsRaw"
            name="tags"
            class="w-full px-3 py-2 bg-surface-raised border border-default rounded-md text-sm text-fg placeholder:text-fg-muted focus:outline-none focus:ring-2 focus:ring-brand/40 focus:border-brand"
            placeholder="dotnet, ef-core, ..."
          />
        </label>

        @if (error()) {
          <p class="text-sm text-danger">{{ error() }}</p>
        }
      </div>

      <footer class="px-5 md:px-6 py-4 border-t border-default flex justify-end gap-2 bg-surface-raised pb-safe">
        <button
          type="button"
          (click)="cancel()"
          class="h-9 px-4 rounded-md text-sm border border-default text-fg hover:bg-surface-hover transition-colors"
        >Cancel</button>
        <button
          type="button"
          (click)="save()"
          [disabled]="busy()"
          class="h-9 px-4 rounded-md text-sm font-medium bg-brand text-brand-on hover:bg-brand-hover transition-colors disabled:opacity-50 disabled:cursor-not-allowed inline-flex items-center gap-2"
        >
          @if (busy()) {
            <app-icon name="loader" [size]="14" class="animate-spin" />
            Saving…
          } @else {
            Save
          }
        </button>
      </footer>
    </div>
  `,
})
export class EditCardDrawerComponent {
  readonly data = inject<EditCardDrawerData>(DIALOG_DATA);
  private readonly ref = inject<DialogRef<CardDto | null, EditCardDrawerComponent>>(DialogRef);
  private readonly api = inject(ApiClient);

  title = this.data.card.title;
  body = this.data.card.body;
  tagsRaw = this.data.card.tags.join(', ');

  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  async save(): Promise<void> {
    this.error.set(null);
    this.busy.set(true);
    try {
      const updated = await new Promise<CardDto>((resolve, reject) => {
        this.api
          .updateCard(this.data.card.id, {
            title: this.title.trim(),
            body: this.body,
            tags: this.parseTags(),
          })
          .subscribe({ next: resolve, error: reject });
      });
      this.ref.close(updated);
    } catch (e) {
      this.error.set(this.describe(e));
    } finally {
      this.busy.set(false);
    }
  }

  cancel(): void {
    this.ref.close(null);
  }

  private parseTags(): string[] {
    return this.tagsRaw
      .split(',')
      .map((t) => t.trim())
      .filter((t) => t.length > 0);
  }

  private describe(e: unknown): string {
    if (e && typeof e === 'object' && 'error' in e) {
      const err = (e as { error?: { code?: string; message?: string } }).error;
      if (err?.code === 'cards.edit_window_closed') {
        return 'Editing is only allowed within 24 hours of creation.';
      }
      if (err?.message) return err.message;
    }
    return 'Could not save the card.';
  }
}

export function openEditDrawer(dialog: Dialog, data: EditCardDrawerData) {
  return dialog.open<CardDto | null, EditCardDrawerData, EditCardDrawerComponent>(
    EditCardDrawerComponent,
    {
      data,
      panelClass: 'app-right-drawer',
      backdropClass: 'app-overlay-backdrop',
      disableClose: false,
    },
  );
}
