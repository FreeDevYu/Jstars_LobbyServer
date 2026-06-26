namespace FieldStressHarness.FieldNet;

public readonly struct CombatFacing
{
    public CombatFacing(float yawDegrees, float aimX, float aimZ)
    {
        YawDegrees = yawDegrees;
        AimX = aimX;
        AimZ = aimZ;
    }

    public float YawDegrees { get; }
    public float AimX { get; }
    public float AimZ { get; }

    public static readonly CombatFacing Forward = new(0f, 0f, 1f);
    public static readonly CombatFacing Right = new(90f, 1f, 0f);
    public static readonly CombatFacing Back = new(180f, 0f, -1f);
    public static readonly CombatFacing Left = new(270f, -1f, 0f);

    public static CombatFacing ForStep(int stepIndex) =>
        (stepIndex % 4) switch
        {
            0 => Forward,
            1 => Right,
            2 => Back,
            _ => Left,
        };
}
