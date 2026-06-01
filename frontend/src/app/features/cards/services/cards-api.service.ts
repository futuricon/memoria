import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../../environments/environment';
import {
  AddCardPayload,
  CardDto,
  CardSummaryDto,
  PagedResult,
  UpdateCardPayload,
} from '../../../core/api/dto';

@Injectable({ providedIn: 'root' })
export class CardsApiService {
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
    if (opts.tags && opts.tags.length > 0) {
      params = params.set('tags', opts.tags.join(','));
    }
    return this.http.get<PagedResult<CardSummaryDto>>(
      `${this.base}/api/v1/cards`,
      { params },
    );
  }

  getCard(id: string): Observable<CardDto> {
    return this.http.get<CardDto>(`${this.base}/api/v1/cards/${id}`);
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

  pauseCard(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/api/v1/cards/${id}/pause`, {});
  }

  unpauseCard(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/api/v1/cards/${id}/unpause`, {});
  }
}
