import { Component, computed, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { EventListItem } from '../../core/models';
import { formatDateTime, formatPrice } from '../../core/format';

@Component({
  selector: 'app-event-card',
  imports: [RouterLink],
  templateUrl: './event-card.html',
  styleUrl: './event-card.css',
})
export class EventCard {
  readonly event = input.required<EventListItem>();
  readonly canManage = input(false);

  readonly edit = output<EventListItem>();
  readonly remove = output<EventListItem>();

  readonly isAuthor = computed(() => this.canManage());
  readonly when = computed(() => formatDateTime(this.event().startsAt));
  readonly priceLabel = computed(() => formatPrice(this.event().price));
  readonly initial = computed(() => this.event().title.trim().charAt(0).toUpperCase());
  readonly coverGradient = computed(() => {
    const hue = (this.event().id * 43) % 360;
    return `linear-gradient(135deg, hsl(${hue} 62% 46%), hsl(${(hue + 48) % 360} 68% 32%))`;
  });
}
