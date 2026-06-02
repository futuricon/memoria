export type AiOperation = 'AnswerGrading' | 'QuestionCardValidation';

export interface ActivationFunnelDto {
  readonly signups: number;
  readonly telegramLinked: number;
  readonly hasCard: number;
  readonly hasReview: number;
}

export interface ActiveUserCountsDto {
  readonly dau: number;
  readonly wau: number;
  readonly mau: number;
}

export interface RetentionCohortsDto {
  readonly windowStart: string;
  readonly windowEnd: string;
  readonly signups: number;
  readonly d1Retained: number;
  readonly d7Retained: number;
  readonly d30Retained: number;
}

export interface RatingDistributionDto {
  readonly forgot: number;
  readonly hard: number;
  readonly good: number;
  readonly easy: number;
  readonly total: number;
}

export interface AiCalibrationBucketDto {
  readonly lowerInclusive: number;
  readonly upperExclusive: number;
  readonly forgot: number;
  readonly hard: number;
  readonly good: number;
  readonly easy: number;
  readonly total: number;
}

export interface ReminderSkipRateDto {
  readonly sent: number;
  readonly confirmed: number;
  readonly skipped: number;
  readonly failed: number;
  readonly total: number;
}

export interface AiSpendTotalsDto {
  readonly totalInputTokens: number;
  readonly totalOutputTokens: number;
  readonly estimatedCostUsd: number;
  readonly callCount: number;
}

export interface AiSpendTrendPointDto {
  readonly dateUtc: string;
  readonly provider: string;
  readonly operation: AiOperation;
  readonly inputTokens: number;
  readonly outputTokens: number;
  readonly estimatedCostUsd: number;
  readonly callCount: number;
}

export interface TopSpenderDto {
  readonly userId: string;
  readonly totalInputTokens: number;
  readonly totalOutputTokens: number;
  readonly estimatedCostUsd: number;
  readonly callCount: number;
}

export interface AiFailureRateDto {
  readonly totalCalls: number;
  readonly failedCalls: number;
  readonly failureRate: number;
}

export interface AdminOverviewDto {
  readonly activationFunnel: ActivationFunnelDto;
  readonly activeUsers: ActiveUserCountsDto;
  readonly retention: RetentionCohortsDto;
  readonly globalRatings: RatingDistributionDto;
  readonly aiCalibration: ReadonlyArray<AiCalibrationBucketDto>;
  readonly reminderSkipRate: ReminderSkipRateDto;
  readonly aiSpend: AiSpendTotalsDto;
  readonly aiSpendTrend: ReadonlyArray<AiSpendTrendPointDto>;
  readonly topSpenders: ReadonlyArray<TopSpenderDto>;
  readonly aiFailureRate: AiFailureRateDto;
  readonly costPerActiveUserUsd: number;
}
