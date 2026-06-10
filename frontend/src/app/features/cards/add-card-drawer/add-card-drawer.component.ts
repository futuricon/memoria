import { Dialog, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { CardDto } from '../models/card.model';
import { ButtonComponent } from '../../../shared/components/button/button.component';
import { IconComponent } from '../../../shared/components/icon/icon.component';
import { CardsApiService } from '../services/cards-api.service';

const TITLE_MAX = 200;
const BODY_MAX = 4000;
const TAGS_MAX = 5;

@Component({
  selector: 'app-add-card-drawer',
  standalone: true,
  imports: [ButtonComponent, FormsModule, IconComponent],
  templateUrl: './add-card-drawer.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AddCardDrawerComponent {
  private readonly ref = inject<DialogRef<CardDto | null, AddCardDrawerComponent>>(DialogRef);
  private readonly api = inject(CardsApiService);

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
      .map((t) => t.trim().replace(/^#+/, ''))
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
