import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { environment } from '../../../environments/environment';
import { TokenBundle, tokenStorage } from './token-storage';

interface TelegramWidgetPayload {
  id: number;
  first_name: string;
  last_name?: string;
  username?: string;
  photo_url?: string;
  auth_date: number;
  hash: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly _tokens = signal<TokenBundle | null>(tokenStorage.read());

  readonly isAuthenticated = computed(() => this._tokens() !== null);
  readonly accessToken = computed(() => this._tokens()?.accessToken ?? null);

  async startEmail(email: string): Promise<void> {
    await firstValueFrom(
      this.http.post(`${environment.apiBase}/api/v1/auth/email/start`, { email }),
    );
  }

  async confirmEmail(email: string, code: string): Promise<void> {
    const bundle = await firstValueFrom(
      this.http.post<TokenBundle>(
        `${environment.apiBase}/api/v1/auth/email/confirm`,
        { email, code },
      ),
    );
    this.applyTokens(bundle);
  }

  async authenticateMiniApp(initData: string): Promise<void> {
    const bundle = await firstValueFrom(
      this.http.post<TokenBundle>(
        `${environment.apiBase}/api/v1/auth/telegram-miniapp`,
        { initData },
      ),
    );
    this.applyTokens(bundle);
  }

  async authenticateTelegram(payload: TelegramWidgetPayload): Promise<void> {
    const body: Record<string, string> = {
      id: String(payload.id),
      first_name: payload.first_name,
      auth_date: String(payload.auth_date),
      hash: payload.hash,
    };
    if (payload.last_name) body['last_name'] = payload.last_name;
    if (payload.username) body['username'] = payload.username;
    if (payload.photo_url) body['photo_url'] = payload.photo_url;

    const bundle = await firstValueFrom(
      this.http.post<TokenBundle>(
        `${environment.apiBase}/api/v1/auth/telegram-widget`,
        body,
      ),
    );
    this.applyTokens(bundle);
  }

  logout(): void {
    tokenStorage.clear();
    this._tokens.set(null);
    void this.router.navigate(['/login']);
  }

  /**
   * Navigates the whole window (NOT an XHR) to the OAuth /start endpoint so
   * the browser follows the cross-site redirect chain. The backend will
   * redirect back to `/auth/callback#access=…&refresh=…` once the provider
   * approves the user.
   */
  startOAuth(provider: 'google' | 'github'): void {
    const returnUrl = `${window.location.origin}/auth/callback`;
    const url = new URL(`${environment.apiBase}/api/v1/auth/${provider}/start`);
    url.searchParams.set('returnUrl', returnUrl);
    window.location.assign(url.toString());
  }

  /** Accepts a token bundle delivered via the OAuth redirect fragment. */
  applyExternalTokens(bundle: TokenBundle): void {
    this.applyTokens(bundle);
  }

  private applyTokens(b: TokenBundle): void {
    tokenStorage.write(b);
    this._tokens.set(b);
  }
}
