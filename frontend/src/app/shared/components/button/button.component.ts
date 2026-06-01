import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

import { IconComponent, type IconName } from '../icon/icon.component';

export type ButtonVariant = 'primary' | 'secondary' | 'danger';
export type ButtonSize = 'sm' | 'md';

const BASE =
  'inline-flex items-center justify-center gap-2 rounded-md font-medium transition-colors disabled:opacity-50 disabled:cursor-not-allowed';

const SIZE: Readonly<Record<ButtonSize, string>> = {
  sm: 'h-9 px-3 text-xs gap-1.5',
  md: 'h-10 px-4 text-sm',
};

const VARIANT: Readonly<Record<ButtonVariant, string>> = {
  primary: 'bg-brand text-brand-on hover:bg-brand-hover',
  secondary:
    'border border-default text-fg-secondary hover:bg-surface-hover hover:text-fg',
  danger: 'btn-danger',
};

@Component({
  selector: 'app-button',
  standalone: true,
  imports: [IconComponent],
  templateUrl: './button.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ButtonComponent {
  readonly variant = input<ButtonVariant>('secondary');
  readonly size = input<ButtonSize>('md');
  readonly type = input<'button' | 'submit'>('button');
  readonly disabled = input<boolean>(false);
  readonly loading = input<boolean>(false);
  readonly leadingIcon = input<IconName | null>(null);
  readonly trailingIcon = input<IconName | null>(null);
  readonly title = input<string | null>(null);
  readonly ariaLabel = input<string | null>(null);
  /** Make the button stretch to the container's full width. */
  readonly block = input<boolean>(false);

  readonly iconSize = computed<number>(() => (this.size() === 'sm' ? 12 : 14));

  readonly classes = computed<string>(() => {
    const blockClass = this.block() ? 'w-full' : '';
    return `${BASE} ${SIZE[this.size()]} ${VARIANT[this.variant()]} ${blockClass}`.trim();
  });
}
