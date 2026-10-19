using System;
using Valari.FoundryTraining.Controller;

// Console test runner for the Unity-free trainer walk monitor (no test framework, no packages).
// Run from the repo root:  dotnet run --project Tests/TrainerLogic
internal static class Program
{
    private static int _passed;
    private static int _failed;

    private static int Main()
    {
        Run("Walk: ends as arrived inside the stopping distance", WalkArrivesInsideStoppingDistance);
        Run("Walk: keeps going while it makes progress", WalkContinuesWhileProgressing);
        Run("Walk: ends as stalled after no progress for the stall time", WalkStallsWithoutProgress);
        Run("Walk: jitter around the stopping distance counts as a stall, not progress", WalkJitterStalls);
        Run("Walk: progress restarts the stall timer", WalkProgressRestartsStallTimer);
        Run("Walk: a pending path never stalls but still times out", WalkPendingPathTimesOut);
        Run("Walk: ends as timed out even while still progressing", WalkTimesOutWhileProgressing);
        Run("Walk: an unknown (infinite or NaN) distance never arrives but stalls", WalkUnknownDistanceStalls);
        Run("Walk: once ended it stays ended with the same reason", WalkStaysEnded);

        Console.WriteLine();
        Console.WriteLine($"{_passed} passed, {_failed} failed");
        return _failed == 0 ? 0 : 1;
    }

    private static void Run(string name, Action test)
    {
        try
        {
            test();
            _passed++;
            Console.WriteLine("PASS  " + name);
        }
        catch (Exception e)
        {
            _failed++;
            Console.WriteLine("FAIL  " + name + " -> " + e.Message);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual)) throw new Exception($"{message}: expected {expected}, got {actual}");
    }

    private const float Stop = 2.5f;
    private const float Step = 0.1f;

    private static TrainerWalkMonitor NewMonitor() => new TrainerWalkMonitor(stallSeconds: 2f, minProgress: 0.05f, timeoutSeconds: 20f);

    private static void WalkArrivesInsideStoppingDistance()
    {
        var walk = NewMonitor();
        Assert(!walk.Update(5f, Stop, false, Step), "far away");
        Assert(walk.Update(2.52f, Stop, false, Step), "within the 5 cm tolerance");
        AssertEqual(TrainerWalkEnd.Arrived, walk.End, "reason");
    }

    private static void WalkContinuesWhileProgressing()
    {
        var walk = NewMonitor();
        float remaining = 10f;
        for (int i = 0; i < 50; i++)
        {
            remaining -= 0.1f; // 1 m/s
            Assert(!walk.Update(remaining, Stop, false, Step), $"still walking at {remaining:0.0} m");
        }

        AssertEqual(TrainerWalkEnd.None, walk.End, "not ended");
    }

    private static void WalkStallsWithoutProgress()
    {
        var walk = NewMonitor();
        bool ended = false;
        int updates = 0;
        while (!ended && updates < 1000)
        {
            ended = walk.Update(4f, Stop, false, Step);
            updates++;
        }

        AssertEqual(TrainerWalkEnd.Stalled, walk.End, "reason");
        Assert(updates >= 19 && updates <= 21, $"stalled after about 2 s (updates {updates})");
    }

    // The 2026-10-06 bug: a pushed trainer hovered just outside the stopping distance and the walk never ended.
    private static void WalkJitterStalls()
    {
        var walk = NewMonitor();
        bool ended = false;
        for (int i = 0; i < 40 && !ended; i++)
            ended = walk.Update(i % 2 == 0 ? 2.58f : 2.61f, Stop, false, Step);
        Assert(ended, "jitter ends the walk");
        AssertEqual(TrainerWalkEnd.Stalled, walk.End, "reason");
    }

    private static void WalkProgressRestartsStallTimer()
    {
        var walk = NewMonitor();
        for (int i = 0; i < 15; i++) walk.Update(4f, Stop, false, Step); // 1.5 s without progress
        Assert(!walk.Update(3.9f, Stop, false, Step), "10 cm of progress");
        for (int i = 0; i < 15; i++)
            Assert(!walk.Update(3.9f, Stop, false, Step), "stall timer restarted");
    }

    private static void WalkPendingPathTimesOut()
    {
        var walk = NewMonitor();
        bool ended = false;
        int updates = 0;
        while (!ended && updates < 1000)
        {
            ended = walk.Update(float.PositiveInfinity, Stop, true, Step);
            updates++;
        }

        AssertEqual(TrainerWalkEnd.TimedOut, walk.End, "reason");
        Assert(updates >= 199 && updates <= 201, $"ended at the 20 s timeout, not before (updates {updates})");
    }

    private static void WalkTimesOutWhileProgressing()
    {
        var walk = new TrainerWalkMonitor(stallSeconds: 2f, minProgress: 0.05f, timeoutSeconds: 3f);
        float remaining = 100f;
        bool ended = false;
        int updates = 0;
        while (!ended && updates < 1000)
        {
            remaining -= 0.1f;
            ended = walk.Update(remaining, Stop, false, Step);
            updates++;
        }

        AssertEqual(TrainerWalkEnd.TimedOut, walk.End, "reason");
        Assert(updates >= 29 && updates <= 31, $"ended at 3 s (updates {updates})");
    }

    private static void WalkUnknownDistanceStalls()
    {
        foreach (float unknown in new[] { float.PositiveInfinity, float.NaN })
        {
            var walk = NewMonitor();
            bool ended = false;
            int updates = 0;
            while (!ended && updates < 1000)
            {
                ended = walk.Update(unknown, Stop, false, Step);
                updates++;
            }

            AssertEqual(TrainerWalkEnd.Stalled, walk.End, $"reason for {unknown}");
            Assert(updates <= 21, $"stalled after about 2 s for {unknown} (updates {updates})");
        }
    }

    private static void WalkStaysEnded()
    {
        var walk = NewMonitor();
        walk.Update(1f, Stop, false, Step);
        Assert(walk.Update(50f, Stop, false, Step), "still ended");
        AssertEqual(TrainerWalkEnd.Arrived, walk.End, "reason unchanged");
    }
}
