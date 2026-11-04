import { Injectable } from '@angular/core';
import { User } from './models';

const TOKEN_KEY = 'afisha.token';
const USER_KEY = 'afisha.user';

/** Токен и пользователь в localStorage. Отдельный сервис, чтобы интерсептор не зависел от AuthService. */
@Injectable({ providedIn: 'root' })
export class TokenStorage {
  token(): string | null {
    return this.read(TOKEN_KEY);
  }

  user(): User | null {
    const raw = this.read(USER_KEY);
    if (!raw) {
      return null;
    }

    try {
      return JSON.parse(raw) as User;
    } catch {
      return null;
    }
  }

  save(token: string, user: User): void {
    this.write(TOKEN_KEY, token);
    this.write(USER_KEY, JSON.stringify(user));
  }

  clear(): void {
    try {
      localStorage.removeItem(TOKEN_KEY);
      localStorage.removeItem(USER_KEY);
    } catch {
      // localStorage может быть недоступен — молча игнорируем.
    }
  }

  private read(key: string): string | null {
    try {
      return localStorage.getItem(key);
    } catch {
      return null;
    }
  }

  private write(key: string, value: string): void {
    try {
      localStorage.setItem(key, value);
    } catch {
      // ignore
    }
  }
}
