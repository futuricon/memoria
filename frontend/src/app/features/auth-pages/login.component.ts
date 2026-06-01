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
  templateUrl: "./login.component.html",
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
