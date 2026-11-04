import { Component, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { errorText, problemFrom } from '../../core/api-error';

@Component({
  selector: 'app-login-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './login-page.html',
  styleUrl: './login-page.css',
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  readonly returnUrl = input<string>('/events');

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
  });

  readonly submitting = signal(false);
  readonly submitted = signal(false);
  readonly error = signal<string | null>(null);

  submit(): void {
    this.submitted.set(true);
    this.error.set(null);
    this.form.markAllAsTouched();

    if (this.form.invalid) {
      return;
    }

    this.submitting.set(true);
    this.auth.login(this.form.getRawValue()).subscribe({
      next: () => {
        this.submitting.set(false);
        void this.router.navigateByUrl(this.returnUrl());
      },
      error: (error: unknown) => {
        this.submitting.set(false);
        const problem = problemFrom(error);
        this.error.set(problem?.detail ?? problem?.title ?? errorText(error));
      },
    });
  }

  fieldError(name: string): string | null {
    const control = this.form.get(name);
    if (!control || (!control.touched && !this.submitted())) {
      return null;
    }
    if (control.errors?.['required']) {
      return 'Поле обязательно.';
    }
    if (control.errors?.['email']) {
      return 'Введите корректный email.';
    }
    return null;
  }

  fillDemo(): void {
    this.form.setValue({ email: 'demo@afisha.local', password: 'Demo12345' });
  }
}
