# 05. Клиент: карточка, список, фильтр и страница списка

Здесь четыре компонента и CSS (CSS разобран блоками — объяснять `padding` построчно
бессмысленно, важно понимать, за что отвечает каждый селектор).

---

## `components/event-card` — карточка мероприятия

### `event-card.ts`

```ts
import { Component, computed, input, output } from '@angular/core';   // 1
import { RouterLink } from '@angular/router';                        // 2
import { EventListItem } from '../../core/models';                   // 3
import { formatDateTime, formatPrice } from '../../core/format';     // 4

@Component({                                                         // 6
  selector: 'app-event-card',                                        // 7 — тег <app-event-card>
  imports: [RouterLink],                                             // 8 — нужен routerLink в шаблоне
  templateUrl: './event-card.html',
  styleUrl: './event-card.css',
})
export class EventCard {
  readonly event = input.required<EventListItem>();                  // 12 — обязательный вход
  readonly currentUserId = input<number | null>(null);               // 13 — необязательный вход

  readonly edit = output<EventListItem>();                           // 15 — событие «редактировать»
  readonly remove = output<EventListItem>();                         // 16 — событие «удалить»

  readonly isAuthor = computed(                                      // 18 — производное значение
    () => this.currentUserId() !== null &&                           // 19 — пользователь вошёл…
          this.currentUserId() === this.event().authorId,            // 20 — …и он автор
  );
  readonly when = computed(() => formatDateTime(this.event().startsAt)); // 21 — дата текстом
  readonly priceLabel = computed(() => formatPrice(this.event().price)); // 22 — цена текстом
  readonly initial = computed(() =>                                   // 23 — первая буква для заглушки
    this.event().title.trim().charAt(0).toUpperCase());
  readonly coverGradient = computed(() => {                          // 24 — фон-заглушка
    const hue = (this.event().id * 43) % 360;                        // 25 — свой оттенок у каждой карточки
    return `linear-gradient(135deg, hsl(${hue} 62% 46%), hsl(${(hue + 48) % 360} 68% 32%))`; // 26
  });
}
```

`input.required` — обязательный вход: если родитель не передал `event`, Angular
выбросит ошибку. `output` — событие наружу; родитель подписывается `(edit)="..."`.

### `event-card.html`

```html
<article class="card">                                        <!-- 1 -->
  <a class="card__cover" [routerLink]="['/events', event().id]" <!-- 2 — ссылка на страницу -->
     [attr.aria-label]="event().title">                        <!-- 3 -->
    @if (event().posterUrl) {                                  <!-- 4 — постер есть? -->
      <img class="card__image" [src]="event().posterUrl"       <!-- 5 -->
           [alt]="event().title" loading="lazy" />             <!-- 6 — ленивая загрузка -->
    } @else {                                                  <!-- 7 -->
      <span class="card__placeholder" [style.background]="coverGradient()"> <!-- 8 -->
        <span>{{ initial() }}</span>                           <!-- 9 -->
      </span>
    }
    <span class="card__price" [class.card__price--free]="event().price === 0"> <!-- 11 -->
      {{ priceLabel() }}                                       <!-- 12 -->
    </span>
  </a>

  <div class="card__body">                                     <!-- 15 -->
    <p class="card__venue">{{ event().venue.name }}</p>        <!-- 16 -->
    <h3 class="card__title">                                   <!-- 17 -->
      <a [routerLink]="['/events', event().id]">{{ event().title }}</a> <!-- 18 -->
    </h3>
    <p class="card__date">{{ when() }}</p>                     <!-- 20 -->

    <div class="card__meta">                                   <!-- 22 -->
      <span>{{ event().seats }} мест</span>                    <!-- 23 -->
      <span class="card__dot" aria-hidden="true"></span>       <!-- 24 -->
      <span>{{ event().venue.city }}</span>                    <!-- 25 -->
    </div>

    @if (isAuthor()) {                                         <!-- 27 — кнопки только автору -->
      <div class="card__actions">                              <!-- 28 -->
        <a class="btn btn--small btn--ghost"                   <!-- 29 -->
           [routerLink]="['/events', event().id, 'edit']">Редактировать</a>
        <button type="button" class="btn btn--small btn--danger" <!-- 31 -->
                (click)="remove.emit(event())">Удалить</button>   <!-- 32 — сообщаем родителю -->
      </div>
    }
  </div>
</article>
```

`[class.card__price--free]` — условный класс. `remove.emit(event())` — отправляем
событие наверх; сама карточка ничего не удаляет (ей нельзя доверять права).

### `event-card.css` — блоки

| Селектор | Зачем |
| --- | --- |
| `:host`, `.card` | карточка занимает высоту строки сетки, скругления, тень, подъём при наведении |
| `.card__cover` | область постера 16:10, обрезает картинку по границам |
| `.card__image` | `object-fit: contain` + тёмный фон: постер 4:5 виден целиком, без обрезки |
| `.card__placeholder` | цветной градиент и большая первая буква, если постера нет |
| `.card__price` | бейдж цены поверх постера, справа снизу |
| `.card__price--free` | «Бесплатно» перекрашивается в акцентный цвет |
| `.card__body`, `.card__title` | типографика, отступы |
| `.card__meta` | `margin-top: auto` прижимает строку «мест/город» к низу карточки |
| `.card__actions` | кнопки автора, разделитель сверху |

---

## `components/event-list` — сетка карточек

```ts
import { Component, input, output } from '@angular/core';
import { EventListItem } from '../../core/models';
import { EventCard } from '../event-card/event-card';

@Component({
  selector: 'app-event-list',
  imports: [EventCard],                                  // 8 — используем карточку
  templateUrl: './event-list.html',
  styleUrl: './event-list.css',
})
export class EventList {
  readonly events = input.required<EventListItem[]>();   // 13 — массив
  readonly currentUserId = input<number | null>(null);   // 14 — для проверки авторства
  readonly edit = output<EventListItem>();               // 16 — прокидываем наверх
  readonly remove = output<EventListItem>();             // 17
}
```

```html
<div class="event-grid">                                 <!-- 1 — CSS Grid -->
  @for (item of events(); track item.id) {               <!-- 2 — track по id для скорости -->
    <app-event-card
      [event]="item"                                     <!-- 4 -->
      [currentUserId]="currentUserId()"                  <!-- 5 -->
      (edit)="edit.emit($event)"                         <!-- 6 — проброс наружу -->
      (remove)="remove.emit($event)"                     <!-- 7 -->
    />
  }
</div>
```

```css
.event-grid {                                            /* сетка */
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(270px, 1fr)); /* 270px минимум, растягивается */
  gap: 1.4rem;
}
```

---

## `components/event-filter` — фильтры и сортировка

### `event-filter.ts`

```ts
import { Component, computed, effect, input, output, signal } from '@angular/core'; // 1
import { FilterState, SortField, SortOrder, Venue } from '../../core/models';      // 2

export const DEFAULT_FILTER: FilterState = {              // 4 — значения по умолчанию
  venueId: null,                                          // 5 — все площадки
  free: false,                                            // 6 — и платные, и бесплатные
  date: null,                                             // 7 — любая дата
  sort: 'date',                                           // 8 — по дате
  order: 'asc',                                           // 9 — сначала ближайшие
};

@Component({
  selector: 'app-event-filter',
  templateUrl: './event-filter.html',
  styleUrl: './event-filter.css',
})
export class EventFilter {
  readonly venues = input<Venue[]>([]);                   // 18 — справочник от родителя
  readonly value = input<FilterState>(DEFAULT_FILTER);    // 19 — текущее состояние

  readonly changed = output<FilterState>();               // 21 — сообщаем наверх

  readonly venueId = signal<number | null>(null);         // 23 — локальные сигналы полей
  readonly free = signal(false);                          // 24
  readonly date = signal<string | null>(null);            // 25
  readonly sort = signal<SortField>('date');              // 26
  readonly order = signal<SortOrder>('asc');              // 27

  readonly sortKey = computed(() => `${this.sort()}:${this.order()}`); // 29 — для <select>

  constructor() {
    effect(() => {                                        // 32 — синхронизация с родителем
      const value = this.value();                         // 33 — читаем вход → эффект зависит от него
      this.venueId.set(value.venueId);                    // 34 — копируем в локальные сигналы
      this.free.set(value.free);
      this.date.set(value.date);
      this.sort.set(value.sort);
      this.order.set(value.order);
    });
  }

  onVenue(event: Event): void {                           // 42 — выбрали площадку
    const raw = (event.target as HTMLSelectElement).value; // 43
    this.venueId.set(raw ? Number(raw) : null);           // 44 — пустая строка → null
    this.emit();                                          // 45
  }

  onDate(event: Event): void {                            // 48
    const raw = (event.target as HTMLInputElement).value; // 49 — "2027-02-14"
    this.date.set(raw || null);                           // 50
    this.emit();                                          // 51
  }

  onFree(event: Event): void {                            // 54
    this.free.set((event.target as HTMLInputElement).checked); // 55
    this.emit();                                          // 56
  }

  onSort(event: Event): void {                            // 59
    const [sort, order] = (event.target as HTMLSelectElement).value.split(':'); // 60 — "price:desc"
    this.sort.set(sort as SortField);                     // 61
    this.order.set(order as SortOrder);                   // 62
    this.emit();                                          // 63
  }

  reset(): void {                                         // 66 — кнопка «Сбросить»
    this.venueId.set(DEFAULT_FILTER.venueId);             // 67
    this.free.set(DEFAULT_FILTER.free);
    this.date.set(DEFAULT_FILTER.date);
    this.sort.set(DEFAULT_FILTER.sort);
    this.order.set(DEFAULT_FILTER.order);
    this.emit();                                          // 72
  }

  private emit(): void {                                  // 75 — собрать и отдать состояние
    this.changed.emit({
      venueId: this.venueId(),                            // 77
      free: this.free(),                                  // 78
      date: this.date(),                                  // 79
      sort: this.sort(),                                  // 80
      order: this.order(),                                // 81
    });
  }
}
```

Почему локальные сигналы, а не `[(ngModel)]`: так компонент остаётся
standalone-сигнальным, а родитель получает одно событие `changed` со всеми полями.

### `event-filter.html`

```html
<section class="filter" aria-label="Фильтры афиши">        <!-- 1 -->
  <div class="filter__field">                             <!-- 2 — площадка -->
    <label for="filter-venue">Площадка</label>
    <select id="filter-venue" [value]="venueId() ?? ''" (change)="onVenue($event)"> <!-- 4 -->
      <option value="">Все площадки</option>              <!-- 5 -->
      @for (venue of venues(); track venue.id) {          <!-- 6 -->
        <option [value]="venue.id">{{ venue.name }}</option> <!-- 7 -->
      }
    </select>
  </div>

  <div class="filter__field">                             <!-- 11 — дата -->
    <label for="filter-date">Дата</label>
    <input id="filter-date" type="date" [value]="date() ?? ''" (change)="onDate($event)" /> <!-- 13 -->
  </div>

  <label class="filter__check" for="filter-free">         <!-- 16 — бесплатные -->
    <input id="filter-free" type="checkbox" [checked]="free()" (change)="onFree($event)" /> <!-- 17 -->
    <span>Только бесплатные</span>
  </label>

  <div class="filter__field">                             <!-- 21 — сортировка -->
    <label for="filter-sort">Сортировка</label>
    <select id="filter-sort" [value]="sortKey()" (change)="onSort($event)"> <!-- 23 -->
      <option value="date:asc">Дата: сначала ближайшие</option>   <!-- 24 -->
      <option value="date:desc">Дата: сначала поздние</option>    <!-- 25 -->
      <option value="price:asc">Цена: сначала дешёвые</option>    <!-- 26 -->
      <option value="price:desc">Цена: сначала дорогие</option>   <!-- 27 -->
    </select>
  </div>

  <button type="button" class="btn btn--ghost filter__reset" (click)="reset()">Сбросить</button> <!-- 31 -->
</section>
```

### `event-filter.css` — блоки

| Селектор | Зачем |
| --- | --- |
| `.filter` | белая панель, `flex-wrap`, выравнивание по низу |
| `.filter__field` | колонка «подпись + поле», минимум 170px |
| `.filter__field label` | маленькая капс-подпись |
| `select/input:focus` | акцентная рамка при фокусе |
| `.filter__check` | чекбокс в рамке, кликабельная область |
| `.filter__reset` | `margin-left: auto` прижимает кнопку вправо |

---

## `pages/event-list-page` — главный экран

### `event-list-page.ts`

```ts
import { Component, computed, inject, signal } from '@angular/core';   // 1
import { Router, RouterLink } from '@angular/router';                 // 2
import { EventService } from '../../core/event.service';               // 3
import { AuthService } from '../../core/auth.service';                 // 4
import { EventListItem, EventQuery, FilterState, PagedResult } from '../../core/models'; // 5
import { errorText } from '../../core/api-error';                      // 6
import { EventFilter, DEFAULT_FILTER } from '../../components/event-filter/event-filter'; // 7
import { EventList } from '../../components/event-list/event-list';   // 8

@Component({
  selector: 'app-event-list-page',
  imports: [RouterLink, EventFilter, EventList],                      // 12
  templateUrl: './event-list-page.html',
  styleUrl: './event-list-page.css',
})
export class EventListPage {
  private readonly eventService = inject(EventService);               // 17 — API
  private readonly router = inject(Router);                           // 18
  readonly auth = inject(AuthService);                                // 19 — публичный, нужен шаблону

  readonly query = signal<EventQuery>({                               // 21 — состояние запроса
    page: 1,                                                          // 22
    pageSize: 6,                                                      // 23
    venueId: null,                                                    // 24
    free: false,                                                      // 25
    date: null,                                                       // 26
    sort: 'date',                                                     // 27
    order: 'asc',                                                     // 28
  });

  readonly pageResource = this.eventService.listResource(this.query); // 30 — httpResource
  readonly venuesResource = this.eventService.venuesResource();       // 31 — справочник

  readonly data = computed<PagedResult<EventListItem> | undefined>(() => this.pageResource.value()); // 33
  readonly events = computed(() => this.data()?.items ?? []);         // 34 — всегда массив
  readonly total = computed(() => this.data()?.total ?? 0);           // 35
  readonly currentPage = computed(() => this.data()?.page ?? this.query().page); // 36
  readonly totalPages = computed(() => Math.max(this.data()?.totalPages ?? 1, 1)); // 37

  readonly filterState = computed<FilterState>(() => {                // 39 — для компонента фильтра
    const value = this.query();
    return {
      venueId: value.venueId,                                         // 42
      free: value.free,                                               // 43
      date: value.date,                                               // 44
      sort: value.sort,                                               // 45
      order: value.order,                                             // 46
    };
  });

  readonly hasFilters = computed(() => {                              // 49 — активны ли фильтры
    const value = this.query();
    return value.venueId !== null || value.free || value.date !== null; // 51
  });

  readonly actionError = signal<string | null>(null);                 // 54 — ошибка удаления
  readonly deletingId = signal<number | null>(null);                  // 55 — какую запись удаляем

  readonly errorText = errorText;                                     // 57 — функция для шаблона

  applyFilter(state: FilterState): void {                             // 59
    this.query.update((value) => ({ ...value, ...state, page: 1 }));  // 60 — новый фильтр → с первой страницы
  }

  resetFilter(): void {                                               // 63
    this.query.update((value) => ({ ...value, ...DEFAULT_FILTER, page: 1 })); // 64
  }

  goToPage(page: number): void {                                      // 67
    const target = Math.min(Math.max(page, 1), this.totalPages());    // 68 — не выходим за границы
    this.query.update((value) => ({ ...value, page: target }));       // 69
  }

  changePageSize(event: Event): void {                                // 72
    const pageSize = Number((event.target as HTMLSelectElement).value); // 73
    this.query.update((value) => ({ ...value, pageSize, page: 1 }));  // 74 — размер → с начала
  }

  onEdit(item: EventListItem): void {                                 // 77
    void this.router.navigate(['/events', item.id, 'edit']);          // 78
  }

  onRemove(item: EventListItem): void {                               // 81
    if (!window.confirm(`Удалить мероприятие «${item.title}»?`)) {    // 82 — подтверждение
      return;
    }

    this.actionError.set(null);                                       // 86
    this.deletingId.set(item.id);                                     // 87

    this.eventService.delete(item.id).subscribe({                     // 89 — DELETE
      next: () => {                                                   // 90
        this.deletingId.set(null);                                    // 91
        if (this.events().length === 1 && this.currentPage() > 1) {   // 92 — удалили последнюю на странице?
          this.query.update((value) => ({ ...value, page: value.page - 1 })); // 93 — шаг назад
        } else {
          this.pageResource.reload();                                 // 95 — иначе перезапрашиваем
        }
      },
      error: (error: unknown) => {                                    // 97
        this.deletingId.set(null);
        this.actionError.set(errorText(error));                       // 99 — 403/400 показываем баннером
      },
    });
  }

  retry(): void {                                                     // 103
    this.venuesResource.reload();                                     // 104
    this.pageResource.reload();                                       // 105
  }
}
```

Обратите внимание: `query.update(...)` создаёт **новый объект**, поэтому
`httpResource` видит изменение сигнала и перезапрашивает список. Мутация
`value.page = 2` не сработала бы — Angular сравнивает ссылки.

### `event-list-page.html` — четыре состояния

```html
<section class="page">                                         <!-- 1 -->
  <header class="page__head">                                  <!-- 2 -->
    <div class="page__intro">                                  <!-- 3 -->
      <p class="eyebrow">Афиша Ташкента</p>                    <!-- 4 -->
      <h1>Куда пойти</h1>                                      <!-- 5 -->
      <p class="lede">События, площадки и билеты — в одном месте.</p> <!-- 6 -->
    </div>

    @if (auth.isAuthenticated()) {                             <!-- 9 -->
      <a class="btn btn--primary" routerLink="/events/new">+ Добавить мероприятие</a> <!-- 10 -->
    } @else {                                                  <!-- 11 -->
      <a class="btn btn--primary" routerLink="/login"          <!-- 12 -->
         [queryParams]="{ returnUrl: '/events/new' }">Войти и добавить</a> <!-- 13 -->
    }
  </header>

  <app-event-filter                                           <!-- 17 -->
    [venues]="venuesResource.value() ?? []"                   <!-- 18 — справочник -->
    [value]="filterState()"                                   <!-- 19 — текущее состояние -->
    (changed)="applyFilter($event)"                           <!-- 20 — фильтр изменился -->
  />

  @if (actionError()) {                                       <!-- 23 — ошибка действия -->
    <div class="alert alert--error" role="alert">
      <span>{{ actionError() }}</span>
      <button type="button" class="alert__close" (click)="actionError.set(null)" aria-label="Закрыть">×</button>
    </div>
  }

  @if (pageResource.isLoading() && !data()) {                 <!-- 30 — СОСТОЯНИЕ 1: загрузка -->
    <div class="skeleton-grid" aria-label="Загрузка">
      @for (item of [1, 2, 3, 4, 5, 6]; track item) {         <!-- 32 -->
        <div class="skeleton-card">
          <div class="skeleton skeleton--cover"></div>
          <div class="skeleton skeleton--line"></div>
          <div class="skeleton skeleton--line skeleton--short"></div>
        </div>
      }
    </div>
  } @else if (pageResource.error()) {                         <!-- 39 — СОСТОЯНИЕ 2: ошибка -->
    <div class="state state--error">
      <h2>Не удалось загрузить афишу</h2>
      <p>{{ errorText(pageResource.error()) }}</p>             <!-- 42 -->
      <button type="button" class="btn btn--primary" (click)="retry()">Повторить</button>
    </div>
  } @else if (events().length === 0) {                        <!-- 45 — СОСТОЯНИЕ 3: пусто -->
    <div class="state state--empty">
      <div class="state__icon">🎭</div>
      <h2>{{ hasFilters() ? 'Ничего не нашлось' : 'Афиша пока пуста' }}</h2> <!-- 48 -->
      <p>
        @if (hasFilters()) {                                   <!-- 50 -->
          Попробуйте изменить площадку, дату или снять фильтр «бесплатные».
        } @else {
          Станьте первым, кто добавит мероприятие.
        }
      </p>
      @if (hasFilters()) {                                     <!-- 57 -->
        <button type="button" class="btn btn--primary" (click)="resetFilter()">Сбросить фильтры</button>
      } @else if (auth.isAuthenticated()) {
        <a class="btn btn--primary" routerLink="/events/new">Добавить мероприятие</a>
      }
    </div>
  } @else {                                                    <!-- 62 — СОСТОЯНИЕ 4: данные -->
    <div class="list-meta">
      <p>Найдено: <strong>{{ total() }}</strong></p>           <!-- 64 -->
      @if (pageResource.isLoading()) {
        <span class="list-meta__updating">Обновляем…</span>    <!-- 66 -->
      }
    </div>

    <app-event-list                                            <!-- 70 -->
      [events]="events()"
      [currentUserId]="auth.currentUserId()"                   <!-- 72 -->
      (edit)="onEdit($event)"
      (remove)="onRemove($event)"
    />

    <nav class="pagination" aria-label="Постраничная навигация"> <!-- 77 -->
      <button type="button" class="btn btn--ghost"             <!-- 78 -->
              [disabled]="currentPage() <= 1"                  <!-- 79 -->
              (click)="goToPage(currentPage() - 1)">← Назад</button> <!-- 80 -->

      <span class="pagination__info">                          <!-- 82 -->
        Страница <strong>{{ currentPage() }}</strong> из <strong>{{ totalPages() }}</strong>
      </span>

      <button type="button" class="btn btn--ghost"             <!-- 86 -->
              [disabled]="currentPage() >= totalPages()"       <!-- 87 -->
              (click)="goToPage(currentPage() + 1)">Вперёд →</button> <!-- 88 -->

      <label class="pagination__size">                         <!-- 90 -->
        На странице:
        <select [value]="query().pageSize" (change)="changePageSize($event)"> <!-- 92 -->
          <option value="3">3</option>
          <option value="6">6</option>
          <option value="12">12</option>
        </select>
      </label>
    </nav>
  }
</section>
```

Условие `isLoading() && !data()` показывает скелетон только при **первой** загрузке.
При смене фильтра старые данные остаются на экране, а сверху появляется «Обновляем…» —
так интерфейс не мигает.

### `event-list-page.css` — блоки

| Селектор | Зачем |
| --- | --- |
| `.page`, `.page__head` | вертикальные отступы, шапка с кнопкой справа |
| `.page__intro h1` | крупный заголовок, `clamp()` для адаптивности |
| `.list-meta` | строка «Найдено: N» |
| `.pagination` | центрирование кнопок «Назад/Вперёд» |
| `.skeleton*` | серые заглушки с анимацией `shimmer` |
| `@media` | на узком экране шапка переносится |

---

## Что спросить у себя после этого раздела

1. Что делает `input.required` и что будет, если вход не передан?
2. Зачем `output`, если карточка могла бы сама вызвать сервис?
3. Почему `query.update` создаёт новый объект?
4. Как `httpResource` узнаёт, что фильтр изменился?
5. Какие четыре состояния есть у списка и как они определяются?
6. Почему при удалении последней записи страница уменьшается?
7. Зачем `[disabled]` на кнопках пагинации, если сервер всё равно проверит page?
8. Как работает `@for ... track item.id`?
