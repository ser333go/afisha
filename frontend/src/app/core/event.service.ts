import { inject, Injectable, Signal } from '@angular/core';
import { HttpClient, httpResource } from '@angular/common/http';
import {
  EventDetails,
  EventListItem,
  EventPayload,
  EventQuery,
  PagedResult,
  PosterUploaded,
  Venue,
} from './models';

@Injectable({ providedIn: 'root' })
export class EventService {
  private readonly http = inject(HttpClient);

  /** Список с фильтрами и пагинацией: httpResource перезапрашивается при изменении сигнала query. */
  listResource(query: Signal<EventQuery>) {
    return httpResource<PagedResult<EventListItem>>(() => {
      const value = query();
      const params: Record<string, string | number | boolean> = {
        page: value.page,
        pageSize: value.pageSize,
        sort: value.sort,
        order: value.order,
      };

      if (value.venueId !== null) {
        params['venueId'] = value.venueId;
      }
      if (value.free) {
        params['free'] = true;
      }
      if (value.date) {
        params['date'] = value.date;
      }

      return { url: '/api/events', params };
    });
  }

  /** Одна запись: пока id не известен, запрос не выполняется. */
  detailResource(id: Signal<number | undefined>) {
    return httpResource<EventDetails>(() => {
      const value = id();
      return value ? { url: `/api/events/${value}` } : undefined;
    });
  }

  venuesResource() {
    return httpResource<Venue[]>(() => '/api/venues');
  }

  create(payload: EventPayload) {
    return this.http.post<EventDetails>('/api/events', payload);
  }

  update(id: number, payload: EventPayload) {
    return this.http.put<EventDetails>(`/api/events/${id}`, payload);
  }

  delete(id: number) {
    return this.http.delete<void>(`/api/events/${id}`);
  }

  uploadPoster(id: number, file: File) {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<PosterUploaded>(`/api/events/${id}/poster`, formData);
  }
}
