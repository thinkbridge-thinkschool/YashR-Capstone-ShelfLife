import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { MemberSummaryDto } from '../models/identity.models';
import { PagedList } from '../models/insights.models';

@Injectable({ providedIn: 'root' })
export class MembersService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/api/v1/identity`;

  searchMembers(search: string, pageSize = 8): Observable<PagedList<MemberSummaryDto>> {
    const params = new HttpParams()
      .set('page', 1)
      .set('pageSize', pageSize)
      .set('search', search);
    return this.http.get<PagedList<MemberSummaryDto>>(`${this.base}/members`, { params });
  }
}
