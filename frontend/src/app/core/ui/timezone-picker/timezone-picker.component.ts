import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  HostListener,
  computed,
  inject,
  input,
  model,
  signal,
} from '@angular/core';

import { TimeZoneDto } from '../../models/user.model';
import { IconComponent } from '../icon/icon.component';

@Component({
  selector: 'app-timezone-picker',
  standalone: true,
  imports: [IconComponent],
  templateUrl: './timezone-picker.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TimeZonePickerComponent {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  readonly options = input<TimeZoneDto[] | null | undefined>([]);
  readonly value = model<string>('');

  readonly open = signal(false);
  readonly query = signal('');

  readonly filtered = computed<TimeZoneDto[]>(() => {
    const list = this.options() ?? [];
    const q = this.query().trim().toLowerCase();
    if (!q) return list;
    return list.filter(
      (tz) =>
        tz.displayName.toLowerCase().includes(q) || tz.id.toLowerCase().includes(q),
    );
  });

  readonly selectedLabel = computed<string>(() => {
    const id = this.value();
    if (!id) return '';
    const match = (this.options() ?? []).find((tz) => tz.id === id);
    // Fallback to the raw id if the backend list doesn't include it
    // (e.g. user previously saved a tz that's no longer in the catalog).
    return match?.displayName ?? id;
  });

  toggle(): void {
    if (this.open()) this.close();
    else this.openPanel();
  }

  openPanel(): void {
    this.open.set(true);
    this.query.set('');
    // Focus the search box after the panel renders.
    queueMicrotask(() => {
      const input = this.host.nativeElement.querySelector<HTMLInputElement>('input');
      input?.focus();
    });
  }

  close(): void {
    this.open.set(false);
  }

  onQueryInput(evt: Event): void {
    this.query.set((evt.target as HTMLInputElement).value);
  }

  select(tz: TimeZoneDto): void {
    this.value.set(tz.id);
    this.close();
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(evt: MouseEvent): void {
    if (!this.open()) return;
    if (!this.host.nativeElement.contains(evt.target as Node)) {
      this.close();
    }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.close();
  }
}
