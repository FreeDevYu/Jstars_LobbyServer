namespace FieldStressHarness.FieldNet;

public static class SpawnPositions
{
    private static readonly Dictionary<int, (float X, float Y, float Z)> Positions = new()
    {
        [1] = (19.9f, 0.5f, -0.6f),
        [2] = (-19.7f, 0.5f, -0.6f),
        [3] = (1.9f, 0.5f, 21.4f),
        [4] = (-2.9f, 0.5f, 5.1f),
        [5] = (4.4f, 0.5f, -5.5f),
    };

    public static (float X, float Y, float Z) Resolve(int spawnPositionId)
    {
        if (Positions.TryGetValue(spawnPositionId, out var position))
        {
            return position;
        }

        return Positions[1];
    }
}
