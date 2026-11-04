import { Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { EventService } from '../../core/event.service';
import { AuthService } from '../../core/auth.service';
import { EventListItem, EventQuery, FilterState, PagedResult } from '../../core/models';
import { errorText } from '../../core/api-error';
import { EventFilter, DEFAULT_FILTER } from '../../components/event-filter/event-filter';
import { EventList } from '../../components/event-list/event-list';

@Component({
  selector: 'app-event-list-page',
  imports: [RouterLink, EventFilter, EventList],
  templateUrl: './event-list-page.html',
  styleUrl: './event-list-page.css',
})
export class EventListPage {
  private readonly eventService = inject(EventService);
  private readonly router = inject(Router);
  readonly auth = inject(AuthService);

  readonly query = signal<EventQuery>({
    page: 1,
    pageSize: 6,
    venueId: null,
    free: false,
    date: null,
    sort: 'date',
    order: 'asc',
  });

  readonly pageResource = this.eventService.listResource(this.query);
  readonly venuesResource = this.eventService.venuesResource();

  readonly data = computed<PagedResult<EventListItem> | undefined>(() => this.pageResource.value());
  readonly events = computed(() => this.data()?.items ?? []);
  readonly total = computed(() => this.data()?.total ?? 0);
  readonly currentPage = computed(() => this.data()?.page ?? this.query().page);
  readonly totalPages = computed(() => Math.max(this.data()?.totalPages ?? 1, 1));

  readonly filterState = computed<FilterState>(() => {
    const value = this.query();
    return {
      venueId: value.venueId,
      free: value.free,
      date: value.date,
      sort: value.sort,
      order: value.order,
    };
  });

  readonly hasFilters = computed(() => {
    const value = this.query();
    return value.venueId !== null || value.free || value.date !== null;
  });

  readonly actionError = signal<string | null>(null);
  readonly deletingId = signal<number | null>(null);

  readonly errorText = errorText;

  applyFilter(state: FilterState): void {
    this.query.update((value) => ({ ...value, ...state, page: 1 }));
  }

  resetFilter(): void {
    this.query.update((value) => ({ ...value, ...DEFAULT_FILTER, page: 1 }));
  }

  goToPage(page: number): void {
    const target = Math.min(Math.max(page, 1), this.totalPages());
    this.query.update((value) => ({ ...value, page: target }));
  }

  changePageSize(event: Event): void {
    const pageSize = Number((event.target as HTMLSelectElement).value);
    this.query.update((value) => ({ ...value, pageSize, page: 1 }));
  }

  onEdit(item: EventListItem): void {
    void this.router.navigate(['/events', item.id, 'edit']);
  }

  onRemove(item: EventListItem): void {
    if (!window.confirm(`Удалить мероприятие «${item.title}»?`)) {
      return;
    }

    this.actionError.set(null);
    this.deletingId.set(item.id);

    this.eventService.delete(item.id).subscribe({
      next: () => {
        this.deletingId.set(null);
        if (this.events().length === 1 && this.currentPage() > 1) {
          this.query.update((value) => ({ ...value, page: value.page - 1 }));
        } else {
          this.pageResource.reload();
        }
      },
      error: (error: unknown) => {
        this.deletingId.set(null);
        this.actionError.set(errorText(error));
      },
    });
  }

  retry(): void {
    this.venuesResource.reload();
    this.pageResource.reload();
  }
}
