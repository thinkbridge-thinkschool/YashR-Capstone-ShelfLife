import { Component, inject, signal } from '@angular/core';
import { FormBuilder, Validators, ReactiveFormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { CatalogService } from '../catalog.service';
import { AuthService } from '../../shared/services/auth.service';

interface BookResult {
  bookTitleId: string;
}

@Component({
  selector: 'app-add-book',
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
  ],
  templateUrl: './add-book.component.html',
})
export class AddBookComponent {
  private readonly fb = inject(FormBuilder);
  private readonly catalog = inject(CatalogService);
  readonly auth = inject(AuthService);

  readonly form = this.fb.nonNullable.group({
    isbn: ['', [Validators.required]],
  });

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly result = signal<BookResult | null>(null);
  readonly copied = signal(false);

  onSubmit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.loading.set(true);
    this.error.set(null);
    this.result.set(null);

    this.catalog.addBook(this.form.getRawValue().isbn.trim()).subscribe({
      next: res => {
        this.loading.set(false);
        this.result.set({ bookTitleId: res.id });
        this.form.reset();
      },
      error: err => {
        this.loading.set(false);
        const msg = typeof err.error === 'string' ? err.error : 'Failed to add book.';
        this.error.set(msg);
      },
    });
  }

  copyId(value: string): void {
    navigator.clipboard.writeText(value).then(() => {
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 2000);
    });
  }
}
