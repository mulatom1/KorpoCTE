export interface LottoWinningTicketsResponse {
  draws: LottoWinningTicketsDraw[];
  summary: LottoWinningTicketsSummary;
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface LottoWinningTicketsDraw {
  id: number;
  drawSystemId: number;
  drawDate: string;
  drawTypeId: number;
  ticketPrice: number;
  numbers: number[];
  specials: number[];
  matchingTickets: LottoWinningTicketsMatchingTicket[];
}

export interface LottoWinningTicketsMatchingTicket {
  id: number;
  drawTypeId: number;
  groupName: string | null;
  createdAt: string;
  numbers: number[];
  specials: number[];
  matchedNumbers: number[];
  matchedSpecials: number[];
  winTier: number;
  winPrize: number;
  ticketPrice: number;
}

export interface LottoWinningTicketsSummary {
  totalDraws: number;
  totalTickets: number;
  totalBets: number;
  totalCost: number;
  totalWinningBets: number;
  totalWinPrize: number;
  balance: number;
  winsByTier: LottoWinningTicketsWinTierSummary[];
}

export interface LottoWinningTicketsWinTierSummary {
  winTier: number;
  winCount: number;
  winPrize: number;
}
