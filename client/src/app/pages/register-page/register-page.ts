import { Component, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { errorText, problemFrom } from '../../core/api-error';

const passwordContentValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value = control.value as string;
  if (!value) {
    return null;
  }
  const hasLetter = /[A-Za-zА-Яа-яЁё]/.test(value);
  const hasDigit = /\d/.test(value);
  return hasLetter && hasDigit ? null : { passwordContent: true };
};

@Component({
  selector: 'app-register-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './register-page.html',
  styleUrl: './register-page.css',
})
export class RegisterPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.nonNullable.group({
    displayName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(254)]],
    password: [
      '',
      [Validators.required, Validators.minLength(8), Validators.maxLength(72), passwordContentValidator],
    ],
  });

  readonly submitting = signal(false);
  readonly submitted = signal(false);
  readonly generalError = signal<string | null>(null);

  submit(): void {
    this.submitted.set(true);
    this.generalError.set(null);
    this.form.markAllAsTouched();

    if (this.form.invalid) {
      return;
    }

    this.submitting.set(true);
    const raw = this.form.getRawValue();
    this.auth
      .register({
        displayName: raw.displayName.trim(),
        email: raw.email.trim().toLowerCase(),
        password: raw.password,
      })
      .subscribe({
        next: () => {
          this.submitting.set(false);
          void this.router.navigate(['/events']);
        },
        error: (error: unknown) => {
          this.submitting.set(false);
          const problem = problemFrom(error);
          if (problem?.errors) {
            for (const [field, messages] of Object.entries(problem.errors)) {
              const control = this.form.get(field);
              if (control) {
                control.setErrors({ server: messages.join(' ') });
                control.markAsTouched();
              }
            }
            return;
          }
          this.generalError.set(problem?.detail ?? problem?.title ?? errorText(error));
        },
      });
  }

  fieldError(name: string): string | null {
    const control = this.form.get(name);
    if (!control || (!control.touched && !this.submitted())) {
      return null;
    }

    const errors = control.errors;
    if (!errors) {
      return null;
    }
    if (errors['server']) {
      return String(errors['server']);
    }
    if (errors['required']) {
      return 'Поле обязательно.';
    }
    if (errors['email']) {
      return 'Введите корректный email.';
    }
    if (errors['minlength']) {
      return `Минимум ${errors['minlength'].requiredLength} символов.`;
    }
    if (errors['maxlength']) {
      return `Максимум ${errors['maxlength'].requiredLength} символов.`;
    }
    if (errors['passwordContent']) {
      return 'Пароль должен содержать хотя бы одну букву и одну цифру.';
    }
    return null;
  }
}
