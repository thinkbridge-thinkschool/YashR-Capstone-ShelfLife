import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { AddBookResponse, AddCopyResponse, BookSummaryDto } from '../shared/models/catalog.models';
import { PagedList } from '../shared/models/insights.models';

@Injectable({ providedIn: 'root' })
export class CatalogService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/api/v1/catalog`;

  /** POST /api/v1/catalog/books — body: { isbn } — requires Librarian role */
  addBook(isbn: string): Observable<AddBookResponse> {
    return this.http.post<AddBookResponse>(`${this.base}/books`, { isbn });
  }

  /** POST /api/v1/catalog/books/{bookTitleId}/copies — body: { barcode } — requires Librarian role */
  addCopy(bookTitleId: string, barcode: string): Observable<AddCopyResponse> {
    return this.http.post<AddCopyResponse>(
      `${this.base}/books/${bookTitleId}/copies`,
      { barcode }
    );
  }

  /** GET /api/v1/catalog/books — available to all authenticated users */
  getBooks(page = 1, pageSize = 20, search?: string): Observable<PagedList<BookSummaryDto>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search) params = params.set('search', search);
    return this.http.get<PagedList<BookSummaryDto>>(`${this.base}/books`, { params });
  }
}
