import { Injectable, effect, inject, signal } from '@angular/core';

import { TelegramWebAppService } from './telegram-web-app.service';

export type ThemeMode = 'system' | 'light' | 'dark';

const COOKIE_NAME = 'memoria.theme';
const COOKIE_DAYS = 365;

function readCookie(name: string): string | null {
  if (typeof document === 'undefined') return null;
  const parts = ('; ' + document.cookie).split('; ' + name + '=');
  return parts.length === 2 ? parts.pop()!.split(';').shift() ?? null : null;
}

function writeCookie(name: string, value: string, days: number): void {
  if (typeof document === 'undefined') return;
  const d = new Date();
  d.setTime(d.getTime() + days * 86400000);
  const secure = location.protocol === 'https:' ? '; Secure' : '';
  document.cookie = `${name}=${value}; expires=${d.toUTCString()}; path=/; SameSite=Lax${secure}`;
}

function clearCookie(name: string): void {
  if (typeof document === 'undefined') return;
  document.cookie = `${name}=; Max-Age=0; path=/`;
}

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly tg = inject(TelegramWebAppService);

  private readonly media =
    typeof window !== 'undefined' ? window.matchMedia('(prefers-color-scheme: dark)') : null;

  private readonly _mode = signal<ThemeMode>(this.readInitial());

  readonly mode = this._mode.asReadonly();
  readonly isInMiniApp = this.tg.isInMiniApp;

  constructor() {
    this.media?.addEventListener?.('change', () => {
      if (this._mode() === 'system') this.apply(this._mode());
    });

    // Inside Telegram the host owns the theme — we mirror tg.colorScheme and
    // ignore the cookie. The signal updates on Telegram's `themeChanged`
    // event (wired up in TelegramWebAppService.ready()).
    if (this.tg.isInMiniApp) {
      effect(() => {
        const scheme = this.tg.colorScheme();
        document.documentElement.classList.toggle('dark', scheme === 'dark');
        document.documentElement.setAttribute('data-theme-mode', scheme);
      });
    } else {
      this.apply(this._mode());
    }
  }

  cycle(): void {
    if (this.tg.isInMiniApp) return;
    const next: ThemeMode =
      this._mode() === 'system' ? 'light' : this._mode() === 'light' ? 'dark' : 'system';
    this.set(next);
  }

  set(mode: ThemeMode): void {
    if (this.tg.isInMiniApp) return;
    this._mode.set(mode);
    if (mode === 'system') clearCookie(COOKIE_NAME);
    else writeCookie(COOKIE_NAME, mode, COOKIE_DAYS);
    this.apply(mode);
  }

  private readInitial(): ThemeMode {
    const c = readCookie(COOKIE_NAME);
    return c === 'light' || c === 'dark' ? c : 'system';
  }

  private apply(mode: ThemeMode): void {
    if (typeof document === 'undefined') return;
    const dark = mode === 'dark' || (mode === 'system' && !!this.media?.matches);
    document.documentElement.classList.toggle('dark', dark);
    document.documentElement.setAttribute('data-theme-mode', mode);
  }
}
