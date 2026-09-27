export interface LottoTicketsGetListResponse {
  tickets: LottoTicketsGetListTicket[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface LottoTicketsGetListTicket {
  id: number;
  drawTypeId: number;
  drawTypeName: string;
  groupName: string | null;
  createdAt: string;
  numbers: number[];
  specials: number[];
}
