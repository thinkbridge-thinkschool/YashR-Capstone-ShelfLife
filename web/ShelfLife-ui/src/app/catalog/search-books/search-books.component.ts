import { Component, inject, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSnackBar } from '@angular/material/snack-bar';
import { CatalogService } from '../catalog.service';
import { LendingService } from '../../lending/lending.service';
import { AuthService } from '../../shared/services/auth.service';
import { BookSummaryDto } from '../../shared/models/catalog.models';

@Component({
  selector: 'app-search-books',
  standalone: true,
  imports: [
    FormsModule,
    RouterLink,
    MatCardModule,
    MatTableModule,
    MatPaginatorModule,
    MatIconModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatFormFieldModule,
    MatInputModule,
    MatTooltipModule,
  ],
  templateUrl: './search-books.component.html',
})
export class SearchBooksComponent implements OnInit {
  private readonly catalog = inject(CatalogService);
  private readonly lending = inject(LendingService);
  private readonly snackBar = inject(MatSnackBar);
  readonly auth = inject(AuthService);

  readonly data = signal<BookSummaryDto[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly searchValue = signal('');
  readonly placingHoldFor = signal<string | null>(null);

  readonly displayedColumns = ['title', 'author', 'isbn', 'availability', 'action'];

  private searchTimer: ReturnType<typeof setTimeout> | null = null;
  private currentPage = 1;
  private currentPageSize = 20;

  ngOnInit(): void {
    this.load(1, 20);
  }

  onSearchInput(value: string): void {
    this.searchValue.set(value);
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.load(1, 20), 400);
  }

  load(page: number, pageSize: number): void {
    this.currentPage = page;
    this.currentPageSize = pageSize;
    this.loading.set(true);
    this.error.set(null);
    const search = this.searchValue() || undefined;
    this.catalog.getBooks(page, pageSize, search).subscribe({
      next: res => {
        this.data.set(res.items);
        this.totalCount.set(res.totalCount);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Failed to load books.');
      },
    });
  }

  placeHold(bookTitleId: string): void {
    this.placingHoldFor.set(bookTitleId);
    this.lending.placeHold(bookTitleId).subscribe({
      next: () => {
        this.placingHoldFor.set(null);
        this.snackBar.open('Hold placed successfully. You will be notified when the book is ready.', 'Dismiss', { duration: 4000 });
        this.load(this.currentPage, this.currentPageSize);
      },
      error: err => {
        this.placingHoldFor.set(null);
        const msg = typeof err.error === 'string' ? err.error : 'Failed to place hold.';
        this.snackBar.open(msg, 'Dismiss', { duration: 4000 });
      },
    });
  }

  onPage(event: PageEvent): void {
    this.load(event.pageIndex + 1, event.pageSize);
  }
}
