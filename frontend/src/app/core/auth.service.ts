import { computed, inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { tap } from 'rxjs';
import { AuthResponse, LoginPayload, RegisterPayload, User } from './models';
import { TokenStorage } from './token-storage';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly storage = inject(TokenStorage);
  private readonly router = inject(Router);

  private readonly tokenSignal = signal<string | null>(this.storage.token());
  private readonly userSignal = signal<User | null>(this.storage.user());

  readonly token = this.tokenSignal.asReadonly();
  readonly currentUser = this.userSignal.asReadonly();
  readonly isAuthenticated = computed(() => this.tokenSignal() !== null);
  readonly currentUserId = computed(() => this.userSignal()?.id ?? null);
  readonly isAdmin = computed(() => this.userSignal()?.role === 'Admin');

  login(payload: LoginPayload) {
    return this.http
      .post<AuthResponse>('/api/auth/login', payload)
      .pipe(tap((response) => this.apply(response)));
  }

  register(payload: RegisterPayload) {
    return this.http
      .post<AuthResponse>('/api/auth/register', payload)
      .pipe(tap((response) => this.apply(response)));
  }

  logout(): void {
    this.storage.clear();
    this.tokenSignal.set(null);
    this.userSignal.set(null);
    void this.router.navigate(['/events']);
  }

  private apply(response: AuthResponse): void {
    this.storage.save(response.token, response.user);
    this.tokenSignal.set(response.token);
    this.userSignal.set(response.user);
  }
}
