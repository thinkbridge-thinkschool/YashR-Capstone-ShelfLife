import { Component, inject, signal, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { LendingService } from '../lending.service';
import { MyLoanDto } from '../../shared/models/lending.models';

@Component({
  selector: 'app-my-loans',
  standalone: true,
  imports: [
    DatePipe,
    MatCardModule,
    MatTableModule,
    MatPaginatorModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './my-loans.component.html',
})
export class MyLoansComponent implements OnInit {
  private readonly lending = inject(LendingService);

  readonly data = signal<MyLoanDto[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly displayedColumns = ['bookTitle', 'borrowedAt', 'dueDate', 'status'];

  ngOnInit(): void {
    this.load(1, 20);
  }

  load(page: number, pageSize: number): void {
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

  onPage(event: PageEvent): void {
    this.load(event.pageIndex + 1, event.pageSize);
  }
}
