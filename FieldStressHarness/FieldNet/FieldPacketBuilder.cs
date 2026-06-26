using Google.FlatBuffers;
using protocol;

namespace FieldStressHarness.FieldNet;

public static class FieldPacketBuilder
{
    public static byte[] BuildMessage(Content contentType, Action<FlatBufferBuilder> buildBody)
    {
        var builder = new FlatBufferBuilder(256);
        buildBody(builder);

        byte[] bodyBytes = builder.SizedByteArray();
        var header = new MessageHeader((uint)bodyBytes.Length, (uint)contentType);
        byte[] headerBytes = header.ToBytes();

        byte[] messageBytes = new byte[bodyBytes.Length + NetworkDefine.NetworkHeaderSize];
        Buffer.BlockCopy(headerBytes, 0, messageBytes, 0, NetworkDefine.NetworkHeaderSize);
        Buffer.BlockCopy(bodyBytes, 0, messageBytes, NetworkDefine.NetworkHeaderSize, bodyBytes.Length);
        return messageBytes;
    }

    public static byte[] RequestAuth(long uid, int roomId, string token) =>
        BuildMessage(Content.REQUEST_AUTH, builder =>
        {
            var tokenOffset = builder.CreateString(token);
            var data = REQUEST_AUTH.CreateREQUEST_AUTH(builder, uid, roomId, tokenOffset);
            builder.Finish(data.Value);
        });

    public static byte[] RequestEnterGameRoom(long uid, int roomId, bool isReady) =>
        BuildMessage(Content.REQUEST_ENTER_GAMEROOM, builder =>
        {
            var data = REQUEST_ENTER_GAMEROOM.CreateREQUEST_ENTER_GAMEROOM(builder, uid, roomId, isReady);
            builder.Finish(data.Value);
        });

    public static byte[] RequestHeartbeat(long clientSendTime) =>
        BuildMessage(Content.REQUEST_HEARTBEAT, builder =>
        {
            var data = REQUEST_HEARTBEAT.CreateREQUEST_HEARTBEAT(builder, clientSendTime);
            builder.Finish(data.Value);
        });

    public static byte[] RequestPlayerMove(float px, float py, float pz, float rx, float ry, float rz, float vx, float vy, float vz) =>
        BuildMessage(Content.REQUEST_PLAYER_MOVE, builder =>
        {
            REQUEST_PLAYER_MOVE.StartREQUEST_PLAYER_MOVE(builder);
            REQUEST_PLAYER_MOVE.AddPosition(builder, Vec3.CreateVec3(builder, px, py, pz));
            REQUEST_PLAYER_MOVE.AddRotation(builder, Vec3.CreateVec3(builder, rx, ry, rz));
            REQUEST_PLAYER_MOVE.AddVelocity(builder, Vec3.CreateVec3(builder, vx, vy, vz));
            var endOffset = REQUEST_PLAYER_MOVE.EndREQUEST_PLAYER_MOVE(builder);
            builder.Finish(endOffset.Value);
        });

    public static byte[] RequestPlayerShot(float px, float py, float pz, int weaponId, float ax, float ay, float az) =>
        BuildMessage(Content.REQUEST_PLAYER_SHOT, builder =>
        {
            REQUEST_PLAYER_SHOT.StartREQUEST_PLAYER_SHOT(builder);
            REQUEST_PLAYER_SHOT.AddPosition(builder, Vec3.CreateVec3(builder, px, py, pz));
            REQUEST_PLAYER_SHOT.AddWeaponId(builder, weaponId);
            REQUEST_PLAYER_SHOT.AddAimDirection(builder, Vec3.CreateVec3(builder, ax, ay, az));
            var endOffset = REQUEST_PLAYER_SHOT.EndREQUEST_PLAYER_SHOT(builder);
            builder.Finish(endOffset.Value);
        });

    public static byte[] RequestPlayerSpecialShot(float px, float py, float pz, int weaponId, float ax, float ay, float az) =>
        BuildMessage(Content.REQUEST_PLAYER_SPECIALSHOT, builder =>
        {
            REQUEST_PLAYER_SPECIALSHOT.StartREQUEST_PLAYER_SPECIALSHOT(builder);
            REQUEST_PLAYER_SPECIALSHOT.AddAimDirection(builder, Vec3.CreateVec3(builder, ax, ay, az));
            REQUEST_PLAYER_SPECIALSHOT.AddWeaponId(builder, weaponId);
            REQUEST_PLAYER_SPECIALSHOT.AddShooterPosition(builder, Vec3.CreateVec3(builder, px, py, pz));
            var endOffset = REQUEST_PLAYER_SPECIALSHOT.EndREQUEST_PLAYER_SPECIALSHOT(builder);
            builder.Finish(endOffset.Value);
        });

    public static byte[] RequestReload(long userKey) =>
        BuildMessage(Content.REQUEST_RELOAD, builder =>
        {
            var data = REQUEST_RELOAD.CreateREQUEST_RELOAD(builder, userKey);
            builder.Finish(data.Value);
        });
}
