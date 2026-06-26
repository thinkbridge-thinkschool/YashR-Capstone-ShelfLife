import { Component, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatAutocompleteModule, MatAutocompleteSelectedEvent } from '@angular/material/autocomplete';
import { debounceTime, distinctUntilChanged, switchMap, catchError, of } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { LendingService } from '../lending.service';
import { MembersService } from '../../shared/services/members.service';
import { CatalogService } from '../../catalog/catalog.service';
import { MemberSummaryDto } from '../../shared/models/identity.models';
import { BookSummaryDto } from '../../shared/models/catalog.models';

@Component({
  selector: 'app-borrow-book',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    MatAutocompleteModule,
  ],
  templateUrl: './borrow-book.component.html',
})
export class BorrowBookComponent {
  private readonly lending = inject(LendingService);
  private readonly membersService = inject(MembersService);
  private readonly catalogService = inject(CatalogService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly router = inject(Router);

  readonly memberSearch = new FormControl('');
  readonly bookSearch = new FormControl('');

  private readonly _selectedMemberId = signal<string | null>(null);
  private readonly _selectedBookTitleId = signal<string | null>(null);
  private readonly _selectedBookAvailableCopies = signal<number | null>(null);
  readonly selectedMemberId = this._selectedMemberId.asReadonly();
  readonly selectedBookTitleId = this._selectedBookTitleId.asReadonly();
  readonly selectedBookAvailableCopies = this._selectedBookAvailableCopies.asReadonly();

  readonly memberOptions = signal<MemberSummaryDto[]>([]);
  readonly bookOptions = signal<BookSummaryDto[]>([]);

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  constructor() {
    this.memberSearch.valueChanges.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      switchMap(q => q && q.length >= 2
        ? this.membersService.searchMembers(q).pipe(catchError(() => of({ items: [] as MemberSummaryDto[] })))
        : of({ items: [] as MemberSummaryDto[] })
      ),
      takeUntilDestroyed()
    ).subscribe(res => this.memberOptions.set(res.items));

    this.bookSearch.valueChanges.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      switchMap(q => q && q.length >= 2
        ? this.catalogService.getBooks(1, 8, q).pipe(catchError(() => of({ items: [] as BookSummaryDto[] })))
        : of({ items: [] as BookSummaryDto[] })
      ),
      takeUntilDestroyed()
    ).subscribe(res => this.bookOptions.set(res.items));
  }

  onMemberSelected(event: MatAutocompleteSelectedEvent): void {
    const member = event.option.value as MemberSummaryDto;
    this._selectedMemberId.set(member.memberId);
    this.memberSearch.setValue(`${member.fullName} (${member.email})`, { emitEvent: false });
  }

  onBookSelected(event: MatAutocompleteSelectedEvent): void {
    const book = event.option.value as BookSummaryDto;
    this._selectedBookTitleId.set(book.bookTitleId);
    this._selectedBookAvailableCopies.set(book.availableCopies);
    this.bookSearch.setValue(`${book.title} — ${book.author}`, { emitEvent: false });
  }

  onMemberClear(): void {
    this._selectedMemberId.set(null);
    this.memberSearch.setValue('');
  }

  onBookClear(): void {
    this._selectedBookTitleId.set(null);
    this._selectedBookAvailableCopies.set(null);
    this.bookSearch.setValue('');
  }

  onSubmit(): void {
    const memberId = this._selectedMemberId();
    const bookTitleId = this._selectedBookTitleId();

    if (!memberId) { this.error.set('Please select a member from the dropdown.'); return; }
    if (!bookTitleId) { this.error.set('Please select a book from the dropdown.'); return; }

    this.loading.set(true);
    this.error.set(null);

    this.lending.borrowBook(memberId, bookTitleId).subscribe({
      next: () => {
        this.loading.set(false);
        this.onMemberClear();
        this.onBookClear();
        this.snackBar.open('Loan issued! Redirecting to Active Loans…', 'Dismiss', { duration: 3000 });
        setTimeout(() => this.router.navigate(['/lending/loans']), 1500);
      },
      error: err => {
        this.loading.set(false);
        let msg: string;
        if (err.status === 0) {
          msg = 'Cannot reach the API. Make sure the server is running on port 5000.';
        } else if (err.status === 401) {
          msg = 'Session expired — please log out and log in again.';
        } else if (err.status === 429) {
          msg = 'Too many requests — wait a moment and try again.';
        } else if (typeof err.error === 'string' && err.error) {
          msg = err.error;
        } else if (err.error?.title) {
          msg = err.error.title;
        } else {
          msg = `Server error (${err.status}). Check API logs for details.`;
        }
        this.error.set(msg);
      },
    });
  }

}
