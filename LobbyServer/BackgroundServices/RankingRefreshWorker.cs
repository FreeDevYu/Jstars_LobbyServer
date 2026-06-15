using LobbyAPI;
using LobbyAPI.Models;
using LobbyServer.Repositories;
using System.Text.Json;

namespace LobbyServer.BackgroundServices
{
    public class RankingRefreshWorker : BackgroundService
    {
        private readonly IRedisHelper _redisHelper;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<RankingRefreshWorker> _logger;

        public RankingRefreshWorker(
            IRedisHelper redisHelper,
            IServiceScopeFactory scopeFactory,
            ILogger<RankingRefreshWorker> logger)
        {
            _redisHelper = redisHelper;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await RefreshRankingCacheAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(RankingConstants.RefreshInterval, stoppingToken);
                    await RefreshRankingCacheAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[RankingRefresh] Worker error");
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
            }
        }

        private async Task RefreshRankingCacheAsync(CancellationToken stoppingToken)
        {
            string lockToken = Guid.NewGuid().ToString();
            bool isLocked = await _redisHelper.AcquireLockAsync(
                RankingConstants.RefreshLockKey,
                lockToken,
                RankingConstants.RefreshLockExpiry);

            if (!isLocked)
            {
                _logger.LogDebug("[RankingRefresh] Skipped. Another instance is refreshing.");
                return;
            }

            try
            {
                stoppingToken.ThrowIfCancellationRequested();

                IReadOnlyList<RankingEntry> entries;
                using (var scope = _scopeFactory.CreateScope())
                {
                    var repository = scope.ServiceProvider.GetRequiredService<ILobbyRespository>();
                    entries = await repository.GetPvpRankingTopAsync();
                }

                DateTime refreshedAt = DateTime.UtcNow;
                string displayJson = JsonSerializer.Serialize(entries);
                string metaJson = JsonSerializer.Serialize(new { refreshedAt });

                await _redisHelper.SetKeyValueAsync(
                    RankingConstants.DisplayKey,
                    displayJson,
                    RankingConstants.CacheExpiry);

                await _redisHelper.SetKeyValueAsync(
                    RankingConstants.MetaKey,
                    metaJson,
                    RankingConstants.CacheExpiry);

                _logger.LogInformation(
                    "[RankingRefresh] Cached {EntryCount} entries at {RefreshedAt:O}",
                    entries.Count,
                    refreshedAt);
            }
            finally
            {
                await _redisHelper.ReleaseLockAsync(RankingConstants.RefreshLockKey, lockToken);
            }
        }
    }
}
