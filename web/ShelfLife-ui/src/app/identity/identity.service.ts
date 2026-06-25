import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { PagedList } from '../shared/models/insights.models';
import { MemberSummaryDto } from '../shared/models/identity.models';

@Injectable({ providedIn: 'root' })
export class IdentityService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/api/v1/identity`;

  /** GET /api/v1/identity/members — requires Librarian role */
  getMembers(page = 1, pageSize = 20, search?: string): Observable<PagedList<MemberSummaryDto>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search) params = params.set('search', search);
    return this.http.get<PagedList<MemberSummaryDto>>(`${this.base}/members`, { params });
  }
}
