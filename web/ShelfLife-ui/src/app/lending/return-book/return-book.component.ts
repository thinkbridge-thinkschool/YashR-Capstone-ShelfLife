import { Component, inject, signal } from '@angular/core';
import { FormBuilder, Validators, ReactiveFormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { LendingService } from '../lending.service';

@Component({
  selector: 'app-return-book',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './return-book.component.html',
})
export class ReturnBookComponent {
  private readonly fb = inject(FormBuilder);
  private readonly lending = inject(LendingService);
  private readonly snackBar = inject(MatSnackBar);

  readonly form = this.fb.nonNullable.group({
    loanId: ['', [Validators.required]],
  });

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal(false);
  readonly returnedLoanId = signal<string | null>(null);

  onSubmit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.loading.set(true);
    this.error.set(null);
    this.success.set(false);

    const loanId = this.form.getRawValue().loanId.trim();
    this.lending.returnBook(loanId).subscribe({
      next: () => {
        this.loading.set(false);
        this.success.set(true);
        this.returnedLoanId.set(loanId);
        this.snackBar.open('Book returned successfully.', 'Close', { duration: 4000 });
        this.form.reset();
      },
      error: err => {
        this.loading.set(false);
        const msg = typeof err.error === 'string' ? err.error : 'Failed to return book.';
        this.error.set(msg);
      },
    });
  }
}
