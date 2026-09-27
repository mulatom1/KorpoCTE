export interface LottoWinningTicketsRequest {
  drawDateFrom?: string;
  drawDateTo?: string;
  drawTypeId?: number;
  groupName?: string;
  page?: number;
  pageSize?: number;
  winTier1?: boolean;
  winTier2?: boolean;
  winTier3?: boolean;
  winTier4?: boolean;
  winTier5?: boolean;
  winTier6?: boolean;
  winTier7?: boolean;
  winTier8?: boolean;
  winTier9?: boolean;
  winTier10?: boolean;
  winTier11?: boolean;
  winTier12?: boolean;
  hideDrawsWithoutMatches?: boolean;
}
