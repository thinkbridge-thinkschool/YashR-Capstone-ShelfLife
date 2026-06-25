import { Component, inject, signal, OnInit } from '@angular/core';
import { DatePipe, SlicePipe } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSnackBar } from '@angular/material/snack-bar';
import { IdentityService } from '../identity.service';
import { AuthService } from '../../shared/services/auth.service';
import { MemberSummaryDto } from '../../shared/models/identity.models';

@Component({
  selector: 'app-members',
  standalone: true,
  imports: [
    DatePipe,
    SlicePipe,
    MatCardModule,
    MatTableModule,
    MatPaginatorModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatFormFieldModule,
    MatInputModule,
    MatTooltipModule,
  ],
  templateUrl: './members.component.html',
})
export class MembersComponent implements OnInit {
  readonly auth = inject(AuthService);
  private readonly identityService = inject(IdentityService);

  readonly data = signal<MemberSummaryDto[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly searchValue = signal('');
  private readonly snackBar = inject(MatSnackBar);

  readonly displayedColumns = ['fullName', 'email', 'role', 'status', 'registeredAt', 'memberId'];

  readonly copiedId = signal<string | null>(null);
  private currentPage = 1;
  private currentPageSize = 20;
  private searchTimer: ReturnType<typeof setTimeout> | null = null;

  copyId(id: string): void {
    navigator.clipboard.writeText(id).then(() => {
      this.copiedId.set(id);
      this.snackBar.open('Member ID copied to clipboard', 'Dismiss', { duration: 2000 });
      setTimeout(() => this.copiedId.set(null), 2000);
    });
  }

  onSearchInput(value: string): void {
    this.searchValue.set(value);
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.load(1, this.currentPageSize), 400);
  }

  ngOnInit(): void {
    if (this.auth.isLibrarian()) this.load(1, 20);
  }

  load(page: number, pageSize: number): void {
    this.currentPage = page;
    this.currentPageSize = pageSize;
    this.loading.set(true);
    this.error.set(null);
    const search = this.searchValue() || undefined;
    this.identityService.getMembers(page, pageSize, search).subscribe({
      next: res => {
        this.data.set(res.items);
        this.totalCount.set(res.totalCount);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Failed to load members.');
      },
    });
  }

  onPage(event: PageEvent): void {
    this.load(event.pageIndex + 1, event.pageSize);
  }
}
