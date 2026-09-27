export interface MailListDto {
  id: number;
  email: string;
  topic: string;
  createdAt: string;
}

export interface MailFromClientListResponse {
  mails: MailListDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}
