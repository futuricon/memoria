import { Dialog, DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { ApiClient } from '../../core/api/api-client';
import { CardDto } from '../../core/api/dto';

export interface EditCardDrawerData {
  card: CardDto;
}

@Component({
  selector: 'app-edit-card-drawer',
  standalone: true,
  imports: [FormsModule],
  template: `
    <div class="bg-white h-full flex flex-col shadow-2xl">
      <header class="px-6 py-4 border-b border-slate-200 flex items-center justify-between">
        <h2 class="text-lg font-semibold text-slate-900">Edit card</h2>
        <button
          type="button"
          (click)="cancel()"
          class="text-slate-400 hover:text-slate-700 text-xl leading-none"
          aria-label="Close"
        >&times;</button>
      </header>

      <div class="flex-1 overflow-y-auto px-6 py-5 space-y-4">
        <label class="block text-sm">
          <span class="text-slate-600">Title</span>
          <input
            type="text"
            [(ngModel)]="title"
            name="title"
            class="mt-1 w-full px-3 py-2 border border-slate-300 rounded focus:outline-none focus:ring-2 focus:ring-slate-400"
          />
        </label>

        <label class="block text-sm">
          <span class="text-slate-600">Body</span>
          <textarea
            [(ngModel)]="body"
            name="body"
            rows="10"
            class="mt-1 w-full px-3 py-2 border border-slate-300 rounded focus:outline-none focus:ring-2 focus:ring-slate-400 font-mono text-sm"
          ></textarea>
        </label>

        <label class="block text-sm">
          <span class="text-slate-600">Tags (comma-separated)</span>
          <input
            type="text"
            [(ngModel)]="tagsRaw"
            name="tags"
            class="mt-1 w-full px-3 py-2 border border-slate-300 rounded focus:outline-none focus:ring-2 focus:ring-slate-400"
            placeholder="dotnet, ef-core, ..."
          />
        </label>

        @if (error()) {
          <p class="text-sm text-rose-600">{{ error() }}</p>
        }
      </div>

      <footer class="px-6 py-4 border-t border-slate-200 flex justify-end gap-2 bg-slate-50">
        <button
          type="button"
          (click)="cancel()"
          class="px-3 py-1.5 text-sm rounded border border-slate-300 bg-white hover:bg-slate-100"
        >Cancel</button>
        <button
          type="button"
          (click)="save()"
          [disabled]="busy()"
          class="px-3 py-1.5 text-sm rounded bg-slate-900 text-white hover:bg-slate-800 disabled:opacity-50"
        >{{ busy() ? 'Saving…' : 'Save' }}</button>
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
