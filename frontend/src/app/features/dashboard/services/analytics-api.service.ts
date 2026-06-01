import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../../environments/environment';
import {
  CardWithGradeDto,
  HeatmapDayDto,
  RatingDistributionDto,
  StreakDto,
  StuckCardDto,
  TagAverageDto,
} from '../../../core/api/dto';

@Injectable({ providedIn: 'root' })
export class AnalyticsApiService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBase;

  worst(take = 5, minReviews = 3): Observable<CardWithGradeDto[]> {
    const params = new HttpParams()
      .set('take', String(take))
      .set('minReviews', String(minReviews));
    return this.http.get<CardWithGradeDto[]>(
      `${this.base}/api/v1/cards/worst`,
      { params },
    );
  }

  streak(): Observable<StreakDto> {
    return this.http.get<StreakDto>(`${this.base}/api/v1/cards/streak`);
  }

  ratingDistribution(days = 30): Observable<RatingDistributionDto> {
    const params = new HttpParams().set('days', String(days));
    return this.http.get<RatingDistributionDto>(
      `${this.base}/api/v1/cards/rating-distribution`,
      { params },
    );
  }

  activityHeatmap(days = 90): Observable<HeatmapDayDto[]> {
    const params = new HttpParams().set('days', String(days));
    return this.http.get<HeatmapDayDto[]>(
      `${this.base}/api/v1/cards/activity-heatmap`,
      { params },
    );
  }

  stuckCards(
    take = 10,
    minConsecutiveForgot = 3,
    maxStage = 2,
  ): Observable<StuckCardDto[]> {
    const params = new HttpParams()
      .set('take', String(take))
      .set('minConsecutiveForgot', String(minConsecutiveForgot))
      .set('maxStage', String(maxStage));
    return this.http.get<StuckCardDto[]>(`${this.base}/api/v1/cards/stuck`, {
      params,
    });
  }

  tagAverages(take = 10, minReviews = 3): Observable<TagAverageDto[]> {
    const params = new HttpParams()
      .set('take', String(take))
      .set('minReviews', String(minReviews));
    return this.http.get<TagAverageDto[]>(
      `${this.base}/api/v1/cards/tag-averages`,
      { params },
    );
  }
}
