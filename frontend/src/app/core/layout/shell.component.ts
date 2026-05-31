import { Component, inject, signal } from '@angular/core';
import {
  NavigationEnd,
  Router,
  RouterLink,
  RouterLinkActive,
  RouterOutlet,
} from '@angular/router';
import { filter } from 'rxjs';

import { AuthService } from '../auth/auth.service';
import { ThemeToggleComponent } from '../theme/theme-toggle.component';
import { IconComponent } from '../ui/icon.component';

interface NavItem {
  path: string;
  label: string;
  icon:
    | 'layout-dashboard'
    | 'play'
    | 'list'
    | 'trash-2'
    | 'settings';
  exact?: boolean;
}

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
    IconComponent,
    ThemeToggleComponent,
  ],
  template: `
    <div class="min-h-screen flex flex-col md:flex-row bg-page">

      <!-- Mobile top bar -->
      <header class="md:hidden sticky top-0 z-30 h-12 px-3 flex items-center gap-2 bg-page border-b border-default">
        <button
          type="button"
          (click)="openSidebar()"
          class="w-10 h-10 -ml-1 rounded-md text-fg-secondary hover:bg-surface-hover flex items-center justify-center"
          aria-label="Open menu"
        >
          <app-icon name="menu" [size]="20" />
        </button>
        <div class="flex items-center gap-2 flex-1 min-w-0">
          <span class="w-6 h-6 rounded-md bg-brand text-brand-on grid place-items-center text-[10px] font-semibold">M</span>
          <span class="text-sm font-semibold tracking-tight truncate">Memoria</span>
        </div>
        <app-theme-toggle />
      </header>

      <!-- Mobile backdrop -->
      <div
        class="sidebar-backdrop fixed inset-0 bg-black/55 z-40 md:hidden"
        [class.is-open]="sidebarOpen()"
        (click)="closeSidebar()"
      ></div>

      <!-- Sidebar — static on md+, off-canvas drawer on mobile -->
      <aside
        class="sidebar-mobile md:translate-x-0 md:static fixed inset-y-0 left-0 z-40 w-64 bg-sidebar border-r border-default flex flex-col pb-safe"
        [class.is-open]="sidebarOpen()"
      >
        <div class="h-12 px-5 flex items-center gap-2 border-b border-default">
          <span class="w-6 h-6 rounded-md bg-brand text-brand-on grid place-items-center text-[10px] font-semibold">M</span>
          <span class="text-sm font-semibold tracking-tight">Memoria</span>
        </div>

        <nav class="flex-1 px-3 py-4 text-sm flex flex-col gap-0.5">
          @for (item of navItems; track item.path) {
            <a
              [routerLink]="item.path"
              [routerLinkActiveOptions]="{ exact: !!item.exact }"
              routerLinkActive="!bg-brand-soft !text-fg border border-brand-soft"
              class="flex items-center gap-2.5 px-3 py-2 rounded-md text-fg-secondary hover:bg-surface-hover hover:text-fg border border-transparent transition-colors"
              (click)="closeSidebar()"
            >
              <app-icon [name]="item.icon" [size]="16" />
              <span>{{ item.label }}</span>
            </a>
          }
        </nav>

        <div class="px-3 pb-3 hidden md:flex justify-between items-center gap-2 border-t border-default pt-3">
          <app-theme-toggle />
          <button
            type="button"
            (click)="logout()"
            class="h-9 px-2.5 rounded-md text-fg-secondary hover:bg-surface-hover hover:text-fg text-xs flex items-center gap-1.5 transition-colors"
          >
            <app-icon name="log-out" [size]="16" />
            Log out
          </button>
        </div>
        <button
          type="button"
          (click)="logout()"
          class="md:hidden mx-3 mb-3 mt-auto h-10 rounded-md border border-default text-fg-secondary hover:bg-surface-hover text-sm flex items-center justify-center gap-1.5"
        >
          <app-icon name="log-out" [size]="16" />
          Log out
        </button>
      </aside>

      <main class="flex-1 min-w-0">
        <router-outlet />
      </main>
    </div>
  `,
})
export class ShellComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly sidebarOpen = signal(false);

  readonly navItems: NavItem[] = [
    { path: '/', label: 'Dashboard', icon: 'layout-dashboard', exact: true },
    { path: '/practice', label: 'Practice', icon: 'play' },
    { path: '/cards', label: 'Cards', icon: 'list' },
    { path: '/trash', label: 'Trash', icon: 'trash-2' },
    { path: '/settings', label: 'Settings', icon: 'settings' },
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
