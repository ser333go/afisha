import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { EventService } from '../../core/event.service';
import { Reservation } from '../../core/models';
import { errorText } from '../../core/api-error';
import { formatDateTime } from '../../core/format';

@Component({
  selector: 'app-my-bookings-page',
  imports: [RouterLink],
  templateUrl: './my-bookings-page.html',
  styleUrl: './my-bookings-page.css',
})
export class MyBookingsPage {
  private readonly eventService = inject(EventService);
  readonly auth = inject(AuthService);

  readonly reservations = this.eventService.myReservationsResource(this.auth.isAuthenticated);
  readonly items = computed(() => this.reservations.value() ?? []);

  readonly busyId = signal<number | null>(null);
  readonly actionError = signal<string | null>(null);
  readonly errorText = errorText;

  when(iso: string): string {
    return formatDateTime(iso);
  }

  cancel(item: Reservation): void {
    if (!window.confirm(`Отменить бронь на «${item.eventTitle}» (${item.seats} мест)?`)) {
      return;
    }

    this.busyId.set(item.id);
    this.actionError.set(null);

    this.eventService.cancelReservation(item.id).subscribe({
      next: () => {
        this.busyId.set(null);
        this.reservations.reload();
      },
      error: (error: unknown) => {
        this.busyId.set(null);
        this.actionError.set(errorText(error));
      },
    });
  }

  retry(): void {
    this.reservations.reload();
  }
}
