import { ChangeDetectionStrategy, Component, inject, signal } from "@angular/core";
import {
  NavigationEnd,
  Router,
  RouterLink,
  RouterLinkActive,
  RouterOutlet,
} from "@angular/router";
import { filter } from "rxjs";

import { AuthService } from "../../services/auth.service";
import { IconComponent } from "../../../shared/components/icon/icon.component";
import { LogoComponent } from "../../../shared/components/logo/logo.component";
import { ThemeToggleComponent } from "../../../shared/components/theme-toggle/theme-toggle.component";

interface NavItem {
  path: string;
  label: string;
  icon: "layout-dashboard" | "play" | "list" | "trash-2" | "settings" | "gauge";
  exact?: boolean;
}

@Component({
  selector: "app-shell",
  standalone: true,
  imports: [
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
    IconComponent,
    LogoComponent,
    ThemeToggleComponent,
  ],
  templateUrl: "./shell.component.html",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ShellComponent {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly sidebarOpen = signal(false);

  readonly navItems: NavItem[] = [
    { path: "/", label: "Dashboard", icon: "layout-dashboard", exact: true },
    { path: "/practice", label: "Practice", icon: "play" },
    { path: "/cards", label: "Cards", icon: "list" },
    { path: "/trash", label: "Trash", icon: "trash-2" },
    { path: "/settings", label: "Settings", icon: "settings" },
  ];

  /** Separate list so we can render it conditionally without splitting @for. */
  readonly adminNavItem: NavItem = {
    path: "/admin",
    label: "Admin",
    icon: "gauge",
  };

  constructor() {
    // Close mobile drawer whenever a navigation completes (covers anchor +
    // programmatic nav both).
    this.router.events
      .pipe(filter((e) => e instanceof NavigationEnd))
      .subscribe(() => this.sidebarOpen.set(false));
  }

  openSidebar(): void {
    this.sidebarOpen.set(true);
  }

  closeSidebar(): void {
    this.sidebarOpen.set(false);
  }

  logout(): void {
    this.auth.logout();
  }
}
