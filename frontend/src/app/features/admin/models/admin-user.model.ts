import { UserRole } from '../../../core/models/user.model';

export type UserSortKey =
  | 'CreatedAtDesc'
  | 'CreatedAtAsc'
  | 'LastSeenAtDesc'
  | 'DisplayNameAsc';

export interface AdminUserRowDto {
  readonly id: string;
  readonly displayName: string;
  readonly email: string | null;
  readonly role: UserRole;
  readonly createdAt: string;
  readonly lastSeenAt: string | null;
  readonly isBlocked: boolean;
  readonly deletedAt: string | null;
  readonly totalInputTokens: number;
  readonly totalOutputTokens: number;
  readonly estimatedCostUsd: number;
  readonly lastCallAt: string | null;
  readonly callCount: number;
}

export interface AdminUserPageDto {
  readonly items: ReadonlyArray<AdminUserRowDto>;
  readonly page: number;
  readonly pageSize: number;
  readonly totalCount: number;
}

export interface AdminUserListQuery {
  readonly page?: number;
  readonly pageSize?: number;
  readonly search?: string;
  readonly sort?: UserSortKey;
}
