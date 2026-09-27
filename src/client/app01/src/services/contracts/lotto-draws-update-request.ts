export interface LottoDrawsUpdateRequest {
  id: number;
  drawSystemId: number;
  drawDate: string;
  drawTypeId: number;
  numbers: number[];
  specials: number[];
}
