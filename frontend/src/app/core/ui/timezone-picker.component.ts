import {
  Component,
  ElementRef,
  HostListener,
  computed,
  inject,
  input,
  model,
  signal,
} from '@angular/core';

import { TimeZoneDto } from '../api/dto';
import { IconComponent } from './icon.component';

@Component({
  selector: 'app-timezone-picker',
  standalone: true,
  imports: [IconComponent],
  template: `
    <div class="relative">
      <button
        type="button"
        (click)="toggle()"
        class="w-full px-3 py-2 bg-surface-raised border border-default rounded-md text-sm text-fg flex items-center justify-between gap-2 hover:bg-surface-hover focus:outline-none focus:ring-2 focus:ring-brand/40 focus:border-brand"
        [class.ring-2]="open()"
        [class.ring-brand-soft]="open()"
        [class.border-brand]="open()"
        [attr.aria-expanded]="open()"
        aria-haspopup="listbox"
      >
        <span class="truncate text-left flex-1" [class.text-fg-muted]="!selectedLabel()">
          {{ selectedLabel() || 'Pick a timezone…' }}
        </span>
        <app-icon name="chevron-right" [size]="14" class="text-fg-muted shrink-0 rotate-90" />
      </button>

      @if (open()) {
        <div
          class="absolute z-20 top-full left-0 right-0 mt-1 bg-surface border border-default rounded-md shadow-overlay overflow-hidden flex flex-col max-h-72"
          role="listbox"
        >
          <div class="p-2 border-b border-default flex items-center gap-2 bg-surface-raised">
            <app-icon name="search" [size]="14" class="text-fg-muted shrink-0" />
            <input
              #searchBox
              type="text"
              [value]="query()"
              (input)="onQueryInput($event)"
              (keydown.escape)="close()"
              placeholder="Search timezones…"
              class="flex-1 bg-transparent border-0 outline-none text-sm text-fg placeholder:text-fg-muted"
              autocomplete="off"
            />
          </div>

          <ul class="flex-1 overflow-y-auto py-1">
            @for (tz of filtered(); track tz.id) {
              <li>
                <button
                  type="button"
                  (click)="select(tz)"
                  class="w-full text-left px-3 py-2 text-sm flex items-baseline justify-between gap-3 hover:bg-surface-hover focus:bg-surface-hover focus:outline-none"
                  [class.bg-brand-soft]="tz.id === value()"
                  [class.text-fg]="tz.id === value()"
                  [class.font-medium]="tz.id === value()"
                  [attr.aria-selected]="tz.id === value()"
                  role="option"
                >
                  <span class="truncate text-fg">{{ tz.displayName }}</span>
                  <span class="text-xs text-fg-muted shrink-0">{{ tz.id }}</span>
                </button>
              </li>
            } @empty {
              <li class="px-3 py-4 text-sm text-fg-muted text-center">No matches.</li>
            }
          </ul>
        </div>
      }
    </div>
  `,
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
