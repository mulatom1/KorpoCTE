export interface LottoDrawsAddRequest {
  drawSystemId: number;
  drawDate: string;
  drawTypeId: number;
  numbers: number[];
  specials: number[];
}
