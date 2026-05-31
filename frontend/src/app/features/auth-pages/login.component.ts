import {
  AfterViewInit,
  Component,
  ElementRef,
  ViewChild,
  inject,
  signal,
} from "@angular/core";
import { FormsModule } from "@angular/forms";
import { Router } from "@angular/router";

import { environment } from "../../../environments/environment";
import { AuthService } from "../../core/auth/auth.service";
import { ThemeToggleComponent } from "../../core/theme/theme-toggle.component";
import { IconComponent } from "../../core/ui/icon.component";

type Tab = "email" | "telegram";
type EmailStep = "request" | "confirm";

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
  selector: "app-login",
  standalone: true,
  imports: [FormsModule, IconComponent, ThemeToggleComponent],
  template: `
    <div class="min-h-screen bg-page flex flex-col">
      <div class="flex justify-end px-4 py-3">
        <app-theme-toggle />
      </div>

      <div class="flex-1 flex items-center justify-center px-4 pb-12">
        <div
          class="w-full max-w-md bg-surface border border-default rounded-xl shadow-card p-7 md:p-8"
        >
          <div class="flex items-center justify-center gap-2 mb-1">
            <svg
              class="w-7 h-7 shrink-0 text-brand"
              viewBox="590 200 350 350"
              fill="currentColor"
              xmlns="http://www.w3.org/2000/svg"
              aria-hidden="true"
            >
              <path
                d="M881.5904,280.71062c0.6748,-0.09128 0.308,-0.05684 0.84,-0.08512c20.1208,-1.07044 36.6632,14.39004 36.4588,34.64888c-0.1008,9.87056 0.0224,19.92228 0.0196,29.78164l0.0028,55.2132v26.6028c0.0028,4.9616 0.2324,12.9304 -0.5964,17.5224c-1.2152,6.4876 -4.326,12.4712 -8.9376,17.1948c-6.4204,6.608 -14.7364,10.0184 -23.9148,10.0576c-8.9264,0.1288 -17.5252,-3.3516 -23.8476,-9.6544c-4.1692,-4.1468 -7.1568,-9.3268 -8.6604,-15.0108c-1.7808,-6.7648 -1.246,-17.3404 -1.2488,-24.6428l-0.0056,-28.504c-1.8088,2.5004 -11.0516,11.382 -13.7172,14.0448l-29.4896,29.4868l-15.1508,15.1872c-4.186,4.2 -9.6936,10.1808 -14.4984,13.23c-4.0292,2.5536 -8.5708,4.1916 -13.3028,4.7992c-8.86676,1.19 -17.84216,-1.1984 -24.94604,-6.636c-7.18592,-5.53 -11.82468,-13.734 -12.85732,-22.7416c-1.04104,-8.9068 1.52992,-17.8612 7.13832,-24.8612c1.96672,-2.4444 5.77276,-6.076 8.06932,-8.372l13.64496,-13.6444l45.40956,-45.40368l41.2832,-41.27508l11.8188,-11.8566c8.736,-8.76596 13.5352,-13.6444 26.488,-15.08164z"
                id="Path-1-1"
                fill="#22c7b8"
              ></path>
              <path
                d="M766.68484,280.71622c8.61196,0.01036 15.98716,2.28424 22.66236,7.91336c6.8376,5.80216 11.0768,14.09492 11.774,23.03672c0.7168,8.90708 -2.1812,17.72792 -8.0416,24.47424c-2.7524,3.14664 -6.6836,6.86672 -9.702,9.8826l-16.43768,16.42732l-51.87224,51.86916l-32.79584,32.802c-4.09808,4.1076 -8.35856,8.274 -12.40344,12.418c-7.24612,7.4256 -14.02688,10.6596 -24.40508,11.3372c-7.7896,0.658 -17.13068,-3.1948 -22.9404,-8.2488c-6.77936,-5.9024 -10.9018,-14.28 -11.43968,-23.2512c-1.04972,-15.9572 7.44576,-23.4612 17.6022,-33.6224l16.93216,-16.9344l52.52548,-52.52044l32.32516,-32.31928c3.67052,-3.67304 7.60676,-7.46648 11.18404,-11.21232c7.34748,-7.69384 14.29624,-11.40048 25.03256,-12.05176z"
                id="Path-1-2"
                fill="#22c7b8"
              ></path>
            </svg>
            <h1 class="text-xl font-semibold text-fg tracking-tight">
              Memoria
            </h1>
          </div>
          <p class="text-sm text-center text-fg-muted mb-6">
            Sign in to your spaced-repetition workspace
          </p>

          <div class="grid grid-cols-2 gap-2 mb-4">
            <button
              type="button"
              (click)="signInWith('google')"
              class="h-10 flex items-center justify-center gap-2 text-sm rounded-md border border-default bg-surface hover:bg-surface-hover text-fg transition-colors"
            >
              <svg
                width="16"
                height="16"
                viewBox="0 0 48 48"
                aria-hidden="true"
              >
                <path
                  fill="#FFC107"
                  d="M43.6 20.5H42V20H24v8h11.3C33.7 32.5 29.3 35 24 35c-6.6 0-12-5.4-12-12s5.4-12 12-12c3 0 5.7 1.1 7.8 2.9l5.7-5.7C33.6 5.1 29.1 3 24 3 12.4 3 3 12.4 3 24s9.4 21 21 21c11 0 20-8 20-21 0-1.3-.1-2.5-.4-3.5z"
                />
                <path
                  fill="#FF3D00"
                  d="m6.3 14.7 6.6 4.8C14.6 16 18.9 13 24 13c3 0 5.7 1.1 7.8 2.9l5.7-5.7C33.6 7.1 29.1 5 24 5c-7.7 0-14.3 4.4-17.7 9.7z"
                />
                <path
                  fill="#4CAF50"
                  d="M24 45c5 0 9.6-1.9 13-5l-6-5c-1.8 1.3-4.2 2-7 2-5.3 0-9.7-3.5-11.3-8.4L6 33.4C9.3 39.9 16.1 45 24 45z"
                />
                <path
                  fill="#1976D2"
                  d="M43.6 20.5H42V20H24v8h11.3c-.8 2.2-2.2 4.1-4 5.5l6 5c-.4.4 6.7-4.9 6.7-14.5 0-1.3-.1-2.5-.4-3.5z"
                />
              </svg>
              <span>Google</span>
            </button>
            <button
              type="button"
              (click)="signInWith('github')"
              class="h-10 flex items-center justify-center gap-2 text-sm rounded-md border border-default bg-surface hover:bg-surface-hover text-fg transition-colors"
            >
              <app-icon name="github" [size]="16" />
              <span>GitHub</span>
            </button>
          </div>

          <div class="relative my-5">
            <div class="absolute inset-0 flex items-center">
              <div class="w-full border-t border-default"></div>
            </div>
            <div class="relative flex justify-center text-xs">
              <span class="px-2 bg-surface text-fg-muted">or</span>
            </div>
          </div>

          <div
            class="grid grid-cols-2 gap-1 p-1 bg-surface-raised rounded-md mb-5 text-sm"
          >
            <button
              type="button"
              class="py-2 rounded-md transition-colors"
              [class.bg-surface]="tab() === 'email'"
              [class.shadow-card]="tab() === 'email'"
              [class.text-fg]="tab() === 'email'"
              [class.text-fg-secondary]="tab() !== 'email'"
              (click)="tab.set('email')"
            >
              Email
            </button>
            <button
              type="button"
              class="py-2 rounded-md transition-colors"
              [class.bg-surface]="tab() === 'telegram'"
              [class.shadow-card]="tab() === 'telegram'"
              [class.text-fg]="tab() === 'telegram'"
              [class.text-fg-secondary]="tab() !== 'telegram'"
              (click)="tab.set('telegram')"
            >
              Telegram
            </button>
          </div>

          @if (tab() === "email") {
            @if (emailStep() === "request") {
              <form (ngSubmit)="requestCode()" class="space-y-3">
                <label class="block">
                  <span
                    class="block text-xs font-medium text-fg-secondary mb-1.5"
                    >Email</span
                  >
                  <input
                    type="email"
                    name="email"
                    [(ngModel)]="email"
                    required
                    class="w-full px-3 py-2 bg-surface-raised border border-default rounded-md text-sm text-fg placeholder:text-fg-muted focus:outline-none focus:ring-2 focus:ring-brand/40 focus:border-brand"
                    placeholder="you@example.com"
                  />
                </label>
                <button
                  type="submit"
                  [disabled]="busy()"
                  class="w-full h-10 bg-brand text-brand-on rounded-md text-sm font-medium hover:bg-brand-hover disabled:opacity-50 inline-flex items-center justify-center gap-2"
                >
                  @if (busy()) {
                    <app-icon name="loader" [size]="14" class="animate-spin" />
                    Sending…
                  } @else {
                    Send code
                  }
                </button>
              </form>
            } @else {
              <form (ngSubmit)="confirmCode()" class="space-y-3">
                <p class="text-sm text-fg-secondary">
                  Code sent to
                  <span class="font-medium text-fg">{{ email }}</span
                  >. Check your inbox.
                </p>
                <label class="block">
                  <span
                    class="block text-xs font-medium text-fg-secondary mb-1.5"
                    >Verification code</span
                  >
                  <input
                    type="text"
                    name="code"
                    [(ngModel)]="code"
                    required
                    inputmode="numeric"
                    class="w-full px-3 py-2 bg-surface-raised border border-default rounded-md text-sm text-fg placeholder:text-fg-muted focus:outline-none focus:ring-2 focus:ring-brand/40 focus:border-brand tracking-widest text-center font-mono"
                    placeholder="123456"
                  />
                </label>
                <button
                  type="submit"
                  [disabled]="busy()"
                  class="w-full h-10 bg-brand text-brand-on rounded-md text-sm font-medium hover:bg-brand-hover disabled:opacity-50 inline-flex items-center justify-center gap-2"
                >
                  @if (busy()) {
                    <app-icon name="loader" [size]="14" class="animate-spin" />
                    Verifying…
                  } @else {
                    Sign in
                  }
                </button>
                <button
                  type="button"
                  (click)="emailStep.set('request')"
                  class="w-full py-2 text-sm text-fg-muted hover:text-fg-secondary inline-flex items-center justify-center gap-1.5"
                >
                  <app-icon name="arrow-left" [size]="14" />
                  Use a different email
                </button>
              </form>
            }
          } @else {
            <div class="flex flex-col items-center gap-3">
              <p class="text-sm text-fg-secondary text-center">
                Authorize with the official Telegram Login widget. No password
                required.
              </p>
              <div #tgMount></div>
            </div>
          }

          @if (error()) {
            <p class="mt-4 text-sm text-danger">{{ error() }}</p>
          }
        </div>
      </div>
    </div>
  `,
})
export class LoginComponent implements AfterViewInit {
  @ViewChild("tgMount") tgMount?: ElementRef<HTMLElement>;

  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly tab = signal<Tab>("email");
  readonly emailStep = signal<EmailStep>("request");
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  email = "";
  code = "";

  ngAfterViewInit(): void {
    window.onTelegramAuth = (user) => {
      void this.handleTelegramAuth(user);
    };
    this.mountTelegramWidget();
  }

  signInWith(provider: "google" | "github"): void {
    this.auth.startOAuth(provider);
  }

  async requestCode(): Promise<void> {
    if (!this.email.trim()) return;
    this.error.set(null);
    this.busy.set(true);
    try {
      await this.auth.startEmail(this.email.trim());
      this.emailStep.set("confirm");
    } catch (e) {
      this.error.set(this.describe(e, "Could not send verification code."));
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
      void this.router.navigate(["/"]);
    } catch (e) {
      this.error.set(this.describe(e, "Invalid or expired code."));
    } finally {
      this.busy.set(false);
    }
  }

  private async handleTelegramAuth(user: TelegramWidgetUser): Promise<void> {
    this.error.set(null);
    this.busy.set(true);
    try {
      await this.auth.authenticateTelegram(user);
      void this.router.navigate(["/"]);
    } catch (e) {
      this.error.set(this.describe(e, "Telegram sign-in failed."));
    } finally {
      this.busy.set(false);
    }
  }

  private mountTelegramWidget(): void {
    if (!this.tgMount) return;
    const script = document.createElement("script");
    script.async = true;
    script.src = "https://telegram.org/js/telegram-widget.js?22";
    script.setAttribute("data-telegram-login", environment.telegramBotUsername);
    script.setAttribute("data-size", "large");
    script.setAttribute("data-onauth", "onTelegramAuth(user)");
    script.setAttribute("data-request-access", "write");
    this.tgMount.nativeElement.appendChild(script);
  }

  private describe(e: unknown, fallback: string): string {
    if (e && typeof e === "object" && "error" in e) {
      const err = (e as { error?: { message?: string } }).error;
      if (err?.message) return err.message;
    }
    return fallback;
  }
}
