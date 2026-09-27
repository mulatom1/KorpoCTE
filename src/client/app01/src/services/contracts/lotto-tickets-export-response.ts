export interface LottoTicketsExportResponse {
  tickets: LottoTicketsExportTicket[];
  csv: string;
  fileName: string;
  totalCount: number;
  exportDate: string;
}

export interface LottoTicketsExportTicket {
  drawTypeId: number;
  groupName: string | null;
  numbers: number[];
  specials: number[];
}
