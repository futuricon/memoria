import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../../environments/environment';
import { TagDto } from '../../../core/api/dto';

@Injectable({ providedIn: 'root' })
export class TagsApiService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBase;

  listTags(): Observable<TagDto[]> {
    return this.http.get<TagDto[]>(`${this.base}/api/v1/tags`);
  }

  listPopularTags(count: number): Observable<TagDto[]> {
    const params = new HttpParams().set('count', String(count));
    return this.http.get<TagDto[]>(`${this.base}/api/v1/tags/popular`, { params });
  }
}
