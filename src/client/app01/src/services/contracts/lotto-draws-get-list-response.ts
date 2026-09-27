export interface LottoDrawsGetListResponse {
  draws: LottoDrawsGetListDraw[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface LottoDrawsGetListDraw {
  id: number;
  drawSystemId: number;
  drawDate: string;
  drawTypeId: number;
  numbers: number[];
  specials: number[];
}
