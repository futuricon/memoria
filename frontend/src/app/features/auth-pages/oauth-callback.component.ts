import { Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { IconComponent } from '../../core/ui/icon.component';

@Component({
  selector: 'app-oauth-callback',
  standalone: true,
  imports: [RouterLink, IconComponent],
  template: `
    <div class="min-h-screen bg-page flex items-center justify-center px-4">
      <div class="w-full max-w-md bg-surface border border-default rounded-xl shadow-card p-8 text-center">
        @if (state() === 'pending') {
          <div class="inline-flex w-12 h-12 rounded-full items-center justify-center mb-3"
               [style.background]="'color-mix(in srgb, var(--color-brand-500) 14%, transparent)'">
            <app-icon name="loader" [size]="22" class="text-brand animate-spin" />
          </div>
          <p class="text-sm text-fg-secondary">Completing sign-in…</p>
        } @else if (state() === 'success') {
          <div class="inline-flex w-12 h-12 rounded-full items-center justify-center mb-3"
               [style.background]="'color-mix(in srgb, var(--color-brand-500) 14%, transparent)'">
            <app-icon name="check-circle" [size]="22" class="text-brand" />
          </div>
          <p class="text-sm text-fg-secondary">Signed in. Redirecting…</p>
        } @else {
          <div class="inline-flex w-12 h-12 rounded-full items-center justify-center mb-3"
               [style.background]="'color-mix(in srgb, var(--color-rating-again) 14%, transparent)'">
            <app-icon name="x-circle" [size]="22" [style.color]="'var(--color-rating-again)'" />
          </div>
          <h1 class="text-lg font-semibold text-fg mb-2">Sign-in failed</h1>
          <p class="text-sm text-fg-secondary mb-4">{{ message() }}</p>
          <a
            routerLink="/login"
            class="inline-flex items-center gap-1.5 h-9 px-4 rounded-md text-sm border border-default text-fg hover:bg-surface-hover"
          >
            <app-icon name="arrow-left" [size]="14" />
            Back to login
          </a>
        }
      </div>
    </div>
  `,
})
export class OAuthCallbackComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly state = signal<'pending' | 'success' | 'error'>('pending');
  readonly message = signal<string>('');

  ngOnInit(): void {
    const fragment = window.location.hash.startsWith('#')
      ? window.location.hash.slice(1)
      : window.location.hash;

    if (!fragment) {
      this.failWith('No data returned from the provider.');
      return;
    }

    const params = new URLSearchParams(fragment);
    const error = params.get('error');
    if (error) {
      this.failWith(this.describeError(error));
      return;
    }

    const access = params.get('access');
    const refresh = params.get('refresh');
    const accessExpires = params.get('accessExpires');
    if (!access || !refresh || !accessExpires) {
      this.failWith('Provider response was incomplete.');
      return;
    }

    this.auth.applyExternalTokens({
      accessToken: access,
      refreshToken: refresh,
      expiresAt: accessExpires,
    });

    history.replaceState(null, '', window.location.pathname);

    this.state.set('success');
    void this.router.navigate(['/']);
  }

  private failWith(msg: string): void {
    this.state.set('error');
    this.message.set(msg);
    history.replaceState(null, '', window.location.pathname);
  }

  private describeError(code: string): string {
    switch (code) {
      case 'missing_id':
        return 'Provider did not return a stable user id.';
      case 'users.oauth_provider_unknown':
        return 'Unsupported OAuth provider.';
      default:
        return `Sign-in failed (${code}).`;
    }
  }
}
