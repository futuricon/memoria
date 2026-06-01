import { CardType } from '../../cards/models/card.model';

export interface CardWithGradeDto {
  readonly id: string;
  readonly title: string;
  readonly tags: readonly string[];
  readonly createdAt: string;
  readonly type: CardType;
  readonly reviewCount: number;
  readonly avgRating: number | null;
  readonly avgAiScore: number | null;
}

export interface StreakDto {
  readonly current: number;
  readonly longest: number;
  readonly lastReviewedOnUtc: string | null;
}

export interface RatingDistributionDto {
  readonly forgot: number;
  readonly hard: number;
  readonly good: number;
  readonly easy: number;
  readonly total: number;
}

export interface HeatmapDayDto {
  readonly dateUtc: string;
  readonly count: number;
}

export interface StuckCardDto {
  readonly cardId: string;
  readonly title: string;
  readonly consecutiveForgotCount: number;
  readonly lastReviewedAt: string;
  readonly currentStage: number | null;
}

export interface TagAverageDto {
  readonly tag: string;
  readonly cardCount: number;
  readonly reviewCount: number;
  readonly avgScore: number;
}
