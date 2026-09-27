export interface LottoDrawsGetListRequest {
  drawDateFrom?: string;
  drawDateTo?: string;
  drawTypeId?: number;
  page?: number;
  pageSize?: number;
  sortOrder?: 'asc' | 'desc';
}
