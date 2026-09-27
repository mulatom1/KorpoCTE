export interface LottoDrawsGetPrizesListResponse {
  winTiers: WinTierDto[];
  drawDate: string;
  drawSystemId: number;
  gameType: string;
}

export interface WinTierDto {
  tier: string;
  winsCount: number;
  winsPrize: number;
}
