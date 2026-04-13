using LobbyAPI;
using LobbyAPI.Models;
using LobbyServer.AuthService;
using LobbyServer.Hubs;
using LobbyServer.Repositories;
using ProtoBuf;
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
        private readonly string _fieldServerInfoey = "FIELD_SERVER_INFO";


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
            bool success = false;
            //오름차순(ASC)이므로 0번 인덱스가 가장인원이 적은 서버
            RedisValue[] serverList = await _redisHelper.GetZSetRangeAsync(_fieldServerCurrentKey, start : 0, stop : 0, order : Order.Ascending);
            if (serverList.Length < 1)
                return success;

            string jsonValue = await _redisHelper.GetHashFieldAsync(_fieldServerInfoey, serverList[0].ToString());
            if (jsonValue == null || jsonValue == string.Empty)
                return success;

            var serverInfo = JsonSerializer.Deserialize<FieldServerInfo>(jsonValue);
            if(serverInfo == null) 
                return success;

            GameRoomCreateDTO dto = new GameRoomCreateDTO();
            dto.UIDList = uidList;
            byte[] protoBytes;

            using (var ms = new MemoryStream())
            {
                // Protobuf-net을 사용하여 바이너리로 변환
                Serializer.Serialize(ms, dto);
                protoBytes = ms.ToArray();
            }

            string redisKey = $"MatchingQueue:{serverInfo.Name}";

            long count = await _redisHelper.EnqueueKeyValueAsync(redisKey, protoBytes);
            //-> 이 dto를 체크한 iocp필드서버가 signalRHub로 방생성완료 패킷 보낼예정. 

            return success;
        }
    }
}