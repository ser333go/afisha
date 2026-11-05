# 04. Клиент: конфигурация, маршруты и сервисы

Здесь `app.config.ts`, `app.routes.ts`, оболочка `App` и весь каталог `core/`.

> **Роли:** в `AuthService` добавлен `isAdmin`, для форм используется `adminGuard` (см. [09-roles.md](09-roles.md)).

---

## `src/main.ts`

```ts
import { bootstrapApplication } from '@angular/platform-browser'; // 1 — запуск standalone-приложения
import { appConfig } from './app/app.config';                    // 2 — провайдеры (router, http, interceptor)
import { App } from './app/app';                                 // 3 — корневой компонент

bootstrapApplication(App, appConfig)                             // 5 — создаём приложение
  .catch((err) => console.error(err));                           // 6 — если старт упал, пишем в консоль
```

В Angular 22 нет `AppModule`: приложение собирается из standalone-компонентов и
провайдеров в `appConfig`.

---

## `src/app/app.config.ts` — провайдеры приложения

```ts
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core'; // 1
import { provideRouter, withComponentInputBinding, withInMemoryScrolling } from '@angular/router'; // 2
import { provideHttpClient, withInterceptors } from '@angular/common/http'; // 3
import { routes } from './app.routes';                            // 4
import { authInterceptor } from './core/auth.interceptor';        // 5

export const appConfig: ApplicationConfig = {                     // 7 — объект конфигурации
  providers: [                                                    // 8 — список провайдеров
    provideBrowserGlobalErrorListeners(),                         // 9 — глобальные обработчики ошибок браузера
    provideRouter(                                                // 10 — регистрируем маршрутизатор
      routes,                                                     // 11 — таблица маршрутов
      withComponentInputBinding(),                                // 12 — параметры маршрута попадают в input() компонента
      withInMemoryScrolling({ scrollPositionRestoration: 'top' }),// 13 — при переходе скроллим наверх
    ),
    provideHttpClient(withInterceptors([authInterceptor])),       // 14 — HttpClient + наш интерсептор
  ],
};
```

`withComponentInputBinding()` — благодаря ему в `EventDetailsPage` объявлен
`id = input.required<string>()`, и Angular сам подставит `id` из URL `/events/5`.

---

## `src/app/app.routes.ts` — маршруты

```ts
import { Routes } from '@angular/router';                          // 1
import { authGuard } from './core/auth.guard';                     // 2 — проверка входа

export const routes: Routes = [                                    // 4
  { path: '', pathMatch: 'full', redirectTo: 'events' },           // 5 — пустой путь → /events

  {                                                                // 6
    path: 'events',                                                // 7
    title: 'Афиша — события',                                      // 8 — title документа
    loadComponent: () =>                                           // 9 — ленивая загрузка компонента
      import('./pages/event-list-page/event-list-page')            // 10 — отдельный чанк
        .then((m) => m.EventListPage),                             // 11
  },

  {                                                                // 13
    path: 'events/new',                                            // 14 — ВАЖНО: до events/:id
    title: 'Новое мероприятие',
    canActivate: [authGuard],                                      // 16 — без входа не пустит
    loadComponent: () =>
      import('./pages/event-form-page/event-form-page').then((m) => m.EventFormPage), // 18
  },

  {                                                                // 20
    path: 'events/:id',                                            // 21 — страница одной записи
    title: 'Мероприятие',
    loadComponent: () =>
      import('./pages/event-details-page/event-details-page').then((m) => m.EventDetailsPage), // 23
  },

  {                                                                // 25
    path: 'events/:id/edit',                                       // 26 — форма редактирования
    title: 'Редактирование мероприятия',
    canActivate: [authGuard],                                      // 28
    loadComponent: () =>
      import('./pages/event-form-page/event-form-page').then((m) => m.EventFormPage), // 30
  },

  { path: 'login', title: 'Вход', loadComponent: () => import('./pages/login-page/login-page').then((m) => m.LoginPage) }, // 32
  { path: 'register', title: 'Регистрация', loadComponent: () => import('./pages/register-page/register-page').then((m) => m.RegisterPage) }, // 33
  { path: '404', title: 'Страница не найдена', loadComponent: () => import('./pages/not-found-page/not-found-page').then((m) => m.NotFoundPage) }, // 34

  {                                                                // 35
    path: '**',                                                    // 36 — любой неизвестный путь
    title: 'Страница не найдена',
    loadComponent: () =>
      import('./pages/not-found-page/not-found-page').then((m) => m.NotFoundPage), // 38
  },
];
```

Порядок важен: `events/new` объявлен **раньше** `events/:id`, иначе `new` был бы
воспринят как id. `**` всегда последний.

---

## `src/app/app.ts` — корневой компонент

```ts
import { Component, inject } from '@angular/core';                 // 1
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router'; // 2
import { AuthService } from './core/auth.service';                 // 3

@Component({                                                       // 5
  selector: 'app-root',                                            // 6 — тег в index.html
  imports: [RouterOutlet, RouterLink, RouterLinkActive],           // 7 — что использует шаблон
  templateUrl: './app.html',                                       // 8
  styleUrl: './app.css',                                           // 9
})
export class App {
  readonly auth = inject(AuthService);                             // 12 — сервис входа (public для шаблона)
  readonly year = new Date().getFullYear();                        // 13 — год в подвале
}
```

`readonly auth = inject(...)` — современный способ получить зависимость без
конструктора. Поле публичное, чтобы шаблон мог читать `auth.isAuthenticated()`.

---

## `src/app/app.html` — шапка и подвал

```html
<div class="shell">                                                <!-- 1 — общая колонка -->
  <header class="header">                                          <!-- 2 -->
    <div class="container header__inner">                          <!-- 3 -->
      <a class="brand" routerLink="/events" aria-label="Афиша — на главную"> <!-- 4 -->
        <span class="brand__mark">А</span>                         <!-- 5 — квадрат с буквой -->
        <span class="brand__text">Афиша<em>.</em></span>           <!-- 6 — логотип -->
      </a>

      <nav class="nav" aria-label="Основная навигация">            <!-- 9 -->
        <a routerLink="/events" routerLinkActive="is-active"       <!-- 10 — активная ссылка -->
           [routerLinkActiveOptions]="{ exact: true }">События</a>  <!-- 11 — точное совпадение -->
        @if (auth.isAuthenticated()) {                             <!-- 12 — блок управления из Angular 17+ -->
          <a routerLink="/events/new" routerLinkActive="is-active">Добавить</a> <!-- 13 -->
        }
      </nav>

      <div class="account">                                        <!-- 16 -->
        @if (auth.isAuthenticated()) {                             <!-- 17 -->
          <span class="account__name" title="Вы вошли">             <!-- 18 -->
            <span class="account__dot" aria-hidden="true"></span>  <!-- 19 -->
            {{ auth.currentUser()?.displayName }}                  <!-- 20 — имя из сигнала -->
          </span>
          <button type="button" class="btn btn--ghost btn--small"  <!-- 21 -->
                  (click)="auth.logout()">Выйти</button>           <!-- 22 — выход -->
        } @else {                                                  <!-- 23 -->
          <a class="btn btn--ghost btn--small" routerLink="/login">Войти</a> <!-- 24 -->
          <a class="btn btn--primary btn--small" routerLink="/register">Регистрация</a> <!-- 25 -->
        }
      </div>
    </div>
  </header>

  <main class="container main">                                    <!-- 30 -->
    <router-outlet />                                              <!-- 31 — сюда рендерится страница -->
  </main>

  <footer class="footer">                                          <!-- 34 -->
    <div class="container footer__inner">                          <!-- 35 -->
      <span>Афиша Ташкента — учебный проект</span>                 <!-- 36 -->
      <span>ASP.NET Core 10 · Angular 22 · {{ year }}</span>       <!-- 37 -->
    </div>
  </footer>
</div>
```

`@if` — новый синтаксис шаблонов Angular (вместо `*ngIf`). `?.` — безопасное
обращение: пока пользователь не загружен, будет `undefined`, а не ошибка.

---

## `core/models.ts` — типы данных

```ts
export interface Venue {              // 1 — площадка
  id: number;                         // 2
  name: string;                       // 3
  address: string;                    // 4
  city: string;                       // 5
}

export interface EventListItem {      // 8 — элемент списка
  id: number;                         // 9
  title: string;                      // 10
  startsAt: string;                   // 11 — ISO-строка, приходит с Z
  price: number;                      // 12
  seats: number;                      // 13
  venue: Venue;                       // 14 — вложенный объект
  posterUrl: string | null;           // 15
  authorId: number;                   // 16
}

export interface EventDetails extends EventListItem { // 19 — детали = всё из списка + ещё поля
  description: string | null;         // 20
  authorName: string;                 // 21
  createdAt: string;                  // 22
}

export interface PagedResult<T> {     // 25 — страница
  items: T[];                         // 26
  page: number;                       // 27
  pageSize: number;                   // 28
  total: number;                      // 29
  totalPages: number;                 // 30
}

export interface EventPayload {       // 33 — тело POST/PUT
  title: string;
  description: string | null;
  startsAt: string;
  price: number;
  seats: number;
  venueId: number;
}

export type SortField = 'date' | 'price';   // 42 — допустимые поля
export type SortOrder = 'asc' | 'desc';     // 43 — направления

export interface EventQuery {         // 45 — состояние списка
  page: number;
  pageSize: number;
  venueId: number | null;
  free: boolean;
  date: string | null;
  sort: SortField;
  order: SortOrder;
}

export interface FilterState {        // 55 — то, что отдаёт компонент фильтра (без page)
  venueId: number | null;
  free: boolean;
  date: string | null;
  sort: SortField;
  order: SortOrder;
}

export interface User {               // 63
  id: number;
  email: string;
  displayName: string;
}

export interface AuthResponse {       // 69
  token: string;                      // 70 — JWT
  user: User;                         // 71
}

export interface LoginPayload { email: string; password: string; }        // 74
export interface RegisterPayload { email: string; displayName: string; password: string; } // 76
export interface PosterUploaded { posterUrl: string; }                   // 78
```

Интерфейсы — только типы, в JS они исчезают при сборке. Валидации во время выполнения
они не дают; ответ API считается доверенным (в учебном проекте).

---

## `core/format.ts` — форматирование дат и цены

```ts
const dateFormatter = new Intl.DateTimeFormat('ru-RU', {   // 1 — форматтер «14 февраля 2027 г. в 18:00»
  day: 'numeric',
  month: 'long',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
});

const dateOnlyFormatter = new Intl.DateTimeFormat('ru-RU', { // 8 — без времени
  day: 'numeric',
  month: 'long',
  year: 'numeric',
});

const moneyFormatter = new Intl.NumberFormat('ru-RU');      // 14 — «120 000»

export function formatDateTime(iso: string): string {
  return dateFormatter.format(new Date(iso));               // 18 — строка → Date → текст
}

export function formatDate(iso: string): string {
  return dateOnlyFormatter.format(new Date(iso));           // 22
}

export function formatPrice(value: number): string {
  return value === 0                                        // 26 — 0 — это бесплатно
    ? 'Бесплатно'
    : `${moneyFormatter.format(value)} сум`;                // 28
}

/** Локальное значение для <input type="datetime-local">. */
export function toDatetimeLocal(iso: string): string {      // 32
  const date = new Date(iso);
  const pad = (value: number) => value.toString().padStart(2, '0'); // 34 — «5» → «05»
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`; // 35
}

/** Значение datetime-local (локальное) → ISO UTC для сервера. */
export function fromDatetimeLocal(value: string): string {
  return new Date(value).toISOString();                     // 40 — локальное время → UTC-строка с Z
}

export function minutesUntil(iso: string): number {
  return Math.round((new Date(iso).getTime() - Date.now()) / 60000); // 44 — сколько минут до события
}
```

`new Date(value)` для `datetime-local` трактует строку как **локальное** время —
именно это нужно для ввода пользователя. `toISOString()` переводит в UTC, который
понимает сервер.

---

## `core/api-error.ts` — разбор ошибок API

```ts
import { HttpErrorResponse } from '@angular/common/http';  // 1

export interface ProblemDetails {                          // 3 — форма ответа сервера
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}

/** Достаёт ProblemDetails из ошибки HttpClient. */
export function problemFrom(error: unknown): ProblemDetails | null { // 10
  if (error instanceof HttpErrorResponse) {                // 11 — обычная ошибка Angular
    const payload: unknown = error.error;                  // 12 — тело ответа
    if (payload && typeof payload === 'object') {          // 13
      return payload as ProblemDetails;                    // 14
    }
  }

  if (error && typeof error === 'object' && 'error' in error) { // 17 — ошибка из httpResource
    const payload = (error as { error: unknown }).error;   // 18
    if (payload && typeof payload === 'object') {          // 19
      return payload as ProblemDetails;                    // 20
    }
  }

  return null;                                             // 23
}

export function errorText(error: unknown): string {        // 26 — человекочитаемый текст
  const problem = problemFrom(error);
  if (problem?.detail) {                                   // 28 — detail приоритетнее
    return problem.detail;
  }
  if (problem?.title) {
    return problem.title;
  }

  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) {                              // 35 — сеть/сервер недоступен
      return 'Сервер недоступен. Проверьте, что API запущен.';
    }
    return `Ошибка запроса (${error.status}).`;            // 38
  }

  return 'Не удалось выполнить запрос. Попробуйте ещё раз.'; // 41
}

export function fieldErrors(problem: ProblemDetails | null): Record<string, string[]> {
  return problem?.errors ?? {};                            // 45 — ошибки по полям
}
```

`httpResource` оборачивает ошибку иначе, чем `HttpClient`, поэтому `problemFrom`
поддерживает оба варианта.

---

## `core/token-storage.ts` — токен в localStorage

```ts
import { Injectable } from '@angular/core';
import { User } from './models';

const TOKEN_KEY = 'afisha.token';                          // 4 — ключ токена
const USER_KEY = 'afisha.user';                            // 5 — ключ пользователя

@Injectable({ providedIn: 'root' })                        // 7 — один экземпляр на всё приложение
export class TokenStorage {
  token(): string | null {                                 // 9
    return this.read(TOKEN_KEY);                           // 10
  }

  user(): User | null {                                    // 13
    const raw = this.read(USER_KEY);                       // 14
    if (!raw) {
      return null;
    }

    try {
      return JSON.parse(raw) as User;                      // 19
    } catch {
      return null;                                         // 21 — битый JSON не должен ломать приложение
    }
  }

  save(token: string, user: User): void {                  // 25
    this.write(TOKEN_KEY, token);                          // 26
    this.write(USER_KEY, JSON.stringify(user));            // 27
  }

  clear(): void {                                          // 30
    try {
      localStorage.removeItem(TOKEN_KEY);                  // 32
      localStorage.removeItem(USER_KEY);                   // 33
    } catch {
      // localStorage может быть недоступен — молча игнорируем.
    }
  }

  private read(key: string): string | null {               // 39
    try {
      return localStorage.getItem(key);                    // 41
    } catch {
      return null;                                         // 43
    }
  }

  private write(key: string, value: string): void {        // 46
    try {
      localStorage.setItem(key, value);                    // 48
    } catch {
      // ignore
    }
  }
}
```

Отдельный сервис нужен, чтобы **интерсептор** мог читать токен, не завися от
`AuthService` (иначе получилась бы циклическая зависимость: AuthService → HttpClient
→ interceptor → AuthService).

---

## `core/auth.service.ts` — состояние входа

```ts
import { computed, inject, Injectable, signal } from '@angular/core'; // 1
import { HttpClient } from '@angular/common/http';                     // 2
import { Router } from '@angular/router';                              // 3
import { tap } from 'rxjs';                                            // 4
import { AuthResponse, LoginPayload, RegisterPayload, User } from './models'; // 5
import { TokenStorage } from './token-storage';                        // 6

@Injectable({ providedIn: 'root' })                                    // 8
export class AuthService {
  private readonly http = inject(HttpClient);                          // 10 — запросы
  private readonly storage = inject(TokenStorage);                     // 11 — localStorage
  private readonly router = inject(Router);                            // 12 — навигация после logout

  private readonly tokenSignal = signal<string | null>(this.storage.token()); // 14 — стартовое значение из localStorage
  private readonly userSignal = signal<User | null>(this.storage.user());     // 15

  readonly token = this.tokenSignal.asReadonly();                      // 17 — снаружи только читать
  readonly currentUser = this.userSignal.asReadonly();                 // 18
  readonly isAuthenticated = computed(() => this.tokenSignal() !== null); // 19 — производный сигнал
  readonly isAdmin = computed(() => this.userSignal()?.role === 'Admin'); // 20 — роль администратора
  readonly currentUserId = computed(() => this.userSignal()?.id ?? null); // 20

  login(payload: LoginPayload) {                                       // 22
    return this.http                                                    // 23
      .post<AuthResponse>('/api/auth/login', payload)                   // 24
      .pipe(tap((response) => this.apply(response)));                   // 25 — после успеха сохраняем токен
  }

  register(payload: RegisterPayload) {                                 // 28
    return this.http
      .post<AuthResponse>('/api/auth/register', payload)
      .pipe(tap((response) => this.apply(response)));                   // 31
  }

  logout(): void {                                                     // 34
    this.storage.clear();                                              // 35 — чистим localStorage
    this.tokenSignal.set(null);                                        // 36 — чистим сигналы
    this.userSignal.set(null);                                         // 37
    void this.router.navigate(['/events']);                            // 38 — уходим на список
  }

  private apply(response: AuthResponse): void {                        // 41
    this.storage.save(response.token, response.user);                  // 42 — сохраняем между перезагрузками
    this.tokenSignal.set(response.token);                              // 43
    this.userSignal.set(response.user);                                // 44
  }
}
```

`signal` — реактивная ячейка. Шаблон, прочитавший `auth.isAuthenticated()`,
автоматически перерисуется после `login/logout`. `computed` пересчитывается только
при изменении зависимости.

---

## `core/auth.interceptor.ts` — токен в каждом запросе

```ts
import { inject } from '@angular/core';
import { HttpInterceptorFn } from '@angular/common/http';
import { TokenStorage } from './token-storage';

/** Добавляет JWT к каждому запросу, если пользователь вошёл. */
export const authInterceptor: HttpInterceptorFn = (request, next) => { // 6 — функциональный интерсептор
  const token = inject(TokenStorage).token();                           // 7 — читаем токен из localStorage
  if (!token) {                                                         // 8 — гость: запрос без заголовка
    return next(request);
  }

  return next(                                                          // 12 — передаём запрос дальше
    request.clone({                                                     // 13 — клонируем (запросы неизменяемы)
      setHeaders: { Authorization: `Bearer ${token}` },                 // 14 — добавляем заголовок
    }),
  );
};
```

`inject()` работает в функциональном интерсепторе, потому что Angular вызывает его
в контексте внедрения зависимостей.

---

## `core/auth.guard.ts` — защита маршрутов

```ts
import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/** Не пускает на формы без входа. */
export const authGuard: CanActivateFn = (_route, state) => {   // 6 — функциональный guard
  const auth = inject(AuthService);                            // 7
  const router = inject(Router);                               // 8

  if (auth.isAuthenticated()) {                                // 10 — вошёл?
    return true;                                               // 11 — пускаем
  }

  return router.createUrlTree(['/login'], {                    // 14 — иначе редирект на вход
    queryParams: { returnUrl: state.url },                     // 15 — запоминаем, куда шёл
  });
};
```

`state.url` — исходный адрес, например `/events/new`. После входа `LoginPage`
вернёт туда же.

---

## `core/event.service.ts` — работа с API

```ts
import { inject, Injectable, Signal } from '@angular/core';            // 1
import { HttpClient, httpResource } from '@angular/common/http';       // 2
import { EventDetails, EventListItem, EventPayload, EventQuery,
         PagedResult, PosterUploaded, Venue } from './models';         // 3–6

@Injectable({ providedIn: 'root' })
export class EventService {
  private readonly http = inject(HttpClient);                          // 10

  /** Список: httpResource перезапрашивается при изменении query. */
  listResource(query: Signal<EventQuery>) {                            // 13
    return httpResource<PagedResult<EventListItem>>(() => {            // 14 — реактивный GET
      const value = query();                                           // 15 — читаем сигнал → зависимость
      const params: Record<string, string | number | boolean> = {      // 16
        page: value.page,                                              // 17
        pageSize: value.pageSize,                                      // 18
        sort: value.sort,                                              // 19
        order: value.order,                                            // 20
      };

      if (value.venueId !== null) {                                    // 22 — не добавляем пустой фильтр
        params['venueId'] = value.venueId;                             // 23
      }
      if (value.free) {                                                // 25
        params['free'] = true;                                         // 26
      }
      if (value.date) {                                                // 28
        params['date'] = value.date;                                   // 29
      }

      return { url: '/api/events', params };                           // 32 — URL + query-параметры
    });
  }

  /** Одна запись: пока id не известен, запрос не выполняется. */
  detailResource(id: Signal<number | undefined>) {                     // 36
    return httpResource<EventDetails>(() => {                          // 37
      const value = id();                                              // 38
      return value ? { url: `/api/events/${value}` } : undefined;      // 39 — undefined = ресурс в idle
    });
  }

  venuesResource() {                                                   // 42
    return httpResource<Venue[]>(() => '/api/venues');                  // 43
  }

  create(payload: EventPayload) {                                      // 45
    return this.http.post<EventDetails>('/api/events', payload);        // 46
  }

  update(id: number, payload: EventPayload) {                          // 49
    return this.http.put<EventDetails>(`/api/events/${id}`, payload);   // 50
  }

  delete(id: number) {                                                 // 53
    return this.http.delete<void>(`/api/events/${id}`);                 // 54
  }

  uploadPoster(id: number, file: File) {                               // 56
    const formData = new FormData();                                   // 57 — multipart/form-data
    formData.append('file', file);                                     // 58 — имя поля должно совпадать с сервером
    return this.http.post<PosterUploaded>(`/api/events/${id}/poster`, formData); // 59
  }
}
```

`httpResource` сам:
- делает запрос при создании;
- следит за сигналами внутри функции (здесь `query()`);
- при изменении сигнала отменяет старый запрос и делает новый;
- отдаёт `value()`, `isLoading()`, `error()`, `statusCode()`, `reload()`.

Именно поэтому при смене фильтра список обновляется без ручного `subscribe`.

---

## `core/validators.ts` — проверка «не только пробелы»

```ts
import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/** Запрещает значения, состоящие только из пробелов. */
export const nonBlankValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => { // 5
  const value = control.value;                                      // 6
  if (typeof value !== 'string' || value.length === 0) {            // 7 — не строка или пусто: пусть решает required
    return null;
  }
  return value.trim().length > 0 ? null : { blank: true };          // 10 — только пробелы → ошибка blank
};
```

Ошибка `{ blank: true }` потом превращается в сообщение в `fieldError()`
формы.

---

## Что спросить у себя после этого раздела

1. Чем `signal` отличается от обычной переменной?
2. Зачем `computed`, если можно посчитать в шаблоне?
3. Почему интерсептор не внедряет `AuthService`?
4. Как guard возвращает редирект?
5. Что произойдёт с `httpResource`, если изменить сигнал `query`?
6. Почему `detailResource` возвращает `undefined`, пока нет id?
7. Зачем `withComponentInputBinding()`?
8. Почему `request.clone()`, а не изменение `request`?


---

## Дополнение: `adminGuard` для ролей

С появлением ролей формы создания/редактирования защищены не `authGuard`, а
`adminGuard`:

```ts
export const adminGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!auth.isAuthenticated()) {
    return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
  }

  if (auth.isAdmin()) {
    return true;
  }

  return router.createUrlTree(['/events']);
};
```

Обычный пользователь, открывший `/events/new` или `/events/:id/edit`, просто
возвращается на список. Подробнее — в [09-roles.md](09-roles.md).
