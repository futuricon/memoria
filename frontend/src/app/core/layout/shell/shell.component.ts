import { ChangeDetectionStrategy, Component, inject, signal } from "@angular/core";
import {
  NavigationEnd,
  Router,
  RouterLink,
  RouterLinkActive,
  RouterOutlet,
} from "@angular/router";
import { filter } from "rxjs";

import { AuthService } from "../../auth/auth.service";
import { ThemeToggleComponent } from "../../theme/theme-toggle/theme-toggle.component";
import { IconComponent } from "../../ui/icon/icon.component";

interface NavItem {
  path: string;
  label: string;
  icon: "layout-dashboard" | "play" | "list" | "trash-2" | "settings";
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
    ThemeToggleComponent,
  ],
  templateUrl: "./shell.component.html",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ShellComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly sidebarOpen = signal(false);

  readonly navItems: NavItem[] = [
    { path: "/", label: "Dashboard", icon: "layout-dashboard", exact: true },
    { path: "/practice", label: "Practice", icon: "play" },
    { path: "/cards", label: "Cards", icon: "list" },
    { path: "/trash", label: "Trash", icon: "trash-2" },
    { path: "/settings", label: "Settings", icon: "settings" },
  ];

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
