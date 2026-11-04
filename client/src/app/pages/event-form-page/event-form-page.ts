import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { EventService } from '../../core/event.service';
import { AuthService } from '../../core/auth.service';
import { errorText, problemFrom } from '../../core/api-error';
import { fromDatetimeLocal, toDatetimeLocal } from '../../core/format';
import { EventPayload } from '../../core/models';

const futureDateValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value = control.value as string;
  if (!value) {
    return null;
  }
  return new Date(value).getTime() > Date.now() ? null : { future: true };
};

const integerValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value = control.value;
  if (value === null || value === undefined || value === '') {
    return null;
  }
  return Number.isInteger(Number(value)) ? null : { integer: true };
};

@Component({
  selector: 'app-event-form-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './event-form-page.html',
  styleUrl: './event-form-page.css',
})
export class EventFormPage {
  private readonly eventService = inject(EventService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  readonly id = input<string>();

  readonly eventId = computed(() => {
    const raw = this.id();
    if (!raw) {
      return undefined;
    }
    const value = Number(raw);
    return Number.isInteger(value) && value > 0 ? value : undefined;
  });

  readonly isEdit = computed(() => this.eventId() !== undefined);
  readonly details = this.eventService.detailResource(this.eventId);
  readonly venues = this.eventService.venuesResource();

  readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(200)]],
    description: ['', [Validators.maxLength(2000)]],
    startsAt: ['', [Validators.required, futureDateValidator]],
    price: [0, [Validators.required, Validators.min(0), Validators.max(1_000_000)]],
    seats: [1, [Validators.required, Validators.min(1), Validators.max(50_000), integerValidator]],
    venueId: [0, [Validators.required, Validators.min(1)]],
  });

  readonly minDateTime = toDatetimeLocal(new Date().toISOString());
  readonly submitted = signal(false);
  readonly saving = signal(false);
  readonly generalError = signal<string | null>(null);
  readonly errorText = errorText;

  private readonly patchedId = signal<number | null>(null);

  readonly loadingEvent = computed(
    () => this.isEdit() && this.details.isLoading() && !this.details.value(),
  );

  readonly forbidden = computed(() => {
    const data = this.details.value();
    const userId = this.auth.currentUserId();
    return data !== undefined && userId !== null && data.authorId !== userId;
  });

  constructor() {
    effect(() => {
      const data = this.details.value();
      if (!data || this.patchedId() === data.id) {
        return;
      }

      this.patchedId.set(data.id);
      this.form.patchValue({
        title: data.title,
        description: data.description ?? '',
        startsAt: toDatetimeLocal(data.startsAt),
        price: data.price,
        seats: data.seats,
        venueId: data.venue.id,
      });
    });

    this.form.valueChanges.subscribe(() => this.clearServerErrors());
  }

  submit(): void {
    this.submitted.set(true);
    this.generalError.set(null);
    this.form.markAllAsTouched();

    if (this.form.invalid) {
      return;
    }

    const id = this.eventId();
    if (this.isEdit() && id === undefined) {
      this.generalError.set('Некорректная ссылка на мероприятие.');
      return;
    }

    const raw = this.form.getRawValue();
    const payload: EventPayload = {
      title: raw.title.trim(),
      description: raw.description.trim() || null,
      startsAt: fromDatetimeLocal(raw.startsAt),
      price: Number(raw.price),
      seats: Number(raw.seats),
      venueId: Number(raw.venueId),
    };

    this.saving.set(true);
    const request =
      this.isEdit() && id !== undefined
        ? this.eventService.update(id, payload)
        : this.eventService.create(payload);

    request.subscribe({
      next: (saved) => {
        this.saving.set(false);
        void this.router.navigate(['/events', saved.id]);
      },
      error: (error: unknown) => {
        this.saving.set(false);
        const problem = problemFrom(error);
        if (problem?.errors) {
          this.setServerErrors(problem.errors);
          this.generalError.set('Проверьте поля — сервер нашёл ошибки.');
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
    if (errors['min']) {
      if (name === 'venueId') {
        return 'Выберите площадку.';
      }
      return `Значение не меньше ${errors['min'].min}.`;
    }
    if (errors['max']) {
      return `Значение не больше ${errors['max'].max}.`;
    }
    if (errors['future']) {
      return 'Дата начала должна быть в будущем.';
    }
    if (errors['integer']) {
      return 'Введите целое число.';
    }

    return 'Некорректное значение.';
  }

  hasError(name: string): boolean {
    return this.fieldError(name) !== null;
  }

  private setServerErrors(errors: Record<string, string[]>): void {
    for (const [field, messages] of Object.entries(errors)) {
      const control = this.form.get(field);
      if (control) {
        control.setErrors({ ...(control.errors ?? {}), server: messages.join(' ') });
        control.markAsTouched();
      }
    }
  }

  private clearServerErrors(): void {
    for (const control of Object.values(this.form.controls)) {
      const errors = control.errors;
      if (errors && 'server' in errors) {
        const rest: ValidationErrors = { ...errors };
        delete rest['server'];
        control.setErrors(Object.keys(rest).length > 0 ? rest : null);
      }
    }
  }
}
