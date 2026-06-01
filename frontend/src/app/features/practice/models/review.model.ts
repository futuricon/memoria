export type Rating = 'Forgot' | 'Hard' | 'Good' | 'Easy';

export type GradingVerdict = 'Incorrect' | 'Partial' | 'Correct';

export interface GradingResult {
  readonly score: number;
  readonly verdict: GradingVerdict;
  readonly feedback: string;
}

export interface RecordReviewPayload {
  readonly reminderId?: string | null;
  readonly rating: Rating;
  readonly note?: string | null;
  readonly answerText?: string | null;
  readonly aiScore?: number | null;
  readonly aiFeedback?: string | null;
  readonly autoGraded?: boolean;
}

export interface ReviewDto {
  readonly id: string;
  readonly cardId: string;
  readonly userId: string;
  readonly reminderId: string | null;
  readonly rating: Rating;
  readonly cardTitleSnapshot: string;
  readonly reviewedAt: string;
  readonly note: string | null;
}
