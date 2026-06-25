import { Component, inject, signal, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
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
import { LendingService } from '../lending.service';
import { LoanSummaryDto } from '../../shared/models/lending.models';

@Component({
  selector: 'app-loans',
  standalone: true,
  imports: [
    DatePipe,
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
  templateUrl: './loans.component.html',
})
export class LoansComponent implements OnInit {
  private readonly lending = inject(LendingService);
  private readonly route = inject(ActivatedRoute);
  private readonly snackBar = inject(MatSnackBar);

  readonly data = signal<LoanSummaryDto[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly returningId = signal<string | null>(null);
  readonly activeOnly = signal(true);
  readonly pageTitle = signal('Active Loans');
  readonly pageSubtitle = signal('Books currently checked out. Click Return to process a return.');
  readonly searchValue = signal('');

  displayedColumns: string[] = [];

  private currentPage = 1;
  private currentPageSize = 20;
  private searchTimer: ReturnType<typeof setTimeout> | null = null;

  ngOnInit(): void {
    const data = this.route.snapshot.data;
    const isActive: boolean = data['activeOnly'] ?? true;
    this.activeOnly.set(isActive);
    this.pageTitle.set(data['title'] ?? 'Active Loans');
    this.pageSubtitle.set(
      isActive
        ? 'Books currently checked out. Click Return to process a return.'
        : 'Complete history of all loans — active, overdue, and returned.'
    );
    this.displayedColumns = isActive
      ? ['member', 'book', 'borrowedAt', 'dueDate', 'status', 'actions']
      : ['member', 'book', 'borrowedAt', 'dueDate', 'status'];
    this.load(1, 20);
  }

  onSearchInput(value: string): void {
    this.searchValue.set(value);
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.load(1, this.currentPageSize), 400);
  }

  load(page: number, pageSize: number): void {
    this.currentPage = page;
    this.currentPageSize = pageSize;
    this.loading.set(true);
    this.error.set(null);
    const search = this.searchValue() || undefined;
    this.lending.getLoans(page, pageSize, this.activeOnly(), search).subscribe({
      next: res => {
        this.data.set(res.items);
        this.totalCount.set(res.totalCount);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Failed to load loans.');
      },
    });
  }

  returnLoan(loanId: string): void {
    this.returningId.set(loanId);
    this.lending.returnBook(loanId).subscribe({
      next: () => {
        this.returningId.set(null);
        this.snackBar.open('Book returned successfully.', 'Dismiss', { duration: 3000 });
        this.load(this.currentPage, this.currentPageSize);
      },
      error: err => {
        this.returningId.set(null);
        const msg = typeof err.error === 'string' ? err.error : 'Failed to return book.';
        this.snackBar.open(msg, 'Dismiss', { duration: 4000 });
      },
    });
  }

  onPage(event: PageEvent): void {
    this.load(event.pageIndex + 1, event.pageSize);
  }
}
