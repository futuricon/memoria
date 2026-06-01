export type CardType = 'Note' | 'Question';

export interface CardSummaryDto {
  id: string;
  title: string;
  tags: string[];
  createdAt: string;
  type: CardType;
  reviewCount: number;
  avgRating: number | null;
  avgAiScore: number | null;
  isPaused: boolean;
  pausedAtStage: number | null;
}

export interface CardDto extends CardSummaryDto {
  body: string;
  updatedAt: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface DueReminderDto {
  reminderId: string;
  cardId: string;
  cardTitle: string;
  scheduledAt: string;
  stageNumber: number;
}

export interface CardWithGradeDto {
  id: string;
  title: string;
  tags: string[];
  createdAt: string;
  type: CardType;
  reviewCount: number;
  avgRating: number | null;
  avgAiScore: number | null;
}

export interface TelegramLinkingTokenDto {
  token: string;
  deepLink: string;
  expiresAt: string;
}

export interface CurrentUserDto {
  id: string;
  displayName: string;
  email: string | null;
  timeZoneId: string;
  quietHoursStart: string | null;
  quietHoursEnd: string | null;
  createdAt: string;
}

export interface UserIdentityDto {
  provider: string;
  externalId: string;
  linkedAt: string;
}

export interface UpdateMePayload {
  timeZoneId: string;
  quietHoursStart: string | null;
  quietHoursEnd: string | null;
}

export interface UpdateCardPayload {
  title?: string;
  body?: string;
  tags?: string[];
}

export interface AddCardPayload {
  title: string;
  body: string;
  tags: string[];
}

export interface TrashedCardDto {
  id: string;
  title: string;
  tags: string[];
  deletedAt: string;
  reviewsCount: number;
}

export interface StreakDto {
  current: number;
  longest: number;
  lastReviewedOnUtc: string | null;
}

export interface RatingDistributionDto {
  forgot: number;
  hard: number;
  good: number;
  easy: number;
  total: number;
}

export interface HeatmapDayDto {
  dateUtc: string;
  count: number;
}

export interface StuckCardDto {
  cardId: string;
  title: string;
  consecutiveForgotCount: number;
  lastReviewedAt: string;
  currentStage: number | null;
}

export interface TagAverageDto {
  tag: string;
  cardCount: number;
  reviewCount: number;
  avgScore: number;
}

export interface TagDto {
  id: string;
  name: string;
  cardCount: number;
}

export interface TimeZoneDto {
  id: string;
  displayName: string;
}

export type Rating = 'Forgot' | 'Hard' | 'Good' | 'Easy';

export interface RevealedAnswerDto {
  cardId: string;
  title: string;
  body: string;
}

export type GradingVerdict = 'Incorrect' | 'Partial' | 'Correct';

export interface GradingResult {
  score: number;
  verdict: GradingVerdict;
  feedback: string;
}

export interface RecordReviewPayload {
  reminderId?: string | null;
  rating: Rating;
  note?: string | null;
  answerText?: string | null;
  aiScore?: number | null;
  aiFeedback?: string | null;
  autoGraded?: boolean;
}

export interface ReviewDto {
  id: string;
  cardId: string;
  userId: string;
  reminderId: string | null;
  rating: Rating;
  cardTitleSnapshot: string;
  reviewedAt: string;
  note: string | null;
}
