import { Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-oauth-callback',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="min-h-screen flex items-center justify-center px-4">
      <div class="w-full max-w-md bg-white border border-slate-200 rounded-xl shadow-sm p-8 text-center">
        @if (state() === 'pending') {
          <p class="text-sm text-slate-500">Completing sign-in…</p>
        } @else if (state() === 'success') {
          <p class="text-sm text-slate-500">Signed in. Redirecting…</p>
        } @else {
          <h1 class="text-lg font-semibold text-rose-700 mb-2">Sign-in failed</h1>
          <p class="text-sm text-slate-600 mb-4">{{ message() }}</p>
          <a
            routerLink="/login"
            class="inline-block px-3 py-1.5 text-sm rounded border border-slate-300 hover:bg-slate-100"
          >Back to login</a>
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
    // Tokens arrive in the URL fragment so they never hit access logs along
    // the way. Parse, store, then strip the fragment from the browser bar.
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

    // Scrub the fragment so the tokens don't linger in the URL bar / history.
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
