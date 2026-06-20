namespace TerrariaSplit;

internal readonly record struct ItemIconAnimation(int FrameCount);

internal static class ItemIconAnimationCatalog
{
    private static readonly int[] FoodItemIds =
    [
        353,
        357,
        1787,
        1911,
        1912,
        1919,
        1920,
        2266,
        2267,
        2268,
        2425,
        2426,
        2427,
        3195,
        3532,
        4009,
        4010,
        4011,
        4012,
        4013,
        4014,
        4015,
        4016,
        4017,
        4018,
        4019,
        4020,
        4021,
        4022,
        4023,
        4024,
        4025,
        4026,
        4027,
        4028,
        4029,
        4030,
        4031,
        4032,
        4033,
        4034,
        4035,
        4036,
        4037,
        967,
        969,
        4282,
        4283,
        4284,
        4285,
        4286,
        4287,
        4288,
        4289,
        4290,
        4291,
        4292,
        4293,
        4294,
        4295,
        4296,
        4297,
        4403,
        4411,
        4614,
        4615,
        4616,
        4617,
        4618,
        4619,
        4620,
        4621,
        4622,
        4623,
        4624,
        4625,
        5009,
        5042,
        5041,
        5092,
        5093,
        5275,
        5277,
        5278,
        5537,
        5645
    ];

    private static readonly IReadOnlyDictionary<int, ItemIconAnimation> Animations =
        CreateAnimations();

    public static bool TryGetAnimation(int itemId, out ItemIconAnimation animation)
    {
        return Animations.TryGetValue(itemId, out animation);
    }

    private static Dictionary<int, ItemIconAnimation> CreateAnimations()
    {
        // Mirrors Terraria 1.4.5.6 Main.InitializeItemAnimations. Item textures
        // are vertical sprite sheets; Terraria draws one frame and trims the
        // 2px separator at the bottom of each frame.
        var animations = new Dictionary<int, ItemIconAnimation>
        {
            [3581] = new(4),
            [3580] = new(4),
            [75] = new(8),
            [575] = new(4),
            [547] = new(4),
            [520] = new(4),
            [548] = new(4),
            [521] = new(4),
            [549] = new(4),
            [3453] = new(4),
            [3454] = new(4),
            [3455] = new(4),
            [4068] = new(4),
            [4069] = new(4),
            [4070] = new(4),
            [5644] = new(9)
        };

        foreach (int itemId in FoodItemIds)
        {
            animations[itemId] = new(3);
        }

        return animations;
    }
}
