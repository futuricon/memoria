import { Dialog, DialogRef } from '@angular/cdk/dialog';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { ApiClient } from '../../core/api/api-client';
import { CardDto } from '../../core/api/dto';

const TITLE_MAX = 200;
const BODY_MAX = 4000;
const TAGS_MAX = 5;

@Component({
  selector: 'app-add-card-drawer',
  standalone: true,
  imports: [FormsModule],
  template: `
    <div class="bg-white h-full flex flex-col shadow-2xl">
      <header class="px-6 py-4 border-b border-slate-200 flex items-center justify-between">
        <h2 class="text-lg font-semibold text-slate-900">New card</h2>
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
            [(ngModel)]="titleProxy"
            name="title"
            [maxlength]="TITLE_MAX"
            class="mt-1 w-full px-3 py-2 border border-slate-300 rounded focus:outline-none focus:ring-2 focus:ring-slate-400"
            placeholder="Either a statement (Note) or a question ending in ?"
          />
          <span class="mt-1 flex items-center justify-between text-xs">
            <span class="text-slate-500">
              Will be created as:
              <span class="font-medium" [class.text-blue-700]="detectedType() === 'Question'" [class.text-slate-700]="detectedType() === 'Note'">
                {{ detectedType() === 'Question' ? '❓ Question' : '📝 Note' }}
              </span>
            </span>
            <span class="text-slate-400">{{ title.length }} / {{ TITLE_MAX }}</span>
          </span>
        </label>

        <label class="block text-sm">
          <span class="text-slate-600">Body</span>
          <textarea
            [(ngModel)]="bodyProxy"
            name="body"
            rows="10"
            [maxlength]="BODY_MAX"
            class="mt-1 w-full px-3 py-2 border border-slate-300 rounded focus:outline-none focus:ring-2 focus:ring-slate-400 font-mono text-sm"
            placeholder="The answer (for Question) or the content you want to remember (for Note)."
          ></textarea>
          <span class="mt-1 block text-right text-xs text-slate-400">
            {{ body.length }} / {{ BODY_MAX }}
          </span>
        </label>

        <label class="block text-sm">
          <span class="text-slate-600">Tags (comma-separated, up to {{ TAGS_MAX }})</span>
          <input
            type="text"
            [(ngModel)]="tagsProxy"
            name="tags"
            class="mt-1 w-full px-3 py-2 border border-slate-300 rounded focus:outline-none focus:ring-2 focus:ring-slate-400"
            placeholder="dotnet, ef-core, ..."
          />
          @if (parsedTags().length > TAGS_MAX) {
            <span class="mt-1 text-xs text-rose-600">
              Too many tags ({{ parsedTags().length }}). Max is {{ TAGS_MAX }}.
            </span>
          }
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
          [disabled]="busy() || !canSubmit()"
          class="px-3 py-1.5 text-sm rounded bg-slate-900 text-white hover:bg-slate-800 disabled:opacity-50"
        >{{ busy() ? 'Creating…' : 'Create' }}</button>
      </footer>
    </div>
  `,
})
export class AddCardDrawerComponent {
  private readonly ref = inject<DialogRef<CardDto | null, AddCardDrawerComponent>>(DialogRef);
  private readonly api = inject(ApiClient);

  readonly TITLE_MAX = TITLE_MAX;
  readonly BODY_MAX = BODY_MAX;
  readonly TAGS_MAX = TAGS_MAX;

  title = '';
  body = '';
  tagsRaw = '';

  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  private readonly titleSig = signal('');
  private readonly bodySig = signal('');
  private readonly tagsRawSig = signal('');

  /** Mirrors backend's `CardTypeResolver.FromTitle`: trailing '?' → Question. */
  readonly detectedType = computed<'Question' | 'Note'>(() =>
    this.titleSig().trimEnd().endsWith('?') ? 'Question' : 'Note');

  readonly parsedTags = computed<string[]>(() =>
    this.tagsRawSig()
      .split(',')
      .map((t) => t.trim())
      .filter((t) => t.length > 0));

  readonly canSubmit = computed<boolean>(() =>
    this.titleSig().trim().length > 0
    && this.bodySig().trim().length > 0
    && this.parsedTags().length <= TAGS_MAX);

  // ngModel + signals: keep signals in sync via setters so computeds react to input.
  set titleProxy(v: string) { this.title = v; this.titleSig.set(v); }
  get titleProxy(): string { return this.title; }
  set bodyProxy(v: string) { this.body = v; this.bodySig.set(v); }
  get bodyProxy(): string { return this.body; }
  set tagsProxy(v: string) { this.tagsRaw = v; this.tagsRawSig.set(v); }
  get tagsProxy(): string { return this.tagsRaw; }

  async save(): Promise<void> {
    if (!this.canSubmit()) return;
    this.error.set(null);
    this.busy.set(true);
    try {
      const created = await firstValueFrom(
        this.api.createCard({
          title: this.title.trim(),
          body: this.body,
          tags: this.parsedTags(),
        }),
      );
      this.ref.close(created);
    } catch (e) {
      this.error.set(this.describe(e));
    } finally {
      this.busy.set(false);
    }
  }

  cancel(): void {
    this.ref.close(null);
  }

  private describe(e: unknown): string {
    if (e && typeof e === 'object' && 'error' in e) {
      const err = (e as { error?: { code?: string; message?: string } }).error;
      if (err?.message) return err.message;
    }
    return 'Could not create the card.';
  }
}

export function openAddDrawer(dialog: Dialog) {
  return dialog.open<CardDto | null, void, AddCardDrawerComponent>(
    AddCardDrawerComponent,
    {
      panelClass: 'app-right-drawer',
      backdropClass: 'app-overlay-backdrop',
      disableClose: false,
    },
  );
}
