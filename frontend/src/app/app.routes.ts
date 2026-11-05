import { Routes } from '@angular/router';
import { adminGuard, authGuard } from './core/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'events' },
  {
    path: 'events',
    title: 'Афиша — события',
    loadComponent: () =>
      import('./pages/event-list-page/event-list-page').then((m) => m.EventListPage),
  },
  {
    path: 'events/new',
    title: 'Новое мероприятие',
    canActivate: [adminGuard],
    loadComponent: () =>
      import('./pages/event-form-page/event-form-page').then((m) => m.EventFormPage),
  },
  {
    path: 'events/:id',
    title: 'Мероприятие',
    loadComponent: () =>
      import('./pages/event-details-page/event-details-page').then((m) => m.EventDetailsPage),
  },
  {
    path: 'events/:id/edit',
    title: 'Редактирование мероприятия',
    canActivate: [adminGuard],
    loadComponent: () =>
      import('./pages/event-form-page/event-form-page').then((m) => m.EventFormPage),
  },
  {
    path: 'my-bookings',
    title: 'Мои брони',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./pages/my-bookings-page/my-bookings-page').then((m) => m.MyBookingsPage),
  },
  {
    path: 'login',
    title: 'Вход',
    loadComponent: () => import('./pages/login-page/login-page').then((m) => m.LoginPage),
  },
  {
    path: 'register',
    title: 'Регистрация',
    loadComponent: () => import('./pages/register-page/register-page').then((m) => m.RegisterPage),
  },
  {
    path: '404',
    title: 'Страница не найдена',
    loadComponent: () =>
      import('./pages/not-found-page/not-found-page').then((m) => m.NotFoundPage),
  },
  {
    path: '**',
    title: 'Страница не найдена',
    loadComponent: () =>
      import('./pages/not-found-page/not-found-page').then((m) => m.NotFoundPage),
  },
];
