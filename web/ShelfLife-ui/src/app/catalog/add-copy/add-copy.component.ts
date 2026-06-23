import { Component, inject, signal } from '@angular/core';
import { FormBuilder, Validators, ReactiveFormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { CatalogService } from '../catalog.service';
import { AuthService } from '../../shared/services/auth.service';

@Component({
  selector: 'app-add-copy',
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
  templateUrl: './add-copy.component.html',
})
export class AddCopyComponent {
  private readonly fb = inject(FormBuilder);
  private readonly catalog = inject(CatalogService);
  readonly auth = inject(AuthService);

  readonly form = this.fb.nonNullable.group({
    bookTitleId: ['', [Validators.required]],
    barcode: ['', [Validators.required, Validators.minLength(1)]],
  });

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly result = signal<{ copyId: string } | null>(null);

  onSubmit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.loading.set(true);
    this.error.set(null);
    this.result.set(null);

    const { bookTitleId, barcode } = this.form.getRawValue();
    this.catalog.addCopy(bookTitleId.trim(), barcode.trim()).subscribe({
      next: res => {
        this.loading.set(false);
        this.result.set({ copyId: res.id });
        this.form.reset();
      },
      error: err => {
        this.loading.set(false);
        const msg = typeof err.error === 'string' ? err.error : 'Failed to add copy.';
        this.error.set(msg);
      },
    });
  }
}
