export interface DueReminderDto {
  readonly reminderId: string;
  readonly cardId: string;
  readonly cardTitle: string;
  readonly scheduledAt: string;
  readonly stageNumber: number;
}

export interface RevealedAnswerDto {
  readonly cardId: string;
  readonly title: string;
  readonly body: string;
}
