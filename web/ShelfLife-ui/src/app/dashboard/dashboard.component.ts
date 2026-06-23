import { Component, inject, signal, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../shared/services/auth.service';
import { InsightsService } from '../insights/insights.service';

type CardStatus = 'loading' | 'loaded' | 'no-api' | 'role-restricted' | 'error';

interface MetricCard {
  title: string;
  icon: string;
  iconBg: string;
  value: number | null;
  status: CardStatus;
  /** Shown beneath the value. Set at construction; overridden on no-api/role-restricted. */
  subtitle: string;
  route?: string;
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    RouterLink,
    MatCardModule,
    MatIconModule,
    MatButtonModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './dashboard.component.html',
})
export class DashboardComponent implements OnInit {
  readonly auth = inject(AuthService);
  private readonly insights = inject(InsightsService);

  readonly cards = signal<MetricCard[]>([
    {
      title: 'Overdue Loans',
      icon: 'warning_amber',
      iconBg: '#e53935',
      value: null,
      status: 'loading',
      subtitle: 'Total overdue',
      route: '/insights/overdue',
    },
    {
      title: 'Popular Titles',
      icon: 'trending_up',
      iconBg: '#3f51b5',
      value: null,
      status: 'loading',
      subtitle: 'Tracked titles',
      route: '/insights/popular',
    },
    {
      // No GET /lending/loans endpoint exists — always placeholder
      title: 'Active Loans',
      icon: 'book',
      iconBg: '#43a047',
      value: null,
      status: 'no-api',
      subtitle: 'Data not available from current API',
    },
    {
      // No GET /identity/members endpoint exists — always placeholder
      title: 'Members',
      icon: 'people',
      iconBg: '#fb8c00',
      value: null,
      status: 'no-api',
      subtitle: 'Data not available from current API',
    },
  ]);

  ngOnInit(): void {
    if (!this.auth.isLibrarian()) {
      // Insights endpoints require Librarian role — mark those cards accordingly
      this.cards.update(cs =>
        cs.map(c =>
          c.status === 'loading'
            ? { ...c, status: 'role-restricted' as CardStatus, subtitle: 'Requires Librarian access' }
            : c
        )
      );
      return;
    }

    // Fetch each insight with page=1&pageSize=1 — we only need totalCount
    this.insights.getOverdueLoans(1, 1).subscribe({
      next: r => this.patch(0, r.totalCount, 'loaded'),
      error: () => this.patch(0, null, 'error'),
    });

    this.insights.getPopularTitles(1, 1).subscribe({
      next: r => this.patch(1, r.totalCount, 'loaded'),
      error: () => this.patch(1, null, 'error'),
    });
  }

  private patch(idx: number, value: number | null, status: CardStatus): void {
    this.cards.update(cs => {
      const updated = [...cs];
      updated[idx] = { ...updated[idx], value, status };
      return updated;
    });
  }
}
