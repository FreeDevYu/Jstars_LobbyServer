using LobbyAPI.Models;
using LobbyServer.Helper;

namespace LobbyServer.Services
{
    public interface IRankingService
    {
        Task<RankingListResponse> GetRankingListAsync(RankingListRequest request);
    }

    public class RankingService : IRankingService
    {
        private readonly IRankingHelper _rankingHelper;

        public RankingService(IRankingHelper rankingHelper)
        {
            _rankingHelper = rankingHelper;
        }

        public Task<RankingListResponse> GetRankingListAsync(RankingListRequest request)
        {
            return _rankingHelper.GetRankingListAsync();
        }
    }
}
