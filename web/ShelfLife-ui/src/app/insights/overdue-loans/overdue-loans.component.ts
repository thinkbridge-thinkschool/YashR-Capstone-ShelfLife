import { Component, inject, signal, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { InsightsService } from '../insights.service';
import { AuthService } from '../../shared/services/auth.service';
import { OverdueLoanDto } from '../../shared/models/insights.models';

@Component({
  selector: 'app-overdue-loans',
  standalone: true,
  imports: [
    DatePipe,
    MatCardModule,
    MatTableModule,
    MatPaginatorModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './overdue-loans.component.html',
})
export class OverdueLoansComponent implements OnInit {
  readonly auth = inject(AuthService);
  private readonly insights = inject(InsightsService);

  readonly data = signal<OverdueLoanDto[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly displayedColumns = ['loanId', 'memberName', 'bookTitle', 'dueDate', 'daysOverdue'];

  ngOnInit(): void {
    if (this.auth.isLibrarian()) this.load(1, 20);
  }

  load(page: number, pageSize: number): void {
    this.loading.set(true);
    this.error.set(null);

    this.insights.getOverdueLoans(page, pageSize).subscribe({
      next: res => {
        this.data.set(res.items);
        this.totalCount.set(res.totalCount);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Failed to load overdue loans.');
      },
    });
  }

  onPage(event: PageEvent): void {
    this.load(event.pageIndex + 1, event.pageSize);
  }
}
