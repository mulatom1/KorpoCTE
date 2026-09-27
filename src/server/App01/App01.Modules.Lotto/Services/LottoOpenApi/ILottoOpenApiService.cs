using App01.Modules.Lotto.Services.LottoOpenApi.Dto;


namespace App01.Modules.Lotto.Services.LottoOpenApi;


public interface ILottoOpenApiService
{
    Task<GameInfoResponse?> GetInfoByGameType(string gameType);

    Task<LottoResponse> GetDrawsByDate(DateTime date);

    Task<List<DrawStatsResponse>?> GetDrawPrizes(string drawType, long drawSystemId);

    Task<List<LottoDraw>?> GetLastDraws();
}
