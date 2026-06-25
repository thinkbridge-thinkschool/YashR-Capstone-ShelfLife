import { Component, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatAutocompleteModule, MatAutocompleteSelectedEvent } from '@angular/material/autocomplete';
import { debounceTime, distinctUntilChanged, switchMap, catchError, of } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { LendingService } from '../lending.service';
import { MembersService } from '../../shared/services/members.service';
import { CatalogService } from '../../catalog/catalog.service';
import { BorrowBookResponse } from '../../shared/models/lending.models';
import { MemberSummaryDto } from '../../shared/models/identity.models';
import { BookSummaryDto } from '../../shared/models/catalog.models';

@Component({
  selector: 'app-borrow-book',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    DatePipe,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatTableModule,
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

  readonly memberSearch = new FormControl('');
  readonly bookSearch = new FormControl('');

  private readonly _selectedMemberId = signal<string | null>(null);
  private readonly _selectedBookTitleId = signal<string | null>(null);
  readonly selectedMemberId = this._selectedMemberId.asReadonly();
  readonly selectedBookTitleId = this._selectedBookTitleId.asReadonly();

  readonly memberOptions = signal<MemberSummaryDto[]>([]);
  readonly bookOptions = signal<BookSummaryDto[]>([]);

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly result = signal<BorrowBookResponse | null>(null);
  readonly copied = signal(false);

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
    this.bookSearch.setValue(`${book.title} — ${book.author}`, { emitEvent: false });
  }

  onMemberClear(): void {
    this._selectedMemberId.set(null);
    this.memberSearch.setValue('');
  }

  onBookClear(): void {
    this._selectedBookTitleId.set(null);
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
      next: res => {
        this.loading.set(false);
        this.result.set(res);
        this.onMemberClear();
        this.onBookClear();
        this.snackBar.open('Loan issued successfully.', 'Dismiss', { duration: 4000 });
      },
      error: err => {
        this.loading.set(false);
        const msg = typeof err.error === 'string' ? err.error : 'Failed to borrow book.';
        this.error.set(msg);
      },
    });
  }

  copyLoanId(value: string): void {
    navigator.clipboard.writeText(value).then(() => {
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 2000);
    });
  }
}
