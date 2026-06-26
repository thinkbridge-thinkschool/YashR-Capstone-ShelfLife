import { Component, inject, signal, OnInit, DestroyRef } from '@angular/core';
import { DatePipe } from '@angular/common';
import { timer } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { LendingService } from '../lending.service';
import { MyLoanDto } from '../../shared/models/lending.models';

const POLL_INTERVAL_MS = 15_000;

@Component({
  selector: 'app-my-loans',
  standalone: true,
  imports: [
    DatePipe,
    MatCardModule,
    MatTableModule,
    MatPaginatorModule,
    MatIconModule,
    MatButtonModule,
    MatTooltipModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './my-loans.component.html',
})
export class MyLoansComponent implements OnInit {
  private readonly lending = inject(LendingService);
  private readonly destroyRef = inject(DestroyRef);

  readonly data = signal<MyLoanDto[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly displayedColumns = ['bookTitle', 'borrowedAt', 'dueDate', 'status'];

  private currentPage = 1;
  private currentPageSize = 20;

  ngOnInit(): void {
    this.load(this.currentPage, this.currentPageSize);
    // Auto-refresh silently so member sees new loans without manual refresh
    timer(POLL_INTERVAL_MS, POLL_INTERVAL_MS)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.silentRefresh());
  }

  load(page: number, pageSize: number): void {
    this.currentPage = page;
    this.currentPageSize = pageSize;
    this.loading.set(true);
    this.error.set(null);
    this.lending.getMyLoans(page, pageSize).subscribe({
      next: res => {
        this.data.set(res.items);
        this.totalCount.set(res.totalCount);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Failed to load your loans.');
      },
    });
  }

  private silentRefresh(): void {
    this.lending.getMyLoans(this.currentPage, this.currentPageSize).subscribe({
      next: res => {
        this.data.set(res.items);
        this.totalCount.set(res.totalCount);
      },
      error: () => {},
    });
  }

  onPage(event: PageEvent): void {
    this.load(event.pageIndex + 1, event.pageSize);
  }

  refresh(): void {
    this.load(this.currentPage, this.currentPageSize);
  }
}
