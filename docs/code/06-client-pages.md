# 06. Клиент: страница мероприятия и форма

Здесь две самые содержательные страницы: `/events/:id` и `/events/new` + `/events/:id/edit`.

> **Роли:** управление доступно только администратору; проверка `canManage` — в [09-roles.md](09-roles.md).

---

## `pages/event-details-page` — одна запись

### `event-details-page.ts`

```ts
import { Component, computed, inject, input, signal } from '@angular/core'; // 1
import { Router, RouterLink } from '@angular/router';                       // 2
import { EventService } from '../../core/event.service';                     // 3
import { AuthService } from '../../core/auth.service';                       // 4
import { errorText, problemFrom } from '../../core/api-error';               // 5
import { formatDateTime, formatPrice, minutesUntil } from '../../core/format'; // 6

const MAX_POSTER_SIZE = 5 * 1024 * 1024;                                     // 8 — 5 МБ на клиенте
const ALLOWED_TYPES = ['image/jpeg', 'image/png', 'image/webp'];             // 9 — разрешённые MIME

@Component({
  selector: 'app-event-details-page',
  imports: [RouterLink],
  templateUrl: './event-details-page.html',
  styleUrl: './event-details-page.css',
})
export class EventDetailsPage {
  private readonly eventService = inject(EventService);                       // 18
  private readonly router = inject(Router);                                  // 19
  readonly auth = inject(AuthService);                                       // 20 — нужен шаблону

  readonly id = input.required<string>();                                    // 22 — id из маршрута (withComponentInputBinding)

  readonly eventId = computed(() => {                                        // 24 — строка → число
    const value = Number(this.id());                                         // 25
    return Number.isInteger(value) && value > 0 ? value : undefined;         // 26 — некорректный id → undefined
  });

  readonly details = this.eventService.detailResource(this.eventId);         // 29 — httpResource

  readonly event = computed(() => this.details.value());                     // 31 — данные или undefined
  readonly canManage = computed(() => this.auth.isAdmin());                   // 32 — роль Admin

  readonly when = computed(() => {                                           // 37 — дата текстом
    const value = this.event();
    return value ? formatDateTime(value.startsAt) : '';                      // 39
  });
  readonly price = computed(() => {                                          // 41 — цена текстом
    const value = this.event();
    return value ? formatPrice(value.price) : '';                            // 43
  });
  readonly lockHint = computed(() => {                                       // 45 — предупреждение о 24 часах
    const value = this.event();
    if (!value) {
      return '';
    }
    const minutes = minutesUntil(value.startsAt);                            // 50
    return minutes < 24 * 60                                                 // 51 — меньше суток?
      ? 'До начала меньше 24 часов — редактирование и удаление заблокированы.'
      : '';
  });

  readonly uploading = signal(false);                                        // 56 — идёт загрузка файла
  readonly deleting = signal(false);                                         // 57 — идёт удаление
  readonly uploadError = signal<string | null>(null);                        // 58 — ошибка файла
  readonly actionError = signal<string | null>(null);                        // 59 — ошибка удаления
  readonly errorText = errorText;                                            // 60 — функция для шаблона

  onFileSelected(event: Event): void {                                       // 62 — выбрали файл
    const input = event.target as HTMLInputElement;                          // 63
    const file = input.files?.[0];                                           // 64 — первый файл
    input.value = '';                                                        // 65 — сбрасываем input, чтобы можно было выбрать тот же файл снова

    if (!file) {                                                             // 67
      return;
    }

    this.uploadError.set(null);                                              // 71

    if (!ALLOWED_TYPES.includes(file.type)) {                                // 73 — проверка MIME на клиенте
      this.uploadError.set('Допустимые форматы: jpg, png, webp.');
      return;
    }

    if (file.size > MAX_POSTER_SIZE) {                                       // 78 — проверка размера
      this.uploadError.set('Файл не должен превышать 5 МБ.');
      return;
    }

    const id = this.eventId();                                               // 83
    if (!id) {
      return;
    }

    this.uploading.set(true);                                                // 88
    this.eventService.uploadPoster(id, file).subscribe({                     // 89 — POST multipart
      next: () => {
        this.uploading.set(false);                                           // 91
        this.details.reload();                                               // 92 — перечитываем запись с новым posterUrl
      },
      error: (error: unknown) => {                                           // 94
        this.uploading.set(false);
        const problem = problemFrom(error);                                  // 96
        const message = problem?.errors?.['file']?.join(' ');                // 97 — ошибка сервера по полю file
        this.uploadError.set(message ?? errorText(error));                   // 98
      },
    });
  }

  remove(): void {                                                           // 103 — удаление
    const item = this.event();
    if (!item || !window.confirm(`Удалить мероприятие «${item.title}»?`)) {   // 105 — подтверждение
      return;
    }

    this.deleting.set(true);                                                 // 109
    this.actionError.set(null);                                              // 110

    this.eventService.delete(item.id).subscribe({                            // 112
      next: () => {
        this.deleting.set(false);
        void this.router.navigate(['/events']);                              // 115 — назад к списку
      },
      error: (error: unknown) => {                                           // 117
        this.deleting.set(false);
        this.actionError.set(errorText(error));                              // 119 — 403/400 показываем баннером
      },
    });
  }

  retry(): void {                                                            // 124
    this.details.reload();                                                   // 125
  }
}
```

Сервер повторно проверяет автора и 24 часа — клиентские проверки только для удобства.
Кнопки вообще не показываются, если `canManage()` ложно.

### `event-details-page.html` — четыре состояния

```html
<section class="page">                                          <!-- 1 -->
  <a class="back-link" routerLink="/events">← Ко всем событиям</a> <!-- 2 -->

  @if (details.isLoading() && !event()) {                       <!-- 4 — СОСТОЯНИЕ 1: загрузка -->
    <div class="details details--loading" aria-label="Загрузка"> <!-- 5 -->
      <div class="skeleton details__poster-skeleton"></div>
      <div class="details__body">
        <div class="skeleton skeleton--line skeleton--short"></div>
        <div class="skeleton skeleton--title"></div>
        <div class="skeleton skeleton--line"></div>
        <div class="skeleton skeleton--line"></div>
        <div class="skeleton skeleton--line skeleton--short"></div>
      </div>
    </div>
  } @else if (details.statusCode() === 404) {                   <!-- 13 — СОСТОЯНИЕ 2: пусто/не найдено -->
    <div class="state state--empty">
      <div class="state__icon">🔎</div>
      <h1>Мероприятие не найдено</h1>
      <p>Возможно, его удалили или ссылка устарела.</p>
      <a class="btn btn--primary" routerLink="/events">Вернуться в афишу</a>
    </div>
  } @else if (details.error()) {                                <!-- 21 — СОСТОЯНИЕ 3: ошибка -->
    <div class="state state--error">
      <h1>Не удалось загрузить мероприятие</h1>
      <p>{{ errorText(details.error()) }}</p>
      <button type="button" class="btn btn--primary" (click)="retry()">Повторить</button>
    </div>
  } @else if (event(); as item) {                               <!-- 29 — СОСТОЯНИЕ 4: данные -->
    <article class="details">
      <div class="details__poster">                             <!-- 31 -->
        @if (item.posterUrl) {                                  <!-- 32 -->
          <img [src]="item.posterUrl" [alt]="'Постер: ' + item.title" /> <!-- 33 -->
        } @else {
          <div class="details__poster-empty">                    <!-- 35 — заглушка -->
            <span>{{ item.title.charAt(0) }}</span>             <!-- 36 -->
            <small>Постер ещё не загружен</small>
          </div>
        }
      </div>

      <div class="details__body">                               <!-- 42 -->
        <p class="eyebrow">{{ item.venue.name }}</p>            <!-- 43 -->
        <h1>{{ item.title }}</h1>                               <!-- 44 -->

        <dl class="facts">                                      <!-- 46 — таблица фактов -->
          <div class="fact">
            <dt>Когда</dt>
            <dd>{{ when() }}</dd>                               <!-- 49 -->
          </div>
          <div class="fact">
            <dt>Где</dt>
            <dd>{{ item.venue.city }}, {{ item.venue.address }}</dd> <!-- 53 -->
          </div>
          <div class="fact">
            <dt>Цена</dt>
            <dd [class.fact--free]="item.price === 0">{{ price() }}</dd> <!-- 57 -->
          </div>
          <div class="fact">
            <dt>Мест</dt>
            <dd>{{ item.seats }}</dd>                           <!-- 61 -->
          </div>
        </dl>

        @if (item.description) {                                <!-- 65 -->
          <p class="details__description">{{ item.description }}</p>
        }

        <p class="details__author">Автор записи: <strong>{{ item.authorName }}</strong></p> <!-- 69 -->

        @if (lockHint()) {                                      <!-- 71 — предупреждение до 24 часов -->
          <div class="alert alert--warning">{{ lockHint() }}</div>
        }

        @if (actionError()) {                                   <!-- 75 -->
          <div class="alert alert--error" role="alert">{{ actionError() }}</div>
        }

        @if (uploadError()) {                                   <!-- 79 -->
          <div class="alert alert--error" role="alert">{{ uploadError() }}</div>
        }

        @if (canManage()) {                                      <!-- 83 — действия администратора -->
          <div class="details__actions">
            <a class="btn btn--primary" [routerLink]="['/events', item.id, 'edit']">Редактировать</a> <!-- 85 -->
            <button type="button" class="btn btn--danger" [disabled]="deleting()" (click)="remove()"> <!-- 86 -->
              {{ deleting() ? 'Удаляем…' : 'Удалить' }}
            </button>
            <label class="btn btn--ghost upload-button">        <!-- 89 -->
              {{ uploading() ? 'Загружаем…' : 'Загрузить постер' }}
              <input
                type="file"                                      <!-- 92 -->
                accept="image/jpeg,image/png,image/webp"         <!-- 93 -->
                [disabled]="uploading()"
                (change)="onFileSelected($event)"                <!-- 95 -->
              />
            </label>
          </div>
          <p class="hint">Постер: jpg, png или webp до 5 МБ. Кнопка загрузки видна только администратору.</p>
        }
      </div>
    </article>
  }
</section>
```

`details.statusCode() === 404` проверяется **до** `details.error()`, потому что 404
для `httpResource` — тоже ошибка, но мы хотим показать «не найдено», а не «ошибка
сервера».

### `event-details-page.css` — блоки

| Селектор | Зачем |
| --- | --- |
| `.details` | две колонки: постер и текст, на узком экране — одна |
| `.details__poster img` | пропорция 4:5, скругление, тень |
| `.details__poster-empty` | тёмная заглушка с первой буквой |
| `.facts` | сетка 2×2 с рамкой |
| `.fact dt/dd` | подпись капсом и значение |
| `.details__actions` | кнопки автора, линия сверху |
| `.upload-button input` | прозрачный input поверх кнопки, чтобы клик открывал выбор файла |

---

## `pages/event-form-page` — создание и редактирование

Один компонент обслуживает два маршрута: `/events/new` (без `id`) и
`/events/:id/edit` (с `id`).

### Валидаторы

```ts
const futureDateValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => { // 11 — своя проверка
  const value = control.value as string;                      // 12 — значение datetime-local
  if (!value) {                                               // 13 — пусто: пусть сработает required
    return null;
  }
  return new Date(value).getTime() > Date.now() ? null : { future: true }; // 16 — дата в будущем?
};

const integerValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => { // 19
  const value = control.value;                                // 20
  if (value === null || value === undefined || value === '') { // 21
    return null;                                              // 22
  }
  return Number.isInteger(Number(value)) ? null : { integer: true }; // 24 — целое?
};
```

Эти же правила есть на сервере: дата в будущем и целое количество мест.

### Класс

```ts
export class EventFormPage {
  private readonly eventService = inject(EventService);       // 34
  private readonly auth = inject(AuthService);                // 35
  private readonly router = inject(Router);                   // 36
  private readonly fb = inject(FormBuilder);                  // 37

  readonly id = input<string>();                              // 39 — id есть только на /edit

  readonly eventId = computed(() => {                         // 41
    const raw = this.id();
    if (!raw) {
      return undefined;                                       // 44 — режим создания
    }
    const value = Number(raw);
    return Number.isInteger(value) && value > 0 ? value : undefined; // 47
  });

  readonly isEdit = computed(() => this.eventId() !== undefined); // 50 — режим?
  readonly details = this.eventService.detailResource(this.eventId); // 51 — для edit загружаем запись
  readonly venues = this.eventService.venuesResource();       // 52 — справочник для select

  readonly form = this.fb.nonNullable.group({                 // 54 — все контролы не null
    title: ['', [Validators.required, nonBlankValidator, Validators.minLength(3), Validators.maxLength(200)]], // 55
    description: ['', [Validators.maxLength(2000)]],          // 56
    startsAt: ['', [Validators.required, futureDateValidator]], // 57
    price: [0, [Validators.required, Validators.min(0), Validators.max(1_000_000)]], // 58
    seats: [1, [Validators.required, Validators.min(1), Validators.max(50_000), integerValidator]], // 59
    venueId: [0, [Validators.required, Validators.min(1)]],   // 60 — 0 = «не выбрано»
  });

  readonly minDateTime = toDatetimeLocal(new Date().toISOString()); // 63 — min для input
  readonly submitted = signal(false);                         // 64 — была ли попытка отправки
  readonly saving = signal(false);                            // 65 — идёт сохранение
  readonly generalError = signal<string | null>(null);        // 66 — общая ошибка
  readonly errorText = errorText;                             // 67

  private readonly patchedId = signal<number | null>(null);   // 69 — какую запись уже заполнили

  readonly loadingEvent = computed(                           // 71 — загрузка только в edit
    () => this.isEdit() && this.details.isLoading() && !this.details.value(),
  );

  readonly forbidden = computed(() => this.isEdit() && !this.auth.isAdmin()); // 75 — не админ?

  constructor() {
    effect(() => {                                            // 82 — реагируем на загрузку данных
      const data = this.details.value();
      if (!data || this.patchedId() === data.id) {            // 84 — уже заполняли?
        return;
      }

      this.patchedId.set(data.id);                            // 88
      this.form.patchValue({                                  // 89 — переносим данные в форму
        title: data.title,
        description: data.description ?? '',                  // 91
        startsAt: toDatetimeLocal(data.startsAt),             // 92 — ISO → локальное для input
        price: data.price,
        seats: data.seats,
        venueId: data.venue.id,                               // 95 — в DTO есть объект venue
      });
    });

    this.form.valueChanges.subscribe(() => this.clearServerErrors()); // 99 — пользователь правит → убираем серверные ошибки
  }

  submit(): void {
    this.submitted.set(true);                                 // 103
    this.generalError.set(null);                              // 104
    this.form.markAllAsTouched();                             // 105 — показать все ошибки

    if (this.form.invalid) {                                  // 107 — клиентская валидация
      return;
    }

    const id = this.eventId();                                // 111
    if (this.isEdit() && id === undefined) {                  // 112
      this.generalError.set('Некорректная ссылка на мероприятие.');
      return;
    }

    const raw = this.form.getRawValue();                      // 117 — значения формы
    const payload: EventPayload = {                           // 118
      title: raw.title.trim(),                                // 119
      description: raw.description.trim() || null,            // 120 — пустая строка → null
      startsAt: fromDatetimeLocal(raw.startsAt),              // 121 — локальное → UTC ISO
      price: Number(raw.price),                               // 122
      seats: Number(raw.seats),                               // 123
      venueId: Number(raw.venueId),                           // 124
    };

    this.saving.set(true);                                    // 127
    const request =                                           // 128
      this.isEdit() && id !== undefined
        ? this.eventService.update(id, payload)               // 130 — PUT
        : this.eventService.create(payload);                  // 131 — POST

    request.subscribe({
      next: (saved) => {                                      // 134
        this.saving.set(false);
        void this.router.navigate(['/events', saved.id]);     // 136 — на страницу сохранённой записи
      },
      error: (error: unknown) => {                            // 138
        this.saving.set(false);
        const problem = problemFrom(error);                   // 140
        if (problem?.errors) {                                // 141 — ошибки по полям
          this.setServerErrors(problem.errors);               // 142
          this.generalError.set('Проверьте поля — сервер нашёл ошибки.'); // 143
          return;
        }
        this.generalError.set(problem?.detail ?? problem?.title ?? errorText(error)); // 146 — 403/400/сеть
      },
    });
  }

  fieldError(name: string): string | null {                   // 151 — сообщение под полем
    const control = this.form.get(name);
    if (!control || (!control.touched && !this.submitted())) { // 153 — не показываем до касания/отправки
      return null;
    }

    const errors = control.errors;
    if (!errors) {
      return null;
    }

    if (errors['server']) {                                   // 162 — ошибка сервера приоритетнее
      return String(errors['server']);
    }
    if (errors['required']) {
      return 'Поле обязательно.';
    }
    if (errors['blank']) {
      return 'Поле не может состоять только из пробелов.';
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
      if (name === 'venueId') {                               // 181 — для select свой текст
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

  hasError(name: string): boolean {                           // 199 — для класса invalid
    return this.fieldError(name) !== null;
  }

  private setServerErrors(errors: Record<string, string[]>): void { // 203 — серверные ошибки в контролы
    for (const [field, messages] of Object.entries(errors)) { // 204
      const control = this.form.get(field);                   // 205
      if (control) {
        control.setErrors({ ...(control.errors ?? {}), server: messages.join(' ') }); // 207 — не затираем клиентские
        control.markAsTouched();                              // 208 — показать сообщение
      }
    }
  }

  private clearServerErrors(): void {                         // 213 — при правке убираем server
    for (const control of Object.values(this.form.controls)) { // 214
      const errors = control.errors;
      if (errors && 'server' in errors) {                     // 216
        const rest: ValidationErrors = { ...errors };         // 217
        delete rest['server'];                                // 218
        control.setErrors(Object.keys(rest).length > 0 ? rest : null); // 219 — остальные ошибки сохраняем
      }
    }
  }
}
```

Ключевая идея: клиентская валидация **дублирует** серверную, но не заменяет её.
Если сервер вернул `errors`, они кладутся в тот же контрол, и `fieldError` показывает
их под соответствующим полем.

### `event-form-page.html` — состояния и поля

```html
<section class="page">                                          <!-- 1 -->
  <header class="page__head">                                   <!-- 2 -->
    <a class="back-link" [routerLink]="isEdit() ? ['/events', id()] : '/events'">← Назад</a> <!-- 3 -->
    <p class="eyebrow">{{ isEdit() ? 'Редактирование' : 'Новая запись' }}</p> <!-- 4 -->
    <h1>{{ isEdit() ? 'Изменить мероприятие' : 'Добавить мероприятие' }}</h1>  <!-- 5 -->
    <p class="lede">
      Дата начала — только в будущем. Менять и удалять запись можно не позднее чем за 24 часа
      до начала.
    </p>
  </header>

  @if (loadingEvent()) {                                        <!-- 11 — загрузка (только edit) -->
    <div class="panel form-loading" aria-label="Загрузка">
      <div class="skeleton skeleton--line"></div>
      <div class="skeleton skeleton--line"></div>
      <div class="skeleton skeleton--line skeleton--short"></div>
      <div class="skeleton skeleton--line"></div>
    </div>
  } @else if (details.error()) {                                <!-- 18 — ошибка загрузки -->
    <div class="state state--error">
      <h2>Не удалось загрузить мероприятие</h2>
      <p>{{ errorText(details.error()) }}</p>
      <button type="button" class="btn btn--primary" (click)="details.reload()">Повторить</button>
    </div>
  } @else if (forbidden()) {                                    <!-- 25 — чужая запись -->
    <div class="state state--empty">
      <div class="state__icon">🔒</div>
      <h2>Это чужая запись</h2>
      <p>Редактировать мероприятие может только его автор.</p>
      <a class="btn btn--primary" routerLink="/events">Вернуться в афишу</a>
    </div>
  } @else {                                                     <!-- 33 — форма -->
    <form class="form panel" [formGroup]="form" (ngSubmit)="submit()" novalidate> <!-- 34 -->
      @if (generalError()) {                                    <!-- 35 -->
        <div class="alert alert--error" role="alert">{{ generalError() }}</div>
      }

      <div class="form__grid">                                  <!-- 39 -->
        <div class="field field--wide">                         <!-- 40 — название -->
          <label for="title">Название</label>
          <input
            id="title"
            type="text"
            formControlName="title"                             <!-- 44 — связь с контролом -->
            placeholder="Например, Ночь космоса"
            [class.invalid]="hasError('title')"                 <!-- 46 — красная рамка -->
            [attr.aria-invalid]="hasError('title')"
          />
          @if (fieldError('title')) {                           <!-- 49 -->
            <p class="field__error">{{ fieldError('title') }}</p>
          }
        </div>

        <div class="field field--wide">                         <!-- 53 — описание -->
          <label for="description">Описание</label>
          <textarea id="description" rows="4" formControlName="description" ...></textarea>
          @if (fieldError('description')) { <p class="field__error">{{ fieldError('description') }}</p> }
        </div>

        <div class="field">                                     <!-- 60 — дата -->
          <label for="startsAt">Дата и время начала</label>
          <input
            id="startsAt"
            type="datetime-local"
            formControlName="startsAt"
            [min]="minDateTime"                                 <!-- 65 — подсказка браузеру -->
            [class.invalid]="hasError('startsAt')"
          />
          @if (fieldError('startsAt')) { <p class="field__error">{{ fieldError('startsAt') }}</p> }
        </div>

        <div class="field">                                     <!-- 72 — площадка -->
          <label for="venueId">Площадка</label>
          <select id="venueId" formControlName="venueId">
            <option [ngValue]="0" disabled>Выберите площадку</option> <!-- 75 — 0 = не выбрано -->
            @for (venue of venues.value() ?? []; track venue.id) {    <!-- 76 -->
              <option [ngValue]="venue.id">{{ venue.name }} — {{ venue.address }}</option> <!-- 77 -->
            }
          </select>
          @if (fieldError('venueId')) { <p class="field__error">{{ fieldError('venueId') }}</p> }
        </div>

        <div class="field">                                     <!-- 82 — цена -->
          <label for="price">Цена, сум</label>
          <input id="price" type="number" min="0" step="1000" formControlName="price" ... />
          <p class="field__hint">0 — бесплатно.</p>
          @if (fieldError('price')) { <p class="field__error">{{ fieldError('price') }}</p> }
        </div>

        <div class="field">                                     <!-- 90 — места -->
          <label for="seats">Количество мест</label>
          <input id="seats" type="number" min="1" step="1" formControlName="seats" ... />
          @if (fieldError('seats')) { <p class="field__error">{{ fieldError('seats') }}</p> }
        </div>
      </div>

      <div class="form__actions">                               <!-- 98 -->
        <button type="submit" class="btn btn--primary" [disabled]="saving()"> <!-- 99 -->
          {{ saving() ? 'Сохраняем…' : isEdit() ? 'Сохранить' : 'Создать мероприятие' }}
        </button>
        <a class="btn btn--ghost" [routerLink]="isEdit() ? ['/events', id()] : '/events'">Отмена</a> <!-- 102 -->
      </div>
    </form>
  }
</section>
```

`[ngValue]` нужен для чисел: обычный `[value]` превратил бы `venueId` в строку.
`novalidate` отключает встроенную браузерную валидацию — показываем свои сообщения.

### `event-form-page.css` — блоки

| Селектор | Зачем |
| --- | --- |
| `.page` | ограничение ширины 860px и центрирование |
| `.form__grid` | две колонки, `.field--wide` на всю ширину |
| `.field` | колонка «label + input + ошибка» |
| `.invalid` | красная рамка и лёгкий фон |
| `.field__error` | красный текст под полем |
| `.form__actions` | кнопки сохранения/отмены |
| `@media` | одна колонка на телефоне |

---

## Что спросить у себя после этого раздела

1. Как один компонент понимает, создание это или редактирование?
2. Почему `effect` заполняет форму, а не конструктор?
3. Что делает `patchValue` и чем отличается от `setValue`?
4. Почему `form.invalid` проверяется до запроса?
5. Как серверная ошибка попадает под поле?
6. Зачем `clearServerErrors` при `valueChanges`?
7. Почему дата переводится в UTC перед отправкой?
8. Почему кнопка загрузки постера — `<label>` с `<input type="file">`?
9. Почему 404 проверяется раньше общей ошибки?
10. Что произойдёт, если открыть `/events/5/edit` под другим пользователем?
