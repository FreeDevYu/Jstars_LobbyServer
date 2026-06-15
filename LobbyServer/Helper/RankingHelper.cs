using LobbyAPI;
using LobbyAPI.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LobbyServer.Helper
{
    public interface IRankingHelper
    {
        Task<RankingListResponse> GetRankingListAsync();
    }

    public class RankingHelper : IRankingHelper
    {
        private readonly IRedisHelper _redisHelper;

        public RankingHelper(IRedisHelper redisHelper)
        {
            _redisHelper = redisHelper;
        }

        public async Task<RankingListResponse> GetRankingListAsync()
        {
            string displayJson = await _redisHelper.GetValueAsync(RankingConstants.DisplayKey);
            if (string.IsNullOrWhiteSpace(displayJson))
            {
                return new RankingListResponse
                {
                    Success = false,
                    Entries = new List<RankingEntry>()
                };
            }

            List<RankingEntry>? entries = JsonSerializer.Deserialize<List<RankingEntry>>(displayJson);
            if (entries == null || entries.Count == 0)
            {
                return new RankingListResponse
                {
                    Success = false,
                    Entries = new List<RankingEntry>()
                };
            }

            DateTime refreshedAt = DateTime.MinValue;
            string metaJson = await _redisHelper.GetValueAsync(RankingConstants.MetaKey);
            if (!string.IsNullOrWhiteSpace(metaJson))
            {
                RankingCacheMeta? meta = JsonSerializer.Deserialize<RankingCacheMeta>(metaJson);
                if (meta != null)
                    refreshedAt = DateTime.SpecifyKind(meta.RefreshedAt, DateTimeKind.Utc);
            }

            return new RankingListResponse
            {
                Success = true,
                RefreshedAt = refreshedAt,
                Entries = entries
            };
        }

        private sealed class RankingCacheMeta
        {
            [JsonPropertyName("refreshedAt")]
            public DateTime RefreshedAt { get; set; }
        }
    }
}
