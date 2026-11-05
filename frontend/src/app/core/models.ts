export interface Venue {
  id: number;
  name: string;
  address: string;
  city: string;
}

export interface EventListItem {
  id: number;
  title: string;
  startsAt: string;
  price: number;
  seats: number;
  venue: Venue;
  posterUrl: string | null;
  authorId: number;
}

export interface EventDetails extends EventListItem {
  description: string | null;
  authorName: string;
  createdAt: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
}

export interface EventPayload {
  title: string;
  description: string | null;
  startsAt: string;
  price: number;
  seats: number;
  venueId: number;
}

export type SortField = 'date' | 'price';
export type SortOrder = 'asc' | 'desc';

export interface EventQuery {
  page: number;
  pageSize: number;
  venueId: number | null;
  free: boolean;
  date: string | null;
  sort: SortField;
  order: SortOrder;
}

export interface FilterState {
  venueId: number | null;
  free: boolean;
  date: string | null;
  sort: SortField;
  order: SortOrder;
}

export interface User {
  id: number;
  email: string;
  displayName: string;
  role: 'Admin' | 'User';
}

export interface AuthResponse {
  token: string;
  user: User;
}

export interface LoginPayload {
  email: string;
  password: string;
}

export interface RegisterPayload {
  email: string;
  displayName: string;
  password: string;
}

export interface PosterUploaded {
  posterUrl: string;
}
