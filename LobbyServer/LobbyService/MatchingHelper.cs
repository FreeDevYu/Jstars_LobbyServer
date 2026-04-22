using LobbyAPI;
using LobbyAPI.Models;
using LobbyServer.Repositories;
using Google.Protobuf;
using StackExchange.Redis;
using System.Text.Json;

namespace LobbyServer.LobbyService
{
    public interface IMatchingHelper
    {
        Task<bool> EnqueueMatchingQueue(long uid);
        Task<bool> CancelMatchingQueue(long uid);
        Task<bool> CreateRoomAsync(List<long> uidList);
    }

    public class MatchingHelper : IMatchingHelper
    {
        private readonly ILobbyRespository _lobbyRespository;
        private readonly IRedisHelper _redisHelper;
        private readonly string _key = "MatchingQueue_ZSET";
        private readonly string _fieldServerCurrentKey = "FIELD_SERVER_CURRENT_USER";


        public MatchingHelper(IRedisHelper redisHelper, ILobbyRespository lobbyRespository)
        {
            _lobbyRespository = lobbyRespository;
            _redisHelper = redisHelper;
        }

        public async Task<bool> EnqueueMatchingQueue(long uid)
        {
            double score = 50.0; // 기본값 (전적이 없을 경우)
            string redisKey = "PvpRecord";
            PvpRecord? pvpRecord = null;

            //  Redis 캐시 확인
            string jsonValue = await _redisHelper.GetHashFieldAsync(redisKey, uid.ToString());

            if (string.IsNullOrEmpty(jsonValue))
            {
                // 캐시 없으면 DB 조회
                pvpRecord = await _lobbyRespository.GetPvpRecordByUIDAsync(uid);

                if (pvpRecord != null)
                {
                    var hashEntries = new Dictionary<string, string> { { uid.ToString(), JsonSerializer.Serialize(pvpRecord) } };
                    await _redisHelper.SetHashFieldsAsync(redisKey, hashEntries);
                }
            }
            else
            {
                pvpRecord = JsonSerializer.Deserialize<PvpRecord>(jsonValue);
            }

            if (pvpRecord != null && pvpRecord.Total > 0)
            {
                score = (double)pvpRecord.Win / pvpRecord.Total * 100.0;
            }

            return await _redisHelper.AddZSetAsync(_key, uid, score);
        }

        public async Task<bool> CancelMatchingQueue(long uid)
        {
            return await _redisHelper.RemoveZSetAsync(_key, uid);
        }

        public async Task<bool> CreateRoomAsync(List<long> uidList)
        {
            // 1. 가장 인원이 적은 서버 찾기
            RedisValue[] serverList = await _redisHelper.GetZSetRangeAsync(
                _fieldServerCurrentKey, start: 0, stop: 0, order: Order.Ascending);

            if (serverList.Length < 1)
                return false;

            string targetServerName = serverList[0].ToString();

            // 2. DTO 생성 및 데이터 세팅 (Google.Protobuf 방식)
            Protocol.GameRoomCreateDTO dto = new Protocol.GameRoomCreateDTO();

            // LINQ를 사용해 long을 ulong으로 캐스팅하며 AddRange로 추가합니다.
            dto.UIDList.AddRange(uidList.Select(uid => (ulong)uid));

            // 3. 직렬화 (MemoryStream 없이 초간단하게 변환!)
            byte[] protoBytes = dto.ToByteArray();

            // 4. Redis 큐에 넣기
            string redisKey = $"MatchingQueue:{targetServerName}";
            long count = await _redisHelper.EnqueueKeyValueAsync(redisKey, protoBytes);

            return count > 0;
        }
    }
}