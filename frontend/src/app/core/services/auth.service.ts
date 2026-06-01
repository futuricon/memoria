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

/** Shape returned by every backend endpoint that issues a JWT pair. */
interface JwtTokenPairWire {
  readonly accessToken: string;
  readonly accessExpiresAt: string;
  readonly refreshToken: string;
  readonly refreshExpiresAt: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly _tokens = signal<TokenBundle | null>(tokenStorage.read());
  /** In-flight refresh, shared across concurrent 401 retries. */
  private refreshInFlight: Promise<string | null> | null = null;

  readonly isAuthenticated = computed(() => this._tokens() !== null);
  readonly accessToken = computed(() => this._tokens()?.accessToken ?? null);

  async startEmail(email: string): Promise<void> {
    await firstValueFrom(
      this.http.post(`${environment.apiBase}/api/v1/auth/email/start`, { email }),
    );
  }

  async confirmEmail(email: string, code: string): Promise<void> {
    const pair = await firstValueFrom(
      this.http.post<JwtTokenPairWire>(
        `${environment.apiBase}/api/v1/auth/email/confirm`,
        { email, code },
      ),
    );
    this.applyTokens(this.mapPair(pair));
  }

  async authenticateMiniApp(initData: string): Promise<void> {
    const pair = await firstValueFrom(
      this.http.post<JwtTokenPairWire>(
        `${environment.apiBase}/api/v1/auth/telegram-miniapp`,
        { initData },
      ),
    );
    this.applyTokens(this.mapPair(pair));
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

    const pair = await firstValueFrom(
      this.http.post<JwtTokenPairWire>(
        `${environment.apiBase}/api/v1/auth/telegram-widget`,
        body,
      ),
    );
    this.applyTokens(this.mapPair(pair));
  }

  /**
   * Exchanges the current refresh token for a fresh JWT pair. Concurrent
   * callers (multiple requests all hitting 401 at once) share a single
   * in-flight refresh promise so we don't burn through a chain of
   * one-shot refresh tokens. Resolves to the new access token, or null
   * when refresh fails (expired / revoked / no token at all).
   */
  refresh(): Promise<string | null> {
    if (this.refreshInFlight) return this.refreshInFlight;

    const current = this._tokens();
    if (!current?.refreshToken) return Promise.resolve(null);

    this.refreshInFlight = firstValueFrom(
      this.http.post<JwtTokenPairWire>(
        `${environment.apiBase}/api/v1/auth/refresh`,
        { refreshToken: current.refreshToken },
      ),
    )
      .then((pair) => {
        const bundle = this.mapPair(pair);
        this.applyTokens(bundle);
        return bundle.accessToken;
      })
      .catch(() => null)
      .finally(() => {
        this.refreshInFlight = null;
      });

    return this.refreshInFlight;
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

  private mapPair(p: JwtTokenPairWire): TokenBundle {
    return {
      accessToken: p.accessToken,
      refreshToken: p.refreshToken,
      expiresAt: p.accessExpiresAt,
    };
  }

  private applyTokens(b: TokenBundle): void {
    tokenStorage.write(b);
    this._tokens.set(b);
  }
}
