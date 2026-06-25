import { Component, inject, signal, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { LendingService } from '../lending.service';
import { AuthService } from '../../shared/services/auth.service';
import { HoldDto } from '../../shared/models/lending.models';

@Component({
  selector: 'app-holds',
  standalone: true,
  imports: [
    DatePipe,
    MatCardModule,
    MatTableModule,
    MatPaginatorModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  templateUrl: './holds.component.html',
})
export class HoldsComponent implements OnInit {
  readonly auth = inject(AuthService);
  private readonly lendingService = inject(LendingService);

  readonly data = signal<HoldDto[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly searchValue = signal('');

  readonly displayedColumns = ['bookTitle', 'memberName', 'status', 'placedAt'];

  private currentPage = 1;
  private currentPageSize = 20;
  private searchTimer: ReturnType<typeof setTimeout> | null = null;

  ngOnInit(): void {
    if (this.auth.isLibrarian()) this.load(1, 20);
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
    this.lendingService.getHolds(page, pageSize, search).subscribe({
      next: res => {
        this.data.set(res.items);
        this.totalCount.set(res.totalCount);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Failed to load holds.');
      },
    });
  }

  onPage(event: PageEvent): void {
    this.load(event.pageIndex + 1, event.pageSize);
  }
}
