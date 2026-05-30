import {
  AfterViewInit,
  Component,
  ElementRef,
  ViewChild,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';

import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/auth/auth.service';

type Tab = 'email' | 'telegram';
type EmailStep = 'request' | 'confirm';

interface TelegramWidgetUser {
  id: number;
  first_name: string;
  last_name?: string;
  username?: string;
  photo_url?: string;
  auth_date: number;
  hash: string;
}

declare global {
  interface Window {
    onTelegramAuth?: (user: TelegramWidgetUser) => void;
  }
}

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule],
  template: `
    <div class="min-h-screen flex items-center justify-center px-4">
      <div class="w-full max-w-md bg-white border border-slate-200 rounded-xl shadow-sm p-8">
        <h1 class="text-2xl font-semibold text-center mb-1">Memoria</h1>
        <p class="text-sm text-center text-slate-500 mb-6">
          Sign in to your spaced-repetition workspace
        </p>

        <div class="grid grid-cols-2 gap-2 mb-4">
          <button
            type="button"
            (click)="signInWith('google')"
            class="flex items-center justify-center gap-2 py-2 text-sm rounded border border-slate-300 bg-white hover:bg-slate-50"
          >
            <svg width="16" height="16" viewBox="0 0 48 48" aria-hidden="true">
              <path fill="#FFC107" d="M43.6 20.5H42V20H24v8h11.3C33.7 32.5 29.3 35 24 35c-6.6 0-12-5.4-12-12s5.4-12 12-12c3 0 5.7 1.1 7.8 2.9l5.7-5.7C33.6 5.1 29.1 3 24 3 12.4 3 3 12.4 3 24s9.4 21 21 21c11 0 20-8 20-21 0-1.3-.1-2.5-.4-3.5z"/>
              <path fill="#FF3D00" d="m6.3 14.7 6.6 4.8C14.6 16 18.9 13 24 13c3 0 5.7 1.1 7.8 2.9l5.7-5.7C33.6 7.1 29.1 5 24 5c-7.7 0-14.3 4.4-17.7 9.7z"/>
              <path fill="#4CAF50" d="M24 45c5 0 9.6-1.9 13-5l-6-5c-1.8 1.3-4.2 2-7 2-5.3 0-9.7-3.5-11.3-8.4L6 33.4C9.3 39.9 16.1 45 24 45z"/>
              <path fill="#1976D2" d="M43.6 20.5H42V20H24v8h11.3c-.8 2.2-2.2 4.1-4 5.5l6 5c-.4.4 6.7-4.9 6.7-14.5 0-1.3-.1-2.5-.4-3.5z"/>
            </svg>
            <span>Google</span>
          </button>
          <button
            type="button"
            (click)="signInWith('github')"
            class="flex items-center justify-center gap-2 py-2 text-sm rounded border border-slate-300 bg-white hover:bg-slate-50"
          >
            <svg width="16" height="16" viewBox="0 0 16 16" fill="#0f172a" aria-hidden="true">
              <path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2 .37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0 0 16 8c0-4.42-3.58-8-8-8z"/>
            </svg>
            <span>GitHub</span>
          </button>
        </div>

        <div class="relative my-4">
          <div class="absolute inset-0 flex items-center">
            <div class="w-full border-t border-slate-200"></div>
          </div>
          <div class="relative flex justify-center text-xs">
            <span class="px-2 bg-white text-slate-400">or</span>
          </div>
        </div>

        <div class="grid grid-cols-2 gap-1 p-1 bg-slate-100 rounded mb-6 text-sm">
          <button
            type="button"
            class="py-2 rounded transition"
            [class.bg-white]="tab() === 'email'"
            [class.shadow-sm]="tab() === 'email'"
            (click)="tab.set('email')"
          >Email</button>
          <button
            type="button"
            class="py-2 rounded transition"
            [class.bg-white]="tab() === 'telegram'"
            [class.shadow-sm]="tab() === 'telegram'"
            (click)="tab.set('telegram')"
          >Telegram</button>
        </div>

        @if (tab() === 'email') {
          @if (emailStep() === 'request') {
            <form (ngSubmit)="requestCode()" class="space-y-3">
              <label class="block text-sm">
                <span class="text-slate-600">Email</span>
                <input
                  type="email"
                  name="email"
                  [(ngModel)]="email"
                  required
                  class="mt-1 w-full px-3 py-2 border border-slate-300 rounded focus:outline-none focus:ring-2 focus:ring-slate-400"
                  placeholder="you@example.com"
                />
              </label>
              <button
                type="submit"
                [disabled]="busy()"
                class="w-full py-2 bg-slate-900 text-white rounded hover:bg-slate-800 disabled:opacity-50"
              >
                {{ busy() ? 'Sending…' : 'Send code' }}
              </button>
            </form>
          } @else {
            <form (ngSubmit)="confirmCode()" class="space-y-3">
              <p class="text-sm text-slate-600">
                Code sent to <span class="font-medium">{{ email }}</span>.
                Check your inbox.
              </p>
              <label class="block text-sm">
                <span class="text-slate-600">Verification code</span>
                <input
                  type="text"
                  name="code"
                  [(ngModel)]="code"
                  required
                  inputmode="numeric"
                  class="mt-1 w-full px-3 py-2 border border-slate-300 rounded focus:outline-none focus:ring-2 focus:ring-slate-400 tracking-widest text-center"
                  placeholder="123456"
                />
              </label>
              <button
                type="submit"
                [disabled]="busy()"
                class="w-full py-2 bg-slate-900 text-white rounded hover:bg-slate-800 disabled:opacity-50"
              >
                {{ busy() ? 'Verifying…' : 'Sign in' }}
              </button>
              <button
                type="button"
                (click)="emailStep.set('request')"
                class="w-full py-2 text-sm text-slate-500 hover:text-slate-700"
              >← Use a different email</button>
            </form>
          }
        } @else {
          <div class="flex flex-col items-center gap-3">
            <p class="text-sm text-slate-600 text-center">
              Authorize with the official Telegram Login widget. No password required.
            </p>
            <div #tgMount></div>
          </div>
        }

        @if (error()) {
          <p class="mt-4 text-sm text-red-600">{{ error() }}</p>
        }
      </div>
    </div>
  `,
})
export class LoginComponent implements AfterViewInit {
  @ViewChild('tgMount') tgMount?: ElementRef<HTMLElement>;

  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly tab = signal<Tab>('email');
  readonly emailStep = signal<EmailStep>('request');
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  email = '';
  code = '';

  ngAfterViewInit(): void {
    window.onTelegramAuth = (user) => {
      void this.handleTelegramAuth(user);
    };
    this.mountTelegramWidget();
  }

  signInWith(provider: 'google' | 'github'): void {
    this.auth.startOAuth(provider);
  }

  async requestCode(): Promise<void> {
    if (!this.email.trim()) return;
    this.error.set(null);
    this.busy.set(true);
    try {
      await this.auth.startEmail(this.email.trim());
      this.emailStep.set('confirm');
    } catch (e) {
      this.error.set(this.describe(e, 'Could not send verification code.'));
    } finally {
      this.busy.set(false);
    }
  }

  async confirmCode(): Promise<void> {
    if (!this.code.trim()) return;
    this.error.set(null);
    this.busy.set(true);
    try {
      await this.auth.confirmEmail(this.email.trim(), this.code.trim());
      void this.router.navigate(['/']);
    } catch (e) {
      this.error.set(this.describe(e, 'Invalid or expired code.'));
    } finally {
      this.busy.set(false);
    }
  }

  private async handleTelegramAuth(user: TelegramWidgetUser): Promise<void> {
    this.error.set(null);
    this.busy.set(true);
    try {
      await this.auth.authenticateTelegram(user);
      void this.router.navigate(['/']);
    } catch (e) {
      this.error.set(this.describe(e, 'Telegram sign-in failed.'));
    } finally {
      this.busy.set(false);
    }
  }

  private mountTelegramWidget(): void {
    if (!this.tgMount) return;
    const script = document.createElement('script');
    script.async = true;
    script.src = 'https://telegram.org/js/telegram-widget.js?22';
    script.setAttribute('data-telegram-login', environment.telegramBotUsername);
    script.setAttribute('data-size', 'large');
    script.setAttribute('data-onauth', 'onTelegramAuth(user)');
    script.setAttribute('data-request-access', 'write');
    this.tgMount.nativeElement.appendChild(script);
  }

  private describe(e: unknown, fallback: string): string {
    if (e && typeof e === 'object' && 'error' in e) {
      const err = (e as { error?: { message?: string } }).error;
      if (err?.message) return err.message;
    }
    return fallback;
  }
}
