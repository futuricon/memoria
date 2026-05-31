import { Injectable, signal } from '@angular/core';

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
  private readonly media =
    typeof window !== 'undefined' ? window.matchMedia('(prefers-color-scheme: dark)') : null;

  private readonly _mode = signal<ThemeMode>(this.readInitial());

  readonly mode = this._mode.asReadonly();

  constructor() {
    this.media?.addEventListener?.('change', () => {
      if (this._mode() === 'system') this.apply('system');
    });
    this.apply(this._mode());
  }

  /** Cycles system → light → dark → system. */
  cycle(): void {
    const next: ThemeMode =
      this._mode() === 'system' ? 'light' : this._mode() === 'light' ? 'dark' : 'system';
    this.set(next);
  }

  set(mode: ThemeMode): void {
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
