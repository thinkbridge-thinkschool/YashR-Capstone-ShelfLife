import { Component, inject, signal, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../shared/services/auth.service';
import { InsightsService } from '../insights/insights.service';
import { LendingService } from '../lending/lending.service';
import { IdentityService } from '../identity/identity.service';

type CardStatus = 'loading' | 'loaded' | 'no-api' | 'role-restricted' | 'error';

interface MetricCard {
  title: string;
  icon: string;
  iconBg: string;
  value: number | null;
  status: CardStatus;
  subtitle: string;
  route?: string;
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    DatePipe,
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
  private readonly lending = inject(LendingService);
  private readonly identity = inject(IdentityService);
  readonly today = new Date();

  readonly cards = signal<MetricCard[]>([]);

  ngOnInit(): void {
    if (this.auth.isLibrarian()) {
      this.initLibrarianCards();
    } else {
      this.initMemberCards();
    }
  }

  private initLibrarianCards(): void {
    this.cards.set([
      { title: 'Overdue Loans',  icon: 'warning_amber', iconBg: '#e53935', value: null, status: 'loading', subtitle: 'Total overdue',            route: '/insights/overdue'    },
      { title: 'Popular Titles', icon: 'trending_up',   iconBg: '#3f51b5', value: null, status: 'loading', subtitle: 'Tracked titles',            route: '/insights/popular'    },
      { title: 'Active Loans',   icon: 'book',          iconBg: '#43a047', value: null, status: 'loading', subtitle: 'Currently checked out',     route: '/lending/loans'       },
      { title: 'Members',        icon: 'people',        iconBg: '#fb8c00', value: null, status: 'loading', subtitle: 'Registered members',        route: '/identity/members'    },
    ]);

    this.insights.getOverdueLoans(1, 1).subscribe({
      next: r => this.patch(0, r.totalCount, 'loaded'),
      error: () => this.patch(0, null, 'error'),
    });

    this.insights.getPopularTitles(1, 1).subscribe({
      next: r => this.patch(1, r.totalCount, 'loaded'),
      error: () => this.patch(1, null, 'error'),
    });

    this.lending.getLoans(1, 1, true).subscribe({
      next: r => this.patch(2, r.totalCount, 'loaded'),
      error: () => this.patch(2, null, 'error'),
    });

    this.identity.getMembers(1, 1).subscribe({
      next: r => this.patch(3, r.totalCount, 'loaded'),
      error: () => this.patch(3, null, 'error'),
    });
  }

  private initMemberCards(): void {
    this.cards.set([
      { title: 'My Active Loans', icon: 'book', iconBg: '#1565c0', value: null, status: 'loading', subtitle: 'Currently borrowed', route: '/lending/my-loans' },
      { title: 'My Holds', icon: 'bookmark', iconBg: '#6a1b9a', value: null, status: 'loading', subtitle: 'Books on hold', route: '/lending/my-holds' },
    ]);

    this.lending.getMyLoans(1, 1, true).subscribe({
      next: r => this.patch(0, r.totalCount, 'loaded'),
      error: () => this.patch(0, null, 'error'),
    });

    this.lending.getMyHolds(1, 1).subscribe({
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
