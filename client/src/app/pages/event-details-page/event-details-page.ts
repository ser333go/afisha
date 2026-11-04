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
  readonly isAuthor = computed(() => {
    const userId = this.auth.currentUserId();
    return userId !== null && this.event()?.authorId === userId;
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
  }
}
