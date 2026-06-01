import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../../environments/environment';
import {
  GradingResult,
  RecordReviewPayload,
  ReviewDto,
} from '../models/review.model';

@Injectable({ providedIn: 'root' })
export class ReviewsApiService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBase;

  gradeAnswer(cardId: string, userAnswer: string): Observable<GradingResult> {
    return this.http.post<GradingResult>(
      `${this.base}/api/v1/cards/${cardId}/grade-answer`,
      { userAnswer },
    );
  }

  recordReview(
    cardId: string,
    payload: RecordReviewPayload,
  ): Observable<ReviewDto> {
    return this.http.post<ReviewDto>(
      `${this.base}/api/v1/cards/${cardId}/review`,
      payload,
    );
  }
}
