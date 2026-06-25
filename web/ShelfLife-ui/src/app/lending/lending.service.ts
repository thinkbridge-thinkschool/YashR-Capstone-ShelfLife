import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { BorrowBookResponse, HoldDto, LoanSummaryDto, MyLoanDto, MyHoldDto } from '../shared/models/lending.models';
import { PagedList } from '../shared/models/insights.models';

@Injectable({ providedIn: 'root' })
export class LendingService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/api/v1/lending`;

  /** POST /api/v1/lending/loans — body: { bookTitleId } — memberId resolved from JWT sub */
  borrowBook(memberId: string, bookTitleId: string): Observable<BorrowBookResponse> {
    return this.http.post<BorrowBookResponse>(`${this.base}/loans`, { memberId, bookTitleId });
  }

  /** POST /api/v1/lending/loans/{loanId}/return */
  returnBook(loanId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/loans/${loanId}/return`, {});
  }

  /** GET /api/v1/lending/loans — requires Librarian role */
  getLoans(page = 1, pageSize = 20, activeOnly = false, search?: string): Observable<PagedList<LoanSummaryDto>> {
    let params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize)
      .set('activeOnly', activeOnly);
    if (search) params = params.set('search', search);
    return this.http.get<PagedList<LoanSummaryDto>>(`${this.base}/loans`, { params });
  }

  /** GET /api/v1/lending/holds — requires Librarian role */
  getHolds(page = 1, pageSize = 20, search?: string): Observable<PagedList<HoldDto>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search) params = params.set('search', search);
    return this.http.get<PagedList<HoldDto>>(`${this.base}/holds`, { params });
  }

  /** GET /api/v1/lending/my-loans — member's own loans */
  getMyLoans(page = 1, pageSize = 20, activeOnly = false): Observable<PagedList<MyLoanDto>> {
    const params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize)
      .set('activeOnly', activeOnly);
    return this.http.get<PagedList<MyLoanDto>>(`${this.base}/my-loans`, { params });
  }

  /** POST /api/v1/lending/holds — memberId resolved from JWT sub */
  placeHold(bookTitleId: string): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.base}/holds`, { bookTitleId });
  }

  /** GET /api/v1/lending/my-holds — member's own holds */
  getMyHolds(page = 1, pageSize = 20): Observable<PagedList<MyHoldDto>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedList<MyHoldDto>>(`${this.base}/my-holds`, { params });
  }
}
