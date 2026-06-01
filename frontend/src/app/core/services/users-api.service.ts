import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import {
  CurrentUserDto,
  TelegramLinkingTokenDto,
  TimeZoneDto,
  UpdateMePayload,
  UserIdentityDto,
} from '../api/dto';

@Injectable({ providedIn: 'root' })
export class UsersApiService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBase;

  getMe(): Observable<CurrentUserDto> {
    return this.http.get<CurrentUserDto>(`${this.base}/api/v1/users/me`);
  }

  updateMe(payload: UpdateMePayload): Observable<void> {
    return this.http.patch<void>(`${this.base}/api/v1/users/me`, payload);
  }

  getIdentities(): Observable<UserIdentityDto[]> {
    return this.http.get<UserIdentityDto[]>(
      `${this.base}/api/v1/users/me/identities`,
    );
  }

  listTimeZones(): Observable<TimeZoneDto[]> {
    return this.http.get<TimeZoneDto[]>(`${this.base}/api/v1/timezones`);
  }

  startTelegramLinking(): Observable<TelegramLinkingTokenDto> {
    return this.http.post<TelegramLinkingTokenDto>(
      `${this.base}/api/v1/auth/telegram-linking/start`,
      {},
    );
  }
}
