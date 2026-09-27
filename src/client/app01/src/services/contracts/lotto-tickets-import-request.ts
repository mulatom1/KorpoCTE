export interface LottoTicketsImportRequest {
  tickets?: LottoTicketsImportTicket[];
  csv?: string;
  drawTypeId?: number;
  groupName?: string;
}

export interface LottoTicketsImportTicket {
  drawTypeId: number;
  groupName: string | null;
  numbers: number[];
  specials: number[];
}
