import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

import { AuthService } from '../../../core/auth/auth.service';
import { IconComponent } from '../../../core/ui/icon/icon.component';

@Component({
  selector: 'app-oauth-callback',
  standalone: true,
  imports: [RouterLink, IconComponent],
  templateUrl: './oauth-callback.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
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
