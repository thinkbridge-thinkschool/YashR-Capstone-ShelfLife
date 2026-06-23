import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  PagedList,
  PopularTitleDto,
  OverdueLoanDto,
  MemberActivityDto,
} from '../shared/models/insights.models';

@Injectable({ providedIn: 'root' })
export class InsightsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/api/v1/insights`;

  /** GET /api/v1/insights/popular-titles — requires Librarian role */
  getPopularTitles(page = 1, pageSize = 20): Observable<PagedList<PopularTitleDto>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedList<PopularTitleDto>>(`${this.base}/popular-titles`, { params });
  }

  /** GET /api/v1/insights/overdue-loans — requires Librarian role */
  getOverdueLoans(page = 1, pageSize = 20): Observable<PagedList<OverdueLoanDto>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedList<OverdueLoanDto>>(`${this.base}/overdue-loans`, { params });
  }

  /** GET /api/v1/insights/member-activity — requires Librarian role */
  getMemberActivity(page = 1, pageSize = 20): Observable<PagedList<MemberActivityDto>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedList<MemberActivityDto>>(`${this.base}/member-activity`, { params });
  }
}
