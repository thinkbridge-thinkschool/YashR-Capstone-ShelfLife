import { Component, inject, signal } from '@angular/core';
import { FormBuilder, Validators, ReactiveFormsModule } from '@angular/forms';
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
import { LendingService } from '../lending.service';
import { BorrowBookResponse } from '../../shared/models/lending.models';

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
  ],
  templateUrl: './borrow-book.component.html',
})
export class BorrowBookComponent {
  private readonly fb = inject(FormBuilder);
  private readonly lending = inject(LendingService);
  private readonly snackBar = inject(MatSnackBar);

  readonly form = this.fb.nonNullable.group({
    memberId: ['', [Validators.required]],
    bookTitleId: ['', [Validators.required]],
  });

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly result = signal<BorrowBookResponse | null>(null);
  readonly copied = signal(false);

  readonly displayedColumns = ['loanId', 'dueDate', 'status'];

  onSubmit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.loading.set(true);
    this.error.set(null);

    const { memberId, bookTitleId } = this.form.getRawValue();
    this.lending.borrowBook(memberId.trim(), bookTitleId.trim()).subscribe({
      next: res => {
        this.loading.set(false);
        this.result.set(res);
        this.form.reset();
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
