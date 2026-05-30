import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import {
  CardDto,
  CardSummaryDto,
  CardWithGradeDto,
  DueReminderDto,
  PagedResult,
  TelegramLinkingTokenDto,
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

  listTags(): Observable<string[]> {
    return this.http.get<string[]>(`${this.base}/api/v1/tags`);
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
}
