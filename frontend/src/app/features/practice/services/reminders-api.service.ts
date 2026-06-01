import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../../environments/environment';
import { DueReminderDto, RevealedAnswerDto } from '../../../core/api/dto';

@Injectable({ providedIn: 'root' })
export class RemindersApiService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBase;

  dueToday(): Observable<DueReminderDto[]> {
    return this.http.get<DueReminderDto[]>(`${this.base}/api/v1/cards/due-today`);
  }

  upcoming(take = 10): Observable<DueReminderDto[]> {
    const params = new HttpParams().set('take', String(take));
    return this.http.get<DueReminderDto[]>(`${this.base}/api/v1/cards/upcoming`, { params });
  }

  revealReminder(reminderId: string): Observable<RevealedAnswerDto> {
    return this.http.post<RevealedAnswerDto>(
      `${this.base}/api/v1/reminders/${reminderId}/reveal`,
      {},
    );
  }

  skipReminder(reminderId: string): Observable<void> {
    return this.http.post<void>(
      `${this.base}/api/v1/reminders/${reminderId}/skip`,
      {},
    );
  }
}
