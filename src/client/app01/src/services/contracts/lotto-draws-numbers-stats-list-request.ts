export interface LottoDrawsNumbersStatsListRequest {
  drawTypeId: number;
  numbersGroup: number;
  specialsGroup?: number;
  sortOrder?: string;
  drawDateFrom?: string;
  drawDateTo?: string;
}
