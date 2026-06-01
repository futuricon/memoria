import { Injectable, signal } from '@angular/core';

interface TelegramThemeParams {
  bg_color?: string;
  text_color?: string;
  hint_color?: string;
  link_color?: string;
  button_color?: string;
  button_text_color?: string;
  secondary_bg_color?: string;
}

interface TelegramWebApp {
  initData: string;
  initDataUnsafe?: { start_param?: string };
  colorScheme: 'light' | 'dark';
  themeParams: TelegramThemeParams;
  ready: () => void;
  expand: () => void;
  onEvent: (event: string, handler: () => void) => void;
  offEvent: (event: string, handler: () => void) => void;
}

declare global {
  interface Window {
    Telegram?: { WebApp?: TelegramWebApp };
  }
}

@Injectable({ providedIn: 'root' })
export class TelegramWebAppService {
  private readonly tg: TelegramWebApp | undefined =
    typeof window !== 'undefined' ? window.Telegram?.WebApp : undefined;

  readonly isInMiniApp: boolean = !!this.tg?.initData;
  readonly initData: string = this.tg?.initData ?? '';
  readonly startParam: string | undefined = this.tg?.initDataUnsafe?.start_param;

  readonly colorScheme = signal<'light' | 'dark'>(this.tg?.colorScheme ?? 'light');

  ready(): void {
    if (!this.tg) return;
    this.tg.ready();
    this.tg.expand();
    this.tg.onEvent('themeChanged', () => {
      this.colorScheme.set(this.tg!.colorScheme);
    });
  }
}
