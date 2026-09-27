export interface LottoTicketsAddRequest {
  drawTypeId: number;
  groupName?: string | null;
  numbers: number[];
  specials: number[];
}
