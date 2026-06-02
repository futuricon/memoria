export type UserRole = 'User' | 'Admin';

export interface CurrentUserDto {
  readonly id: string;
  readonly displayName: string;
  readonly email: string | null;
  readonly timeZoneId: string;
  readonly quietHoursStart: string | null;
  readonly quietHoursEnd: string | null;
  readonly createdAt: string;
  readonly role: UserRole;
}

export interface UserIdentityDto {
  readonly provider: string;
  readonly externalId: string;
  readonly linkedAt: string;
}

export interface UpdateMePayload {
  readonly timeZoneId: string;
  readonly quietHoursStart: string | null;
  readonly quietHoursEnd: string | null;
}

export interface TelegramLinkingTokenDto {
  readonly token: string;
  readonly deepLink: string;
  readonly expiresAt: string;
}

export interface TimeZoneDto {
  readonly id: string;
  readonly displayName: string;
}
