import { Component, computed, inject } from '@angular/core';

import { IconComponent, type IconName } from '../ui/icon.component';
import { ThemeService } from './theme.service';

@Component({
  selector: 'app-theme-toggle',
  standalone: true,
  imports: [IconComponent],
  template: `
    @if (!theme.isInMiniApp) {
      <button
        type="button"
        (click)="cycle()"
        class="h-9 px-2.5 rounded-md text-fg-secondary hover:bg-surface-hover hover:text-fg text-xs flex items-center gap-1.5 transition-colors"
        [attr.title]="title()"
        [attr.aria-label]="title()"
      >
        <app-icon [name]="iconName()" [size]="16" />
        <span class="hidden sm:inline capitalize">{{ mode() }}</span>
      </button>
    }
  `,
})
export class ThemeToggleComponent {
  protected readonly theme = inject(ThemeService);

  readonly mode = this.theme.mode;

  readonly iconName = computed<IconName>(() => {
    const m = this.mode();
    if (m === 'light') return 'sun';
    if (m === 'dark') return 'moon';
    return 'monitor';
  });

  readonly title = computed(
    () => `Theme: ${this.mode()} (click to cycle system → light → dark)`,
  );

  cycle(): void {
    this.theme.cycle();
  }
}
