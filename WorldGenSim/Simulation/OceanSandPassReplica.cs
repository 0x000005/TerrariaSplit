namespace WorldGenSim.Simulation;

internal static class OceanSandPassReplica
{
    public static void Apply(WorldGenContext context, GenerationProgress progress)
    {
        WorldGenState state = context.State ??
            throw new InvalidOperationException("Ocean Sand replica requires a WorldGenState.");
        if (!state.Options.IsTargetScope)
        {
            throw new InvalidOperationException(state.Options.TargetScopeDetail());
        }

        UnifiedRandom random = context.Random;
        int width = state.Options.Dimensions.Width;
        double widthScale = width / 4200.0;

        for (int i = 0; i < 3; i++)
        {
            progress.Set((float)i / 3f);
            int center = random.Next(width);
            while (center > width * 0.4 && center < width * 0.6)
            {
                center = random.Next(width);
            }

            int span = random.Next(35, 90);
            if (i == 1)
            {
                span += (int)(random.Next(20, 40) * widthScale);
            }

            if (random.Next(3) == 0)
            {
                span *= 2;
            }

            if (i == 1)
            {
                span *= 2;
            }

            int left = center - span;

            span = random.Next(35, 90);
            if (random.Next(3) == 0)
            {
                span *= 2;
            }

            if (i == 1)
            {
                span *= 2;
            }

            int right = center + span;
            if (left < 0)
            {
                left = 0;
            }

            if (right > width)
            {
                right = width;
            }

            if (i == 0)
            {
                left = 0;
                right = state.LeftBeachEnd;
            }
            else if (i == 2)
            {
                left = state.RightBeachStart;
                right = width;
            }
            else if (i == 1)
            {
                continue;
            }

            int sandDepth = random.Next(50, 100);
            for (int x = left; x < right; x++)
            {
                if (random.Next(2) == 0)
                {
                    sandDepth += random.Next(-1, 2);
                    if (sandDepth < 50)
                    {
                        sandDepth = 50;
                    }

                    if (sandDepth > 200)
                    {
                        sandDepth = 200;
                    }
                }

                for (int y = 0; y < (state.MainWorldSurface + state.MainRockLayer) / 2.0; y++)
                {
                    if (!state.Tiles[x, y].Active)
                    {
                        continue;
                    }

                    if (x == (left + right) / 2 && random.Next(6) == 0)
                    {
                        state.AddPyramidCandidate(x, y, sourceIndex: -1);
                    }

                    int depth = sandDepth;
                    if (x - left < depth)
                    {
                        depth = x - left;
                    }

                    if (right - x < depth)
                    {
                        depth = right - x;
                    }

                    depth += random.Next(5);
                    for (int sandY = y; sandY < y + depth && sandY < state.Options.Dimensions.Height; sandY++)
                    {
                        if (x > left + random.Next(5) && x < right - random.Next(5))
                        {
                            state.Tiles[x, sandY].Type = TileIds.Sand;
                        }
                    }

                    break;
                }
            }
        }

        state.OceanSandApplied = true;
    }
}
