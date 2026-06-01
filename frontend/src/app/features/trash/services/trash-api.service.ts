import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../../environments/environment';
import { PagedResult } from '../../../shared/models/paged-result.model';
import { CardDto } from '../../cards/models/card.model';
import { TrashedCardDto } from '../models/trashed-card.model';

@Injectable({ providedIn: 'root' })
export class TrashApiService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBase;

  listTrash(page = 1, pageSize = 10): Observable<PagedResult<TrashedCardDto>> {
    const params = new HttpParams()
      .set('page', String(page))
      .set('pageSize', String(pageSize));
    return this.http.get<PagedResult<TrashedCardDto>>(
      `${this.base}/api/v1/cards/trash`,
      { params },
    );
  }

  restoreCard(id: string): Observable<CardDto> {
    return this.http.post<CardDto>(`${this.base}/api/v1/cards/${id}/restore`, {});
  }

  permanentlyDeleteCard(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/api/v1/cards/${id}/permanent`);
  }
}
