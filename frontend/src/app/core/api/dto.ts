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
