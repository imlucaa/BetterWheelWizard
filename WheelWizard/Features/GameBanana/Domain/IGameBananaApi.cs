using Refit;

namespace WheelWizard.GameBanana.Domain;

public interface IGameBananaApi
{
    [Get("/Mod/{modId}/ProfilePage")]
    Task<GameBananaModDetails> GetModDetails(int modId);

    [Get("/Util/Search/Results")]
    Task<GameBananaSearchResults> GetModSearchResults(
        [AliasAs("_sSearchString")] string searchString,
        [AliasAs("_idGameRow")] int gameId,
        [AliasAs("_sModelName")] string modelName,
        [AliasAs("_nPage")] int page = 1
    );
}
