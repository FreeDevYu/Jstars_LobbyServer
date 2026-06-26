using FieldStressHarness.FieldNet;
using FieldStressHarness.Models;
using protocol;

namespace FieldStressHarness.Services;

public static class CombatBasicLoop
{
    private static readonly string[] StepNames = ["forward+shot", "right+shot", "back+shot", "left+special", "reload"];

    public static async Task RunAsync(
        string label,
        FieldTcpSession field,
        long uid,
        int weaponSubcategory,
        float px,
        float py,
        float pz,
        HarnessConfig config,
        CancellationToken cancellationToken)
    {
        var sessionEndAt = DateTime.UtcNow.AddSeconds(config.FieldSessionSeconds);
        var nextHeartbeatAt = DateTime.UtcNow;
        var nextStepAt = DateTime.UtcNow;
        int step = 0;

        HarnessLog.Info(label, "combat_basic started (5-step smoke loop)");

        while (DateTime.UtcNow < sessionEndAt && !cancellationToken.IsCancellationRequested)
        {
            if (field.TryTake(Content.NOTICE_GAME_END, out _))
            {
                HarnessLog.Info(label, "NOTICE_GAME_END");
                return;
            }

            if (DateTime.UtcNow >= nextHeartbeatAt)
            {
                field.Send(FieldPacketBuilder.RequestHeartbeat(Environment.TickCount64));
                nextHeartbeatAt = DateTime.UtcNow.AddMilliseconds(config.HeartbeatIntervalMs);
            }

            if (DateTime.UtcNow >= nextStepAt)
            {
                int cycleStep = step % 5;
                string stepName = StepNames[cycleStep];
                CombatFacing facing = CombatFacing.ForStep(cycleStep);

                field.Send(FieldPacketBuilder.RequestPlayerMove(
                    px, py, pz,
                    0f, facing.YawDegrees, 0f,
                    0f, 0f, 0f));

                switch (cycleStep)
                {
                    case 0:
                    case 1:
                    case 2:
                        field.Send(FieldPacketBuilder.RequestPlayerShot(
                            px, py, pz, weaponSubcategory,
                            facing.AimX, 0f, facing.AimZ));
                        break;
                    case 3:
                        field.Send(FieldPacketBuilder.RequestPlayerSpecialShot(
                            px, py, pz, weaponSubcategory,
                            facing.AimX, 0f, facing.AimZ));
                        break;
                    default:
                        field.Send(FieldPacketBuilder.RequestReload(uid));
                        break;
                }

                HarnessLog.Info(label, $"combat_basic step={cycleStep + 1}/5 ({stepName})");
                step++;
                nextStepAt = DateTime.UtcNow.AddMilliseconds(config.CombatBasicStepIntervalMs);
            }

            await Task.Delay(50, cancellationToken);
        }

        HarnessLog.Info(label, $"combat_basic finished ({config.FieldSessionSeconds}s)");
    }
}
