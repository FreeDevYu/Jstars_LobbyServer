using LobbyServer.Helper;
using LobbyServer.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Protocol;
using StackExchange.Redis;

namespace LobbyServer.BackgroundServices
{
    public class MatchResultWorker : BackgroundService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<SignalRHub> _hubContext;
        private readonly ILogger<MatchResultWorker> _logger;

        private const string QueueKey = "Queue:MatchResult";

        public MatchResultWorker(
            IConnectionMultiplexer redis,
            IServiceScopeFactory scopeFactory,
            IHubContext<SignalRHub> hubContext,
            ILogger<MatchResultWorker> logger)
        {
            _redis = redis;
            _scopeFactory = scopeFactory;
            _hubContext = hubContext;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var db = _redis.GetDatabase();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var redisValue = await db.ListRightPopAsync(QueueKey);

                    if (redisValue.HasValue)
                    {
                        byte[] payload = (byte[])redisValue!;

                        if (payload != null && payload.Length > 0)
                        {
                            MatchingResultDTO resultDTO = MatchingResultDTO.Parser.ParseFrom(payload);

                            using (var scope = _scopeFactory.CreateScope())
                            {
                                var characterHelper = scope.ServiceProvider.GetRequiredService<ICharacterHelper>();
                                await characterHelper.ApplyMatchRewardsAsync(resultDTO);

                                _logger.LogInformation(
                                    "[게임 결과 수신] RoomId={RoomId}, GameModeId={GameModeId}, MapId={MapId}, WinnerTeamId={WinnerTeamId}, WinCount={WinCount}, LoseCount={LoseCount}",
                                    resultDTO.RoomId,
                                    resultDTO.GameModeId,
                                    resultDTO.MapId,
                                    resultDTO.WinnerTeamId,
                                    resultDTO.WinPlayers.Count,
                                    resultDTO.LosePlayers.Count);

                                foreach (var winPlayer in resultDTO.WinPlayers)
                                {
                                    _logger.LogInformation(
                                        "[게임 결과][WIN] Uid={Uid}, TeamId={TeamId}, Kills={Kills}, Deaths={Deaths}",
                                        winPlayer.Uid,
                                        winPlayer.TeamId,
                                        winPlayer.Kills,
                                        winPlayer.Deaths);
                                }

                                foreach (var losePlayer in resultDTO.LosePlayers)
                                {
                                    _logger.LogInformation(
                                        "[게임 결과][LOSE] Uid={Uid}, TeamId={TeamId}, Kills={Kills}, Deaths={Deaths}",
                                        losePlayer.Uid,
                                        losePlayer.TeamId,
                                        losePlayer.Kills,
                                        losePlayer.Deaths);
                                }
                            }

                            await NotifyMatchResultAsync(db, resultDTO, stoppingToken);
                        }
                    }
                    else
                    {
                        await Task.Delay(1000, stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Worker Error: {Message}", ex.Message);
                    await Task.Delay(2000, stoppingToken);
                }
            }
        }

        private static object BuildMatchResultPayload(MatchingResultDTO resultDTO)
        {
            var playerStats = new List<object>(
                resultDTO.WinPlayers.Count + resultDTO.LosePlayers.Count);

            foreach (var player in resultDTO.WinPlayers)
            {
                MatchPlayerReward reward = GameRewardCalculator.Calculate(player, isWin: true);
                playerStats.Add(BuildPlayerStatPayload(player, reward));
            }

            foreach (var player in resultDTO.LosePlayers)
            {
                MatchPlayerReward reward = GameRewardCalculator.Calculate(player, isWin: false);
                playerStats.Add(BuildPlayerStatPayload(player, reward));
            }

            return new { PlayerStats = playerStats };
        }

        private static object BuildPlayerStatPayload(PlayerMatchResult player, MatchPlayerReward reward)
        {
            return new
            {
                UID = (long)player.Uid,
                TeamID = player.TeamId,
                Kill = reward.Kills,
                Death = reward.Deaths,
                RewardExp = (int)reward.ExpReward,
                RewardGold = (int)reward.GoldReward
            };
        }

        private async Task NotifyMatchResultAsync(
            IDatabase db,
            MatchingResultDTO resultDTO,
            CancellationToken stoppingToken)
        {
            var uids = resultDTO.WinPlayers
                .Select(player => (long)player.Uid)
                .Concat(resultDTO.LosePlayers.Select(player => (long)player.Uid))
                .Distinct()
                .ToList();

            if (uids.Count == 0)
            {
                _logger.LogWarning("[MatchResult] 전송 스킵: 참가자 없음");
                return;
            }

            object matchResultPayload = BuildMatchResultPayload(resultDTO);

            _logger.LogInformation(
                "[MatchResult] 전송 시작. PlayerCount={PlayerCount}, Uids={Uids}",
                resultDTO.WinPlayers.Count + resultDTO.LosePlayers.Count,
                string.Join(", ", uids));

            var keys = uids.Select(uid => (RedisKey)$"SignalRConn:{uid}").ToArray();
            var connectionIds = await db.StringGetAsync(keys);

            int sentCount = 0;
            int skippedCount = 0;

            var sendTasks = uids.Select(async (uid, index) =>
            {
                var connectionId = connectionIds[index];
                if (connectionId.IsNullOrEmpty)
                {
                    Interlocked.Increment(ref skippedCount);
                    _logger.LogWarning("[MatchResult] 전송 스킵: SignalR 미연결. Uid={Uid}", uid);
                    return;
                }

                try
                {
                    await _hubContext.Clients.Client(connectionId!)
                        .SendAsync("MatchResult", matchResultPayload, stoppingToken);

                    Interlocked.Increment(ref sentCount);
                    _logger.LogInformation(
                        "[MatchResult] 전송 성공. Uid={Uid}, ConnectionId={ConnectionId}",
                        uid,
                        connectionId.ToString());
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref skippedCount);
                    _logger.LogError(ex, "[MatchResult] 전송 실패. Uid={Uid}, ConnectionId={ConnectionId}", uid, connectionId.ToString());
                }
            });

            await Task.WhenAll(sendTasks);

            _logger.LogInformation(
                "[MatchResult] 전송 완료. Sent={SentCount}, Skipped={SkippedCount}, Total={TotalCount}",
                sentCount,
                skippedCount,
                uids.Count);
        }
    }
}
