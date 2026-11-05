import { inject } from '@angular/core';
import { HttpInterceptorFn } from '@angular/common/http';
import { TokenStorage } from './token-storage';

/** Добавляет JWT к каждому запросу, если пользователь вошёл. */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const token = inject(TokenStorage).token();
  if (!token) {
    return next(request);
  }

  return next(
    request.clone({
      setHeaders: { Authorization: `Bearer ${token}` },
    }),
  );
};
