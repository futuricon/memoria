export interface TrashedCardDto {
  readonly id: string;
  readonly title: string;
  readonly tags: readonly string[];
  readonly deletedAt: string;
  readonly reviewsCount: number;
}
