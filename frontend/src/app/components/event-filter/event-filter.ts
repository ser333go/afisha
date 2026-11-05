import { Component, computed, effect, input, output, signal } from '@angular/core';
import { FilterState, SortField, SortOrder, Venue } from '../../core/models';

export const DEFAULT_FILTER: FilterState = {
  venueId: null,
  free: false,
  date: null,
  sort: 'date',
  order: 'asc',
};

@Component({
  selector: 'app-event-filter',
  templateUrl: './event-filter.html',
  styleUrl: './event-filter.css',
})
export class EventFilter {
  readonly venues = input<Venue[]>([]);
  readonly value = input<FilterState>(DEFAULT_FILTER);

  readonly changed = output<FilterState>();

  readonly venueId = signal<number | null>(null);
  readonly free = signal(false);
  readonly date = signal<string | null>(null);
  readonly sort = signal<SortField>('date');
  readonly order = signal<SortOrder>('asc');

  readonly sortKey = computed(() => `${this.sort()}:${this.order()}`);

  constructor() {
    effect(() => {
      const value = this.value();
      this.venueId.set(value.venueId);
      this.free.set(value.free);
      this.date.set(value.date);
      this.sort.set(value.sort);
      this.order.set(value.order);
    });
  }

  onVenue(event: Event): void {
    const raw = (event.target as HTMLSelectElement).value;
    this.venueId.set(raw ? Number(raw) : null);
    this.emit();
  }

  onDate(event: Event): void {
    const raw = (event.target as HTMLInputElement).value;
    this.date.set(raw || null);
    this.emit();
  }

  onFree(event: Event): void {
    this.free.set((event.target as HTMLInputElement).checked);
    this.emit();
  }

  onSort(event: Event): void {
    const [sort, order] = (event.target as HTMLSelectElement).value.split(':');
    this.sort.set(sort as SortField);
    this.order.set(order as SortOrder);
    this.emit();
  }

  reset(): void {
    this.venueId.set(DEFAULT_FILTER.venueId);
    this.free.set(DEFAULT_FILTER.free);
    this.date.set(DEFAULT_FILTER.date);
    this.sort.set(DEFAULT_FILTER.sort);
    this.order.set(DEFAULT_FILTER.order);
    this.emit();
  }

  private emit(): void {
    this.changed.emit({
      venueId: this.venueId(),
      free: this.free(),
      date: this.date(),
      sort: this.sort(),
      order: this.order(),
    });
  }
}
