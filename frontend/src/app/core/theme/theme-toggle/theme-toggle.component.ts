import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';

import { IconComponent, type IconName } from '../../ui/icon/icon.component';
import { ThemeService } from '../theme.service';

@Component({
  selector: 'app-theme-toggle',
  standalone: true,
  imports: [IconComponent],
  templateUrl: './theme-toggle.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
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
