export interface LottoDrawsImportRequest {
  draws?: LottoDrawsImportDraw[];
  csv?: string;
  drawTypeId?: number;
}

export interface LottoDrawsImportDraw {
  drawSystemId: number;
  drawDate: string;
  drawTypeId: number;
  numbers: number[];
  specials: number[];
}
