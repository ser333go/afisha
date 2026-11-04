import { HttpErrorResponse } from '@angular/common/http';

export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}

/** Достаёт ProblemDetails из ошибки HttpClient (в том числе объект errors). */
export function problemFrom(error: unknown): ProblemDetails | null {
  if (error instanceof HttpErrorResponse) {
    const payload: unknown = error.error;
    if (payload && typeof payload === 'object') {
      return payload as ProblemDetails;
    }
  }

  if (error && typeof error === 'object' && 'error' in error) {
    const payload = (error as { error: unknown }).error;
    if (payload && typeof payload === 'object') {
      return payload as ProblemDetails;
    }
  }

  return null;
}

export function errorText(error: unknown): string {
  const problem = problemFrom(error);
  if (problem?.detail) {
    return problem.detail;
  }
  if (problem?.title) {
    return problem.title;
  }

  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) {
      return 'Сервер недоступен. Проверьте, что API запущен.';
    }
    return `Ошибка запроса (${error.status}).`;
  }

  return 'Не удалось выполнить запрос. Попробуйте ещё раз.';
}

export function fieldErrors(problem: ProblemDetails | null): Record<string, string[]> {
  return problem?.errors ?? {};
}
