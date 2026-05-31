import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import {
  AddCardPayload,
  CardDto,
  CardSummaryDto,
  CardWithGradeDto,
  CurrentUserDto,
  DueReminderDto,
  GradingResult,
  HeatmapDayDto,
  PagedResult,
  RatingDistributionDto,
  RecordReviewPayload,
  RevealedAnswerDto,
  ReviewDto,
  StreakDto,
  StuckCardDto,
  TagAverageDto,
  TagDto,
  TelegramLinkingTokenDto,
  TrashedCardDto,
  UpdateCardPayload,
  UpdateMePayload,
  UserIdentityDto,
} from './dto';

@Injectable({ providedIn: 'root' })
export class ApiClient {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBase;

  listCards(opts: {
    search?: string;
    tags?: string[];
    page?: number;
    pageSize?: number;
  }): Observable<PagedResult<CardSummaryDto>> {
    let params = new HttpParams()
      .set('page', String(opts.page ?? 1))
      .set('pageSize', String(opts.pageSize ?? 10));
    if (opts.search?.trim()) params = params.set('search', opts.search.trim());
    if (opts.tags && opts.tags.length > 0) params = params.set('tags', opts.tags.join(','));
    return this.http.get<PagedResult<CardSummaryDto>>(`${this.base}/api/v1/cards`, { params });
  }

  getCard(id: string): Observable<CardDto> {
    return this.http.get<CardDto>(`${this.base}/api/v1/cards/${id}`);
  }

  listTags(): Observable<TagDto[]> {
    return this.http.get<TagDto[]>(`${this.base}/api/v1/tags`);
  }

  dueToday(): Observable<DueReminderDto[]> {
    return this.http.get<DueReminderDto[]>(`${this.base}/api/v1/cards/due-today`);
  }

  upcoming(take = 10): Observable<DueReminderDto[]> {
    const params = new HttpParams().set('take', String(take));
    return this.http.get<DueReminderDto[]>(`${this.base}/api/v1/cards/upcoming`, { params });
  }

  worst(take = 5, minReviews = 3): Observable<CardWithGradeDto[]> {
    const params = new HttpParams()
      .set('take', String(take))
      .set('minReviews', String(minReviews));
    return this.http.get<CardWithGradeDto[]>(`${this.base}/api/v1/cards/worst`, { params });
  }

  startTelegramLinking(): Observable<TelegramLinkingTokenDto> {
    return this.http.post<TelegramLinkingTokenDto>(
      `${this.base}/api/v1/auth/telegram-linking/start`,
      {},
    );
  }

  createCard(payload: AddCardPayload): Observable<CardDto> {
    return this.http.post<CardDto>(`${this.base}/api/v1/cards`, payload);
  }

  updateCard(id: string, payload: UpdateCardPayload): Observable<CardDto> {
    return this.http.patch<CardDto>(`${this.base}/api/v1/cards/${id}`, payload);
  }

  softDeleteCard(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/api/v1/cards/${id}`);
  }

  listTrash(page = 1, pageSize = 10): Observable<PagedResult<TrashedCardDto>> {
    const params = new HttpParams()
      .set('page', String(page))
      .set('pageSize', String(pageSize));
    return this.http.get<PagedResult<TrashedCardDto>>(
      `${this.base}/api/v1/cards/trash`, { params });
  }

  restoreCard(id: string): Observable<CardDto> {
    return this.http.post<CardDto>(`${this.base}/api/v1/cards/${id}/restore`, {});
  }

  permanentlyDeleteCard(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/api/v1/cards/${id}/permanent`);
  }

  streak(): Observable<StreakDto> {
    return this.http.get<StreakDto>(`${this.base}/api/v1/cards/streak`);
  }

  ratingDistribution(days = 30): Observable<RatingDistributionDto> {
    const params = new HttpParams().set('days', String(days));
    return this.http.get<RatingDistributionDto>(
      `${this.base}/api/v1/cards/rating-distribution`, { params });
  }

  activityHeatmap(days = 90): Observable<HeatmapDayDto[]> {
    const params = new HttpParams().set('days', String(days));
    return this.http.get<HeatmapDayDto[]>(
      `${this.base}/api/v1/cards/activity-heatmap`, { params });
  }

  stuckCards(take = 10, minConsecutiveForgot = 3, maxStage = 2): Observable<StuckCardDto[]> {
    const params = new HttpParams()
      .set('take', String(take))
      .set('minConsecutiveForgot', String(minConsecutiveForgot))
      .set('maxStage', String(maxStage));
    return this.http.get<StuckCardDto[]>(`${this.base}/api/v1/cards/stuck`, { params });
  }

  tagAverages(take = 10, minReviews = 3): Observable<TagAverageDto[]> {
    const params = new HttpParams()
      .set('take', String(take))
      .set('minReviews', String(minReviews));
    return this.http.get<TagAverageDto[]>(
      `${this.base}/api/v1/cards/tag-averages`, { params });
  }

  revealReminder(reminderId: string): Observable<RevealedAnswerDto> {
    return this.http.post<RevealedAnswerDto>(
      `${this.base}/api/v1/reminders/${reminderId}/reveal`, {});
  }

  skipReminder(reminderId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/api/v1/reminders/${reminderId}/skip`, {});
  }

  gradeAnswer(cardId: string, userAnswer: string): Observable<GradingResult> {
    return this.http.post<GradingResult>(
      `${this.base}/api/v1/cards/${cardId}/grade-answer`,
      { userAnswer });
  }

  recordReview(cardId: string, payload: RecordReviewPayload): Observable<ReviewDto> {
    return this.http.post<ReviewDto>(
      `${this.base}/api/v1/cards/${cardId}/review`, payload);
  }

  pauseCard(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/api/v1/cards/${id}/pause`, {});
  }

  unpauseCard(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/api/v1/cards/${id}/unpause`, {});
  }

  getMe(): Observable<CurrentUserDto> {
    return this.http.get<CurrentUserDto>(`${this.base}/api/v1/users/me`);
  }

  updateMe(payload: UpdateMePayload): Observable<void> {
    return this.http.patch<void>(`${this.base}/api/v1/users/me`, payload);
  }

  getIdentities(): Observable<UserIdentityDto[]> {
    return this.http.get<UserIdentityDto[]>(`${this.base}/api/v1/users/me/identities`);
  }
}
