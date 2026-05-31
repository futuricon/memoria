import { DecimalPipe } from '@angular/common';
import { Component, computed, input } from '@angular/core';

import { CardType } from '../api/dto';
import { IconComponent } from './icon.component';

/**
 * Type-aware grade pill. Note cards show the rating average, Question
 * cards show the AI score average if available (falling back to rating).
 * Color tracks the score band — green > 75, amber 40–74, red < 40.
 * Empty pill if no reviews exist yet.
 */
@Component({
  selector: 'app-grade-pill',
  standalone: true,
  imports: [DecimalPipe, IconComponent],
  template: `
    @if (display() !== null) {
      <span
        class="inline-flex items-center gap-1 px-2 py-0.5 rounded text-xs font-medium"
        [style.color]="bandColor()"
        [style.background]="bandBackground()"
        [title]="reviewCount() + ' reviews'"
      >
        <app-icon
          [name]="type() === 'Question' ? 'help-circle' : 'file-text'"
          [size]="11"
        />
        <span class="tabular-nums">{{ display()! | number: '1.0-0' }}</span>
      </span>
    } @else {
      <span class="inline-flex items-center gap-1 text-xs text-fg-muted" title="No reviews yet">
        <app-icon
          [name]="type() === 'Question' ? 'help-circle' : 'file-text'"
          [size]="11"
        />
        <span>—</span>
      </span>
    }
  `,
})
export class GradePillComponent {
  readonly type = input.required<CardType>();
  readonly avgRating = input<number | null>(null);
  readonly avgAiScore = input<number | null>(null);
  readonly reviewCount = input<number>(0);

  readonly display = computed<number | null>(() => {
    if (this.type() === 'Question' && this.avgAiScore() !== null) {
      return this.avgAiScore();
    }
    return this.avgRating();
  });

  readonly bandColor = computed(() => {
    const v = this.display();
    if (v === null) return undefined;
    if (v >= 75) return 'var(--color-rating-good)';
    if (v >= 40) return 'var(--color-rating-hard)';
    return 'var(--color-rating-again)';
  });

  readonly bandBackground = computed(() => {
    const v = this.display();
    if (v === null) return undefined;
    const c = this.bandColor();
    return `color-mix(in srgb, ${c} 14%, transparent)`;
  });
}
