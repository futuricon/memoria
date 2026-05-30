import { DecimalPipe } from '@angular/common';
import { Component, computed, input } from '@angular/core';

import { CardType } from '../api/dto';

/**
 * Type-aware grade pill. Note cards show the rating average (📝), Question
 * cards show the AI score average if available (❓), falling back to rating.
 * Empty pill if no reviews exist yet.
 */
@Component({
  selector: 'app-grade-pill',
  standalone: true,
  template: `
    @if (display() !== null) {
      <span
        class="inline-flex items-center gap-1 px-2 py-0.5 rounded text-xs font-medium"
        [class.bg-emerald-100]="display()! >= 75"
        [class.text-emerald-700]="display()! >= 75"
        [class.bg-amber-100]="display()! >= 40 && display()! < 75"
        [class.text-amber-700]="display()! >= 40 && display()! < 75"
        [class.bg-rose-100]="display()! < 40"
        [class.text-rose-700]="display()! < 40"
        [title]="reviewCount() + ' reviews'"
      >
        <span>{{ type() === 'Question' ? '❓' : '📝' }}</span>
        <span>{{ display()! | number: '1.0-0' }}</span>
      </span>
    } @else {
      <span class="text-xs text-slate-400" title="No reviews yet">
        {{ type() === 'Question' ? '❓' : '📝' }} —
      </span>
    }
  `,
  imports: [DecimalPipe],
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
}
