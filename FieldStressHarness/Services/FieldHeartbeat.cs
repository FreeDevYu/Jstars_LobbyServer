using FieldStressHarness.FieldNet;
using protocol;

namespace FieldStressHarness.Services;

internal static class FieldHeartbeat
{
    public static void TrySend(FieldTcpSession field, int heartbeatIntervalMs, ref DateTime nextHeartbeatAt)
    {
        if (!field.IsConnected || DateTime.UtcNow < nextHeartbeatAt)
        {
            return;
        }

        field.Send(FieldPacketBuilder.RequestHeartbeat(Environment.TickCount64));
        nextHeartbeatAt = DateTime.UtcNow.AddMilliseconds(heartbeatIntervalMs);
    }

    public static bool IsDisconnected(FieldTcpSession field) => !field.IsConnected;
}
