import { Component, computed, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { EventService } from '../../core/event.service';
import { AuthService } from '../../core/auth.service';
import { errorText, problemFrom } from '../../core/api-error';
import { formatDateTime, formatPrice, minutesUntil } from '../../core/format';

const MAX_POSTER_SIZE = 5 * 1024 * 1024;
const ALLOWED_TYPES = ['image/jpeg', 'image/png', 'image/webp'];

@Component({
  selector: 'app-event-details-page',
  imports: [RouterLink],
  templateUrl: './event-details-page.html',
  styleUrl: './event-details-page.css',
})
export class EventDetailsPage {
  private readonly eventService = inject(EventService);
  private readonly router = inject(Router);
  readonly auth = inject(AuthService);

  readonly id = input.required<string>();

  readonly eventId = computed(() => {
    const value = Number(this.id());
    return Number.isInteger(value) && value > 0 ? value : undefined;
  });

  readonly details = this.eventService.detailResource(this.eventId);

  readonly event = computed(() => this.details.value());
  readonly canManage = computed(() => this.auth.isAdmin());

  readonly myReservations = this.eventService.myReservationsResource(this.auth.isAuthenticated);
  readonly myReservation = computed(
    () => this.myReservations.value()?.find((r) => r.eventId === this.eventId()) ?? null,
  );
  readonly bookingSeats = signal(1);
  readonly bookingBusy = signal(false);
  readonly bookingError = signal<string | null>(null);
  readonly isPast = computed(() => {
    const value = this.event();
    return value ? new Date(value.startsAt).getTime() <= Date.now() : false;
  });

  readonly when = computed(() => {
    const value = this.event();
    return value ? formatDateTime(value.startsAt) : '';
  });
  readonly price = computed(() => {
    const value = this.event();
    return value ? formatPrice(value.price) : '';
  });
  readonly lockHint = computed(() => {
    const value = this.event();
    if (!value) {
      return '';
    }
    const minutes = minutesUntil(value.startsAt);
    return minutes < 24 * 60
      ? 'До начала меньше 24 часов — редактирование и удаление заблокированы.'
      : '';
  });

  readonly uploading = signal(false);
  readonly deleting = signal(false);
  readonly uploadError = signal<string | null>(null);
  readonly actionError = signal<string | null>(null);
  readonly errorText = errorText;

  onSeatsInput(event: Event): void {
    const value = Number((event.target as HTMLInputElement).value);
    this.bookingSeats.set(Number.isFinite(value) ? value : 1);
  }

  reserve(event?: Event): void {
    event?.preventDefault();

    const item = this.event();
    if (!item) {
      return;
    }

    if (!this.auth.isAuthenticated()) {
      void this.router.navigate(['/login'], { queryParams: { returnUrl: `/events/${item.id}` } });
      return;
    }

    const seats = Math.trunc(Number(this.bookingSeats()));
    if (!Number.isInteger(seats) || seats < 1) {
      this.bookingError.set('Укажите количество мест от 1.');
      return;
    }

    if (seats > item.availableSeats) {
      this.bookingError.set(`Свободных мест осталось: ${item.availableSeats}.`);
      return;
    }

    this.bookingError.set(null);
    this.bookingBusy.set(true);

    this.eventService.reserve(item.id, seats).subscribe({
      next: () => {
        this.bookingBusy.set(false);
        this.bookingSeats.set(1);
        this.details.reload();
        this.myReservations.reload();
      },
      error: (error: unknown) => {
        this.bookingBusy.set(false);
        const problem = problemFrom(error);
        this.bookingError.set(
          problem?.errors?.['seats']?.join(' ') ?? problem?.detail ?? errorText(error),
        );
      },
    });
  }

  cancelBooking(): void {
    const booking = this.myReservation();
    if (!booking || !window.confirm(`Отменить бронь на ${booking.seats} мест?`)) {
      return;
    }

    this.bookingBusy.set(true);
    this.bookingError.set(null);

    this.eventService.cancelReservation(booking.id).subscribe({
      next: () => {
        this.bookingBusy.set(false);
        this.details.reload();
        this.myReservations.reload();
      },
      error: (error: unknown) => {
        this.bookingBusy.set(false);
        this.bookingError.set(errorText(error));
      },
    });
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';

    if (!file) {
      return;
    }

    this.uploadError.set(null);

    if (!ALLOWED_TYPES.includes(file.type)) {
      this.uploadError.set('Допустимые форматы: jpg, png, webp.');
      return;
    }

    if (file.size > MAX_POSTER_SIZE) {
      this.uploadError.set('Файл не должен превышать 5 МБ.');
      return;
    }

    const id = this.eventId();
    if (!id) {
      return;
    }

    this.uploading.set(true);
    this.eventService.uploadPoster(id, file).subscribe({
      next: () => {
        this.uploading.set(false);
        this.details.reload();
      },
      error: (error: unknown) => {
        this.uploading.set(false);
        const problem = problemFrom(error);
        const message = problem?.errors?.['file']?.join(' ');
        this.uploadError.set(message ?? errorText(error));
      },
    });
  }

  remove(): void {
    const item = this.event();
    if (!item || !window.confirm(`Удалить мероприятие «${item.title}»?`)) {
      return;
    }

    this.deleting.set(true);
    this.actionError.set(null);

    this.eventService.delete(item.id).subscribe({
      next: () => {
        this.deleting.set(false);
        void this.router.navigate(['/events']);
      },
      error: (error: unknown) => {
        this.deleting.set(false);
        this.actionError.set(errorText(error));
      },
    });
  }

  retry(): void {
    this.details.reload();
    this.myReservations.reload();
  }
}
