const ACCESS_KEY = 'memoria.accessToken';
const REFRESH_KEY = 'memoria.refreshToken';
const EXPIRES_KEY = 'memoria.expiresAt';

export interface TokenBundle {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
}

export const tokenStorage = {
  read(): TokenBundle | null {
    const accessToken = localStorage.getItem(ACCESS_KEY);
    const refreshToken = localStorage.getItem(REFRESH_KEY);
    const expiresAt = localStorage.getItem(EXPIRES_KEY);
    if (!accessToken || !refreshToken || !expiresAt) return null;
    return { accessToken, refreshToken, expiresAt };
  },
  write(b: TokenBundle): void {
    localStorage.setItem(ACCESS_KEY, b.accessToken);
    localStorage.setItem(REFRESH_KEY, b.refreshToken);
    localStorage.setItem(EXPIRES_KEY, b.expiresAt);
  },
  clear(): void {
    localStorage.removeItem(ACCESS_KEY);
    localStorage.removeItem(REFRESH_KEY);
    localStorage.removeItem(EXPIRES_KEY);
  },
};
