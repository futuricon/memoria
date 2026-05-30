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
