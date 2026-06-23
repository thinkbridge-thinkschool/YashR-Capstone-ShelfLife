import { Injectable, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { LoginRequest, LoginResponse, RegisterRequest } from '../models/auth.models';

interface JwtPayload {
  sub: string;
  email: string;
  role: string;
  exp: number;
}

interface CurrentUser {
  id: string;
  email: string;
  role: string;
  displayName: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly tokenKey = 'shelflife_token';
  private readonly base = `${environment.apiBaseUrl}/api/v1/identity`;

  private readonly _token = signal<string | null>(
    typeof localStorage !== 'undefined' ? localStorage.getItem(this.tokenKey) : null
  );

  readonly isLoggedIn = computed(() => {
    const token = this._token();
    if (!token) return false;
    const payload = this.decode(token);
    return payload !== null && payload.exp * 1000 > Date.now();
  });

  readonly currentUser = computed((): CurrentUser | null => {
    const token = this._token();
    if (!token) return null;
    const p = this.decode(token);
    if (!p) return null;
    return {
      id: p.sub,
      email: p.email,
      role: p.role,
      displayName: p.email.split('@')[0],
    };
  });

  constructor(private readonly http: HttpClient) {}

  login(req: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.base}/login`, req).pipe(
      tap(res => {
        localStorage.setItem(this.tokenKey, res.token);
        this._token.set(res.token);
      })
    );
  }

  register(req: RegisterRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.base}/register`, req);
  }

  logout(): void {
    localStorage.removeItem(this.tokenKey);
    this._token.set(null);
  }

  getToken(): string | null {
    return this._token();
  }

  isLibrarian(): boolean {
    return this.currentUser()?.role === 'Librarian';
  }

  private decode(token: string): JwtPayload | null {
    try {
      return JSON.parse(atob(token.split('.')[1])) as JwtPayload;
    } catch {
      return null;
    }
  }
}
