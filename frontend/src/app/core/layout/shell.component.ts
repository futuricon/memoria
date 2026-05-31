import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { AuthService } from '../auth/auth.service';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <div class="min-h-screen flex">
      <aside class="w-56 bg-slate-900 text-slate-100 flex flex-col">
        <div class="px-6 py-5 text-lg font-semibold tracking-tight">Memoria</div>
        <nav class="flex-1 flex flex-col gap-1 px-3 text-sm">
          <a
            routerLink="/"
            [routerLinkActiveOptions]="{ exact: true }"
            routerLinkActive="bg-slate-800"
            class="px-3 py-2 rounded hover:bg-slate-800"
          >Dashboard</a>
          <a
            routerLink="/practice"
            routerLinkActive="bg-slate-800"
            class="px-3 py-2 rounded hover:bg-slate-800"
          >Practice</a>
          <a
            routerLink="/cards"
            routerLinkActive="bg-slate-800"
            class="px-3 py-2 rounded hover:bg-slate-800"
          >Cards</a>
          <a
            routerLink="/trash"
            routerLinkActive="bg-slate-800"
            class="px-3 py-2 rounded hover:bg-slate-800"
          >Trash</a>
          <a
            routerLink="/settings"
            routerLinkActive="bg-slate-800"
            class="px-3 py-2 rounded hover:bg-slate-800"
          >Settings</a>
        </nav>
        <button
          type="button"
          (click)="logout()"
          class="m-3 px-3 py-2 text-sm rounded bg-slate-800 hover:bg-slate-700 text-left"
        >Log out</button>
      </aside>
      <main class="flex-1 px-8 py-6 overflow-x-hidden">
        <router-outlet />
      </main>
    </div>
  `,
})
export class ShellComponent {
  private readonly auth = inject(AuthService);

  logout(): void {
    this.auth.logout();
  }
}
