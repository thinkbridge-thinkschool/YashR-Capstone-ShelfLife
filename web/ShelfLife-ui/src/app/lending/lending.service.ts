import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { BorrowBookResponse } from '../shared/models/lending.models';

@Injectable({ providedIn: 'root' })
export class LendingService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/api/v1/lending`;

  /** POST /api/v1/lending/loans — body: { bookTitleId } — memberId resolved from JWT sub */
  borrowBook(bookTitleId: string): Observable<BorrowBookResponse> {
    return this.http.post<BorrowBookResponse>(`${this.base}/loans`, { bookTitleId });
  }

  /** POST /api/v1/lending/loans/{loanId}/return */
  returnBook(loanId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/loans/${loanId}/return`, {});
  }
}
