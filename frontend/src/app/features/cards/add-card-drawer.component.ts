import { Dialog, DialogRef } from '@angular/cdk/dialog';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { ApiClient } from '../../core/api/api-client';
import { CardDto } from '../../core/api/dto';
import { IconComponent } from '../../core/ui/icon.component';

const TITLE_MAX = 200;
const BODY_MAX = 4000;
const TAGS_MAX = 5;

@Component({
  selector: 'app-add-card-drawer',
  standalone: true,
  imports: [FormsModule, IconComponent],
  template: `
    <div class="bg-surface flex flex-col md:h-full">
      <header class="px-5 md:px-6 py-4 border-b border-default flex items-center justify-between">
        <div class="flex items-center gap-2">
          <app-icon name="plus" [size]="18" class="text-brand" />
          <h2 class="text-base font-semibold text-fg">New card</h2>
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

      <div class="md:flex-1 md:overflow-y-auto px-5 md:px-6 py-5 space-y-5">
        <label class="block">
          <span class="block text-xs font-medium text-fg-secondary mb-1.5">Title</span>
          <input
            type="text"
            [(ngModel)]="titleProxy"
            name="title"
            [maxlength]="TITLE_MAX"
            class="w-full px-3 py-2 bg-surface-raised border border-default rounded-md text-sm text-fg placeholder:text-fg-muted focus:outline-none focus:ring-2 focus:ring-brand/40 focus:border-brand"
            placeholder="Either a statement (Note) or a question ending in ?"
          />
          <span class="mt-1.5 flex items-center justify-between text-xs">
            <span class="flex items-center gap-1.5 text-fg-muted">
              Will be created as:
              <span
                class="inline-flex items-center gap-1 font-medium"
                [class]="detectedType() === 'Question' ? 'chip-question' : 'chip-note'"
              >
                <app-icon [name]="detectedType() === 'Question' ? 'help-circle' : 'file-text'" [size]="11" />
                {{ detectedType() }}
              </span>
            </span>
            <span class="text-fg-muted tabular-nums">{{ title.length }} / {{ TITLE_MAX }}</span>
          </span>
        </label>

        <label class="block">
          <span class="block text-xs font-medium text-fg-secondary mb-1.5">Body</span>
          <textarea
            [(ngModel)]="bodyProxy"
            name="body"
            rows="10"
            [maxlength]="BODY_MAX"
            class="w-full px-3 py-2 bg-surface-raised border border-default rounded-md text-sm text-fg placeholder:text-fg-muted focus:outline-none focus:ring-2 focus:ring-brand/40 focus:border-brand font-mono"
            placeholder="The answer (for Question) or the content you want to remember (for Note)."
          ></textarea>
          <span class="mt-1.5 block text-right text-xs text-fg-muted tabular-nums">
            {{ body.length }} / {{ BODY_MAX }}
          </span>
        </label>

        <label class="block">
          <span class="block text-xs font-medium text-fg-secondary mb-1.5">
            Tags <span class="text-fg-muted font-normal">(comma-separated, up to {{ TAGS_MAX }})</span>
          </span>
          <input
            type="text"
            [(ngModel)]="tagsProxy"
            name="tags"
            class="w-full px-3 py-2 bg-surface-raised border border-default rounded-md text-sm text-fg placeholder:text-fg-muted focus:outline-none focus:ring-2 focus:ring-brand/40 focus:border-brand"
            placeholder="dotnet, ef-core, ..."
          />
          @if (parsedTags().length > TAGS_MAX) {
            <span class="mt-1.5 block text-xs text-danger">
              Too many tags ({{ parsedTags().length }}). Max is {{ TAGS_MAX }}.
            </span>
          }
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
          [disabled]="busy() || !canSubmit()"
          class="h-9 px-4 rounded-md text-sm font-medium bg-brand text-brand-on hover:bg-brand-hover transition-colors disabled:opacity-50 disabled:cursor-not-allowed inline-flex items-center gap-2"
        >
          @if (busy()) {
            <app-icon name="loader" [size]="14" class="animate-spin" />
            Creating…
          } @else {
            Create
          }
        </button>
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
