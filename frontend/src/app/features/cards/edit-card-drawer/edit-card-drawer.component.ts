import { Dialog, DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { ApiClient } from '../../../core/api/api-client';
import { CardDto } from '../../../core/api/dto';
import { IconComponent } from '../../../core/ui/icon/icon.component';

export interface EditCardDrawerData {
  card: CardDto;
}

@Component({
  selector: 'app-edit-card-drawer',
  standalone: true,
  imports: [FormsModule, IconComponent],
  templateUrl: './edit-card-drawer.component.html',
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
