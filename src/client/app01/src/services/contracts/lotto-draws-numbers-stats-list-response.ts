export interface LottoDrawsNumbersStatsListResponse {
  numbersStats: NumbersGroupStatsDto[];
  specialsStats: NumbersGroupStatsDto[];
  totalDrawsCount: number;
  totalNumbersGroupsCount: number;
  totalSpecialsGroupsCount: number;
}

export interface NumbersGroupStatsDto {
  numbers: number[];
  count: number;
}
