import { Component, input, output } from '@angular/core';
import { EventListItem } from '../../core/models';
import { EventCard } from '../event-card/event-card';

@Component({
  selector: 'app-event-list',
  imports: [EventCard],
  templateUrl: './event-list.html',
  styleUrl: './event-list.css',
})
export class EventList {
  readonly events = input.required<EventListItem[]>();
  readonly currentUserId = input<number | null>(null);

  readonly edit = output<EventListItem>();
  readonly remove = output<EventListItem>();
}
