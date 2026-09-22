import { HttpParams } from '@angular/common/http';

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export function emptyPage<T>(): PagedResult<T> {
  return { items: [], page: 1, pageSize: 20, totalCount: 0 };
}

type QueryValue = string | number | boolean | null | undefined;

/** Builds query parameters, skipping empty values so the API applies its defaults. */
export function toHttpParams(values: object): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries(values) as [string, QueryValue][]) {
    if (value !== null && value !== undefined && value !== '') {
      params = params.set(key, String(value));
    }
  }
  return params;
}
