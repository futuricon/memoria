import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../../environments/environment';
import { AdminOverviewDto } from '../models/admin-overview.model';
import {
  AdminUserListQuery,
  AdminUserPageDto,
} from '../models/admin-user.model';

@Injectable({ providedIn: 'root' })
export class AdminApiService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBase;

  listUsers(query: AdminUserListQuery = {}): Observable<AdminUserPageDto> {
    let params = new HttpParams();
    if (query.page !== undefined) params = params.set('page', String(query.page));
    if (query.pageSize !== undefined) params = params.set('pageSize', String(query.pageSize));
    if (query.search) params = params.set('search', query.search);
    if (query.sort) params = params.set('sort', query.sort);

    return this.http.get<AdminUserPageDto>(
      `${this.base}/api/v1/admin/users`,
      { params },
    );
  }

  overview(): Observable<AdminOverviewDto> {
    return this.http.get<AdminOverviewDto>(`${this.base}/api/v1/admin/overview`);
  }
}
