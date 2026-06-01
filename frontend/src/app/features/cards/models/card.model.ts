export type CardType = 'Note' | 'Question';

export interface CardSummaryDto {
  readonly id: string;
  readonly title: string;
  readonly tags: readonly string[];
  readonly createdAt: string;
  readonly type: CardType;
  readonly reviewCount: number;
  readonly avgRating: number | null;
  readonly avgAiScore: number | null;
  readonly isPaused: boolean;
  readonly pausedAtStage: number | null;
}

export interface CardDto extends CardSummaryDto {
  readonly body: string;
  readonly updatedAt: string;
}

export interface AddCardPayload {
  readonly title: string;
  readonly body: string;
  readonly tags: readonly string[];
}

export interface UpdateCardPayload {
  readonly title?: string;
  readonly body?: string;
  readonly tags?: readonly string[];
}
