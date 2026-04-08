/*
using LobbyAPI.Models;
using LobbyServer.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using System.Text.Json;

namespace LobbyServer.BackgroundServices
{
    public class InventoryUpdateWorker : BackgroundService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IServiceScopeFactory _scopeFactory;

        // 중복을 허용하지 않는 Redis Set의 키 이름
        private const string DirtyUsersSetKey = "dirty_inventory_users";

        public InventoryUpdateWorker(IConnectionMultiplexer redis, IServiceScopeFactory scopeFactory)
        {
            _redis = redis;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var db = _redis.GetDatabase();

            // 워커 시작 시 첫 대기 (바로 시작하지 않고 일정 시간 모음)
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // 1. Set에서 변경된 유저 ID 목록을 한 번에 왕창 꺼냅니다 (SPOP)
                    // SetPopAsync에 개수를 지정하면 해당 개수만큼 꺼내고 Set에서 원자적으로 삭제합니다.
                    // (한 번에 최대 10,000명씩 끊어서 처리한다고 가정)
                    var dirtyUserIds = await db.SetPopAsync(DirtyUsersSetKey, 10000);

                    if (dirtyUserIds != null && dirtyUserIds.Length > 0)
                    {
                        using (var scope = _scopeFactory.CreateScope())
                        {
                            // DB 처리를 위한 Repository나 Helper를 가져옵니다.
                            var inventoryRepo = scope.ServiceProvider.GetRequiredService<IUserRespository>();

                            // 2. 꺼내온 유저들의 Redis '최종 인벤토리 상태'를 조회합니다.
                            var bulkUpdateData = new List<Item>();

                            foreach (var redisValue in dirtyUserIds)
                            {
                                long characterID = (long)redisValue;
                                string redisKey = $"inventory:{characterID}";

                                // Redis에서 해당 유저의 최신 상태(Hash) 전체를 가져옴
                                var currentInventoryHash = await db.HashGetAllAsync(redisKey);

                                if (currentInventoryHash.Length > 0)
                                {
                                    // DB에 통째로 덮어쓸 스냅샷 데이터 생성
                                    bulkUpdateData.Add(new Item
                                    {
                                        CharacterID = characterID,
                                        Items = currentInventoryHash // 필요에 따라 파싱
                                    });
                                }
                            }

                            // 3. 수집된 최종 상태를 DB에 일괄 업데이트 (Bulk Update)
                            if (bulkUpdateData.Count > 0)
                            {
                                await inventoryRepo.BulkUpdateInventoriesAsync(bulkUpdateData);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Inventory Sync Worker Error: {ex.Message}");
                    // TODO: 에러 발생 시 처리하지 못한 dirtyUserIds를 다시 Set에 넣는(SADD) 복구 로직 필요
                }

                // 4. 모든 처리가 끝나면 다시 30초 동안 대기하며 더티 데이터를 모읍니다.
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
}
*/