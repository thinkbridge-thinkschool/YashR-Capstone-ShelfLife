import { Component, inject, signal, OnInit } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { InsightsService } from '../insights.service';
import { AuthService } from '../../shared/services/auth.service';
import { PopularTitleDto } from '../../shared/models/insights.models';

@Component({
  selector: 'app-popular-books',
  standalone: true,
  imports: [
    MatCardModule,
    MatTableModule,
    MatPaginatorModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './popular-books.component.html',
})
export class PopularBooksComponent implements OnInit {
  readonly auth = inject(AuthService);
  private readonly insights = inject(InsightsService);

  readonly data = signal<PopularTitleDto[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly displayedColumns = ['rank', 'title', 'author', 'borrowCount'];
  private currentPage = 1;
  private currentPageSize = 20;

  ngOnInit(): void {
    if (this.auth.isLibrarian()) this.load(1, 20);
  }

  load(page: number, pageSize: number): void {
    this.loading.set(true);
    this.error.set(null);
    this.currentPage = page;
    this.currentPageSize = pageSize;

    this.insights.getPopularTitles(page, pageSize).subscribe({
      next: res => {
        this.data.set(res.items);
        this.totalCount.set(res.totalCount);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Failed to load popular titles.');
      },
    });
  }

  onPage(event: PageEvent): void {
    this.load(event.pageIndex + 1, event.pageSize);
  }

  rankOf(index: number): number {
    return (this.currentPage - 1) * this.currentPageSize + index + 1;
  }
}
