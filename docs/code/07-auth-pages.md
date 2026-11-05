# 07. Клиент: вход, регистрация и 404

---

## `pages/login-page` — вход

### `login-page.ts`

```ts
import { Component, inject, input, signal } from '@angular/core';        // 1
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms'; // 2
import { Router, RouterLink } from '@angular/router';                    // 3
import { AuthService } from '../../core/auth.service';                   // 4
import { errorText, problemFrom } from '../../core/api-error';           // 5

@Component({
  selector: 'app-login-page',
  imports: [ReactiveFormsModule, RouterLink],                            // 9
  templateUrl: './login-page.html',
  styleUrl: './login-page.css',
})
export class LoginPage {
  private readonly auth = inject(AuthService);                           // 14
  private readonly router = inject(Router);                              // 15
  private readonly fb = inject(FormBuilder);                             // 16

  readonly returnUrl = input<string>('/events');                         // 18 — куда вернуться после входа

  readonly form = this.fb.nonNullable.group({                            // 20 — форма
    email: ['', [Validators.required, Validators.email]],                // 21
    password: ['', [Validators.required]],                               // 22
  });

  readonly submitting = signal(false);                                   // 25 — идёт запрос
  readonly submitted = signal(false);                                    // 26 — была попытка
  readonly error = signal<string | null>(null);                          // 27 — ошибка 401

  submit(): void {
    this.submitted.set(true);                                            // 30
    this.error.set(null);                                                // 31
    this.form.markAllAsTouched();                                        // 32 — показать ошибки полей

    if (this.form.invalid) {                                             // 34 — клиентская проверка
      return;
    }

    this.submitting.set(true);                                           // 38
    this.auth.login(this.form.getRawValue()).subscribe({                 // 39 — POST /api/auth/login
      next: () => {
        this.submitting.set(false);                                      // 41
        void this.router.navigateByUrl(this.returnUrl());                // 42 — returnUrl или /events
      },
      error: (error: unknown) => {                                       // 44
        this.submitting.set(false);
        const problem = problemFrom(error);                              // 46
        this.error.set(problem?.detail ?? problem?.title ?? errorText(error)); // 47 — текст 401
      },
    });
  }

  fieldError(name: string): string | null {                              // 52
    const control = this.form.get(name);
    if (!control || (!control.touched && !this.submitted())) {           // 54
      return null;
    }
    if (control.errors?.['required']) {                                  // 57
      return 'Поле обязательно.';
    }
    if (control.errors?.['email']) {                                     // 60
      return 'Введите корректный email.';
    }
    return null;
  }

  fillDemo(): void {                                                     // 66 — кнопка «Подставить данные»
    this.form.setValue({ email: 'demo@afisha.local', password: 'Demo12345' }); // 67
  }
}
```

`returnUrl` приходит из query-параметра, который выставил guard:
`/login?returnUrl=%2Fevents%2Fnew`. `withComponentInputBinding()` связывает его
с `input()`.

### `login-page.html` — ключевые места

```html
<section class="auth">                                        <!-- 1 -->
  <div class="auth__panel panel">                             <!-- 2 -->
    <p class="eyebrow">Вход</p>                               <!-- 3 -->
    <h1>С возвращением</h1>                                   <!-- 4 -->
    <p class="lede">Войдите, чтобы добавлять мероприятия и загружать постеры.</p> <!-- 5 -->

    @if (error()) {                                           <!-- 7 — ошибка 401 -->
      <div class="alert alert--error" role="alert">{{ error() }}</div>
    }

    <form class="auth__form" [formGroup]="form" (ngSubmit)="submit()" novalidate> <!-- 11 -->
      <div class="field">
        <label for="email">Email</label>
        <input id="email" type="email" autocomplete="email"   <!-- 14 -->
               formControlName="email" [class.invalid]="fieldError('email')" /> <!-- 15 -->
        @if (fieldError('email')) {                           <!-- 16 -->
          <p class="field__error">{{ fieldError('email') }}</p>
        }
      </div>

      <div class="field">
        <label for="password">Пароль</label>
        <input id="password" type="password" autocomplete="current-password" <!-- 22 -->
               formControlName="password" [class.invalid]="fieldError('password')" />
        @if (fieldError('password')) {
          <p class="field__error">{{ fieldError('password') }}</p>
        }
      </div>

      <button type="submit" class="btn btn--primary btn--block" [disabled]="submitting()"> <!-- 29 -->
        {{ submitting() ? 'Входим…' : 'Войти' }}
      </button>
    </form>

    <div class="auth__demo">                                  <!-- 34 — подсказка для проверяющего -->
      <p><strong>Тестовый пользователь</strong></p>
      <p>demo&#64;afisha.local / Demo12345</p>                 <!-- 36 — &#64; = @ -->
      <button type="button" class="btn btn--ghost btn--small" (click)="fillDemo()">Подставить данные</button>
    </div>

    <p class="auth__switch">Нет аккаунта? <a routerLink="/register">Зарегистрироваться</a></p> <!-- 41 -->
  </div>
</section>
```

---

## `pages/register-page` — регистрация

### `register-page.ts`

```ts
import { Component, inject, signal } from '@angular/core';               // 1
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors,
         ValidatorFn, Validators } from '@angular/forms';                 // 2
import { Router, RouterLink } from '@angular/router';                    // 3
import { AuthService } from '../../core/auth.service';                   // 4
import { errorText, problemFrom } from '../../core/api-error';           // 5
import { nonBlankValidator } from '../../core/validators';               // 6

const passwordContentValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => { // 8
  const value = control.value as string;                                 // 9
  if (!value) {                                                          // 10
    return null;                                                         // 11 — пусто: сработает required
  }
  const hasLetter = /[A-Za-zА-Яа-яЁё]/.test(value);                      // 13 — есть буква (лат/кир)
  const hasDigit = /\d/.test(value);                                     // 14 — есть цифра
  return hasLetter && hasDigit ? null : { passwordContent: true };       // 15 — иначе ошибка
};

@Component({
  selector: 'app-register-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './register-page.html',
  styleUrl: './register-page.css',
})
export class RegisterPage {
  private readonly auth = inject(AuthService);                           // 25
  private readonly router = inject(Router);                              // 26
  private readonly fb = inject(FormBuilder);                             // 27

  readonly form = this.fb.nonNullable.group({                            // 29
    displayName: ['', [Validators.required, nonBlankValidator, Validators.minLength(2), Validators.maxLength(100)]], // 30
    email: ['', [Validators.required, Validators.email, Validators.maxLength(254)]], // 31
    password: [                                                          // 32
      '',
      [Validators.required, Validators.minLength(8), Validators.maxLength(72), passwordContentValidator], // 34
    ],
  });

  readonly submitting = signal(false);                                   // 38
  readonly submitted = signal(false);                                    // 39
  readonly generalError = signal<string | null>(null);                   // 40

  submit(): void {
    this.submitted.set(true);                                            // 43
    this.generalError.set(null);                                         // 44
    this.form.markAllAsTouched();                                        // 45

    if (this.form.invalid) {                                             // 47
      return;
    }

    this.submitting.set(true);                                           // 51
    const raw = this.form.getRawValue();                                 // 52
    this.auth
      .register({
        displayName: raw.displayName.trim(),                             // 55 — без пробелов по краям
        email: raw.email.trim().toLowerCase(),                           // 56 — нормализация email
        password: raw.password,                                          // 57
      })
      .subscribe({
        next: () => {
          this.submitting.set(false);                                    // 61
          void this.router.navigate(['/events']);                        // 62 — сразу на афишу
        },
        error: (error: unknown) => {
          this.submitting.set(false);
          const problem = problemFrom(error);                            // 66
          if (problem?.errors) {                                         // 67 — ошибки по полям
            for (const [field, messages] of Object.entries(problem.errors)) { // 68
              const control = this.form.get(field);                      // 69
              if (control) {
                control.setErrors({ server: messages.join(' ') });       // 71 — ошибка под полем
                control.markAsTouched();                                  // 72
              }
            }
            return;
          }
          this.generalError.set(problem?.detail ?? problem?.title ?? errorText(error)); // 77
        },
      });
  }

  fieldError(name: string): string | null {                              // 82
    const control = this.form.get(name);
    if (!control || (!control.touched && !this.submitted())) {           // 84
      return null;
    }

    const errors = control.errors;
    if (!errors) {
      return null;
    }
    if (errors['server']) {                                              // 92 — серверная ошибка
      return String(errors['server']);
    }
    if (errors['required']) {
      return 'Поле обязательно.';
    }
    if (errors['blank']) {                                               // 98
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
    if (errors['passwordContent']) {                                     // 110
      return 'Пароль должен содержать хотя бы одну букву и одну цифру.';
    }
    return null;
  }
}
```

Ошибка сервера «email уже занят» приходит как
`errors: { email: ["Пользователь с таким email уже зарегистрирован."] }` и
попадает ровно под поле email — это требование варианта.

### `register-page.html` — ключевые места

```html
<section class="auth">                                          <!-- 1 -->
  <div class="auth__panel panel">
    <p class="eyebrow">Регистрация</p>                          <!-- 3 -->
    <h1>Создать аккаунт</h1>                                    <!-- 4 -->
    <p class="lede">Аккаунт нужен, чтобы публиковать мероприятия и загружать постеры.</p>

    @if (generalError()) {                                      <!-- 7 -->
      <div class="alert alert--error" role="alert">{{ generalError() }}</div>
    }

    <form class="auth__form" [formGroup]="form" (ngSubmit)="submit()" novalidate> <!-- 11 -->
      <div class="field">
        <label for="displayName">Имя</label>
        <input id="displayName" type="text" autocomplete="name" formControlName="displayName"
               [class.invalid]="fieldError('displayName')" />    <!-- 15 -->
        @if (fieldError('displayName')) {
          <p class="field__error">{{ fieldError('displayName') }}</p>
        }
      </div>

      <div class="field">
        <label for="email">Email</label>
        <input id="email" type="email" autocomplete="email" formControlName="email"
               [class.invalid]="fieldError('email')" />          <!-- 23 -->
        @if (fieldError('email')) {
          <p class="field__error">{{ fieldError('email') }}</p>
        }
      </div>

      <div class="field">
        <label for="password">Пароль</label>
        <input id="password" type="password" autocomplete="new-password" formControlName="password"
               placeholder="Минимум 8 символов, буква и цифра"  <!-- 31 -->
               [class.invalid]="fieldError('password')" />
        @if (fieldError('password')) {
          <p class="field__error">{{ fieldError('password') }}</p>
        }
      </div>

      <button type="submit" class="btn btn--primary btn--block" [disabled]="submitting()"> <!-- 38 -->
        {{ submitting() ? 'Создаём…' : 'Зарегистрироваться' }}
      </button>
    </form>

    <p class="auth__switch">Уже есть аккаунт? <a routerLink="/login">Войти</a></p> <!-- 43 -->
  </div>
</section>
```

---

## `pages/not-found-page` — 404

### `not-found-page.ts`

```ts
import { Component } from '@angular/core';          // 1
import { RouterLink } from '@angular/router';       // 2

@Component({
  selector: 'app-not-found-page',
  imports: [RouterLink],                            // 7
  templateUrl: './not-found-page.html',
  styleUrl: './not-found-page.css',
})
export class NotFoundPage {}                        // 11 — логики нет, только шаблон
```

### `not-found-page.html`

```html
<section class="not-found">                         <!-- 1 -->
  <p class="not-found__code">404</p>                <!-- 2 -->
  <h1>Страница не найдена</h1>                      <!-- 3 -->
  <p class="lede">Такой страницы нет — возможно, ссылка устарела или в адресе опечатка.</p>
  <a class="btn btn--primary" routerLink="/events">Вернуться в афишу</a> <!-- 5 -->
</section>
```

Страница используется дважды: маршрутом `404` и wildcard-маршрутом `**`.

---

## Как связаны вход, guard и returnUrl

1. Гость открывает `/events/new`.
2. `authGuard` видит `isAuthenticated() === false` и возвращает
   `router.createUrlTree(['/login'], { queryParams: { returnUrl: '/events/new' } })`.
3. `LoginPage` получает `returnUrl` через `input()` и после успешного входа вызывает
   `router.navigateByUrl('/events/new')`.
4. Только теперь форма открывается.

---

## Что спросить у себя после этого раздела

1. Откуда `LoginPage` знает `returnUrl`?
2. Почему после регистрации сразу выполняется вход?
3. Где хранится токен и почему не в памяти?
4. Что будет, если ввести неверный пароль?
5. Почему email приводится к нижнему регистру и на клиенте, и на сервере?
6. Чем `navigate` отличается от `navigateByUrl`?
7. Почему 404-страница не имеет своей логики?
