using System.Text.Json;

// Minimal reproducible headless smoke test. This is NOT TBSv3 gameplay logic.
internal static class Program
{
    private static int Main(string[] args)
    {
        int scenarios = args.Length > 0 && int.TryParse(args[0], out int count) ? count : 1000;
        Directory.CreateDirectory("test-results");
        int executed = 0;
        try
        {
            if (scenarios < 1 || scenarios > 100_000)
                throw new ArgumentOutOfRangeException(nameof(scenarios));

            for (int i = 0; i < scenarios; i++)
            {
                uint seed = unchecked(0x9E3779B9u + (uint)i * 2654435761u);
                var a = Run(seed);
                var b = Run(seed);
                if (a != b)
                    throw new InvalidOperationException($"Non-deterministic replay: scenario {i}, seed {seed}, A={a}, B={b}");
                executed++;
            }

            string report = JsonSerializer.Serialize(new
            {
                status = "passed", scenarios = executed, ticksPerScenario = 128,
                totalTicks = executed * 128, deterministicReplay = true,
                runner = "standalone-dotnet8-smoke"
            }, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText("test-results/report.json", report);
            Console.WriteLine(report);
            return 0;
        }
        catch (Exception ex)
        {
            File.WriteAllText("test-results/replay-failure.json", JsonSerializer.Serialize(new
            {
                status = "failed", executed, exception = ex.ToString(),
                runner = "standalone-dotnet8-smoke"
            }, new JsonSerializerOptions { WriteIndented = true }));
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static (int X, int Y, int Ammo, uint Rng) Run(uint seed)
    {
        uint rng = seed == 0 ? 1u : seed;
        int x = 0, y = 0, ammo = 30;
        for (int tick = 0; tick < 128; tick++)
        {
            int oldX = x, oldY = y;
            uint roll = Next(ref rng);
            if (roll % 7 != 0)
            {
                switch ((roll / 7) % 4)
                {
                    case 0: x++; break;
                    case 1: x--; break;
                    case 2: y++; break;
                    default: y--; break;
                }
            }
            if (tick % 11 == 0 && ammo > 0)
                ammo--;

            if (Math.Abs(x - oldX) + Math.Abs(y - oldY) > 1)
                throw new InvalidOperationException($"Movement invariant broken at tick {tick}");
            if (ammo < 0 || ammo > 30)
                throw new InvalidOperationException($"Ammunition invariant broken at tick {tick}");
        }
        return (x, y, ammo, rng);
    }

    private static uint Next(ref uint state)
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return state;
    }
}
