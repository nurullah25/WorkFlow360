import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { authInterceptor } from './auth.interceptor';
import { AuthResponse } from './auth.models';
import { AuthService } from './auth.service';

function authResponse(accessToken: string): AuthResponse {
  return {
    accessToken,
    expiresAt: new Date(Date.now() + 15 * 60_000).toISOString(),
    user: { id: 1, email: 'rafiq@test.local', fullName: 'Rafiq Hasan', role: 'Employee', employeeId: 4 },
  };
}

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let auth: AuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: '**', children: [] }]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
  });

  afterEach(() => httpMock.verify());

  function signIn(token: string): void {
    auth.login('rafiq@test.local', 'secret').subscribe();
    httpMock.expectOne('/api/auth/login').flush(authResponse(token));
  }

  it('adds the bearer token to API requests', () => {
    signIn('token-1');

    http.get('/api/employees').subscribe();

    const request = httpMock.expectOne('/api/employees');
    expect(request.request.headers.get('Authorization')).toBe('Bearer token-1');
    request.flush([]);
  });

  it('does not send the token to other hosts', () => {
    signIn('token-1');

    http.get('https://example.com/data').subscribe();

    const request = httpMock.expectOne('https://example.com/data');
    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush({});
  });

  it('refreshes the session and retries once when the token has expired', () => {
    signIn('expired-token');
    let result: unknown;

    http.get('/api/employees').subscribe((body) => (result = body));
    httpMock.expectOne('/api/employees').flush(null, { status: 401, statusText: 'Unauthorized' });
    httpMock.expectOne('/api/auth/refresh').flush(authResponse('fresh-token'));

    const retry = httpMock.expectOne('/api/employees');
    expect(retry.request.headers.get('Authorization')).toBe('Bearer fresh-token');
    retry.flush(['ok']);

    expect(result).toEqual(['ok']);
  });

  it('uses a single refresh call for parallel requests that fail with 401', () => {
    signIn('expired-token');

    http.get('/api/projects').subscribe();
    http.get('/api/tasks').subscribe();
    httpMock.expectOne('/api/projects').flush(null, { status: 401, statusText: 'Unauthorized' });
    httpMock.expectOne('/api/tasks').flush(null, { status: 401, statusText: 'Unauthorized' });

    httpMock.expectOne('/api/auth/refresh').flush(authResponse('fresh-token'));

    httpMock.expectOne('/api/projects').flush([]);
    httpMock.expectOne('/api/tasks').flush([]);
  });

  it('signs the user out when the refresh fails', () => {
    signIn('expired-token');
    let failedStatus: number | undefined;

    http.get('/api/employees').subscribe({ error: (error) => (failedStatus = error.status) });
    httpMock.expectOne('/api/employees').flush(null, { status: 401, statusText: 'Unauthorized' });
    httpMock.expectOne('/api/auth/refresh').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(failedStatus).toBe(401);
    expect(auth.isAuthenticated()).toBe(false);
  });
});
