using System;
using ImmersiveTraining.TrainingInteractions;

// Console test runner for the Unity-free Packing and Dispatch (M2) rules (no test framework, no packages).
// Run from the repo root:  dotnet run --project Tests/PackingDispatchLogic
internal static class Program
{
    private static int _passed;
    private static int _failed;

    private static int Main()
    {
        Run("Fit: usable size is the inner size less 5 cm on every side", FitUsableSizes);
        Run("Fit: the speaker needs Medium although it physically fits Small", FitSpeakerNeedsMedium);
        Run("Fit: the three-item order needs Large", FitThreeItemsNeedLarge);
        Run("Fit: orientation does not matter", FitIgnoresOrientation);
        Run("Fit: a size exactly at the usable limit fits", FitExactLimitFits);
        Run("Fit: oversized and undersized choices are judged as such", FitJudgesWrongChoices);
        Run("Fit: when nothing fits every carton is too small", FitNothingFits);
        Run("Fit: NaN never fits", FitNaNNeverFits);

        Run("Fill: an item is refused until the base layer is in", FillItemNeedsBase);
        Run("Fill: phases run base, items, top, full", FillPhasesInOrder);
        Run("Fill: pillows are refused while items are missing and once full", FillRefusesOutOfPhase);
        Run("Fill: every item must be in before the top fill", FillAllItemsBeforeTop);
        Run("Fill: an item beyond the order is refused", FillRefusesExtraItem);
        Run("Fill: a pre-packed under-filled box needs only top fill", FillPrePackedNeedsTop);
        Run("Fill: no items required goes straight to the top fill", FillNoItems);
        Run("Fill: fill height grows to the base share, then to the full share", FillHeightShares);

        Run("Seam: marks bins and completes at the threshold", SeamCompletesAtThreshold);
        Run("Seam: the same bin counts once", SeamCountsBinOnce);
        Run("Seam: points off the seam are ignored", SeamIgnoresOutside);
        Run("Seam: reset clears the coverage", SeamReset);

        Run("Segment: closest parameter and distance", SegmentClosest);
        Run("Segment: points beyond an end measure to that end", SegmentBeyondEnd);
        Run("Segment: a zero-length segment measures to its point", SegmentDegenerate);

        Run("Gaze: found after the dwell time", GazeFoundAfterDwell);
        Run("Gaze: looking away restarts the dwell", GazeLookAwayRestarts);
        Run("Gaze: too far or too wide never finds", GazeOutOfRange);
        Run("Gaze: NaN never finds and found reports once", GazeNaNAndOnce);
        Run("Gaze: angle between directions", GazeAngles);

        Run("Orientation: label-up tolerance", OrientationFaceUp);
        Run("Region: inside, outside, tolerance and NaN", RegionContains);

        Run("M2_04: a base filled before the step opens completes it when it arms", ScenarioEarlyBaseFillCompletesOnArm);

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

    private static void AssertNear(float expected, float actual, string message, float epsilon = 1e-4f)
    {
        if (!(Math.Abs(expected - actual) <= epsilon)) throw new Exception($"{message}: expected {expected}, got {actual}");
    }

    private static void AssertSize(Size3 size, float x, float y, float z, string message)
    {
        AssertNear(x, size.X, message + " X");
        AssertNear(y, size.Y, message + " Y");
        AssertNear(z, size.Z, message + " Z");
    }

    // ---- Fit ----

    private static void FitUsableSizes()
    {
        AssertSize(CartonFit.UsableSize(CartonSize.Small), 20f, 5f, 10f, "Small");
        AssertSize(CartonFit.UsableSize(CartonSize.Medium), 35f, 15f, 25f, "Medium");
        AssertSize(CartonFit.UsableSize(CartonSize.Large), 50f, 30f, 40f, "Large");
    }

    private static void FitSpeakerNeedsMedium()
    {
        var speaker = new Size3(24f, 10f, 14f);
        Assert(!CartonFit.Fits(speaker, CartonSize.Small), "speaker must not fit Small with clearance");
        AssertEqual<CartonSize?>(CartonSize.Medium, CartonFit.SmallestFit(speaker), "smallest fit");
        AssertEqual(CartonVerdict.Correct, CartonFit.Judge(CartonSize.Medium, speaker), "Medium verdict");
    }

    private static void FitThreeItemsNeedLarge()
    {
        var order = new Size3(32f, 14f, 30f);
        Assert(!CartonFit.Fits(order, CartonSize.Medium), "Medium is too narrow");
        AssertEqual<CartonSize?>(CartonSize.Large, CartonFit.SmallestFit(order), "smallest fit");
    }

    private static void FitIgnoresOrientation()
    {
        Assert(CartonFit.Fits(new Size3(10f, 24f, 14f), CartonSize.Medium), "turned one way");
        Assert(CartonFit.Fits(new Size3(14f, 10f, 24f), CartonSize.Medium), "turned another way");
    }

    private static void FitExactLimitFits()
    {
        Assert(CartonFit.Fits(new Size3(35f, 25f, 15f), CartonSize.Medium), "exact usable size fits");
        Assert(!CartonFit.Fits(new Size3(35.1f, 25f, 15f), CartonSize.Medium), "a millimetre over does not");
    }

    private static void FitJudgesWrongChoices()
    {
        var speaker = new Size3(24f, 10f, 14f);
        AssertEqual(CartonVerdict.TooSmall, CartonFit.Judge(CartonSize.Small, speaker), "Small");
        AssertEqual(CartonVerdict.Oversized, CartonFit.Judge(CartonSize.Large, speaker), "Large");
    }

    private static void FitNothingFits()
    {
        var huge = new Size3(80f, 10f, 10f);
        AssertEqual<CartonSize?>(null, CartonFit.SmallestFit(huge), "no carton fits");
        foreach (CartonSize size in new[] { CartonSize.Small, CartonSize.Medium, CartonSize.Large })
            AssertEqual(CartonVerdict.TooSmall, CartonFit.Judge(size, huge), size.ToString());
    }

    private static void FitNaNNeverFits()
    {
        Assert(!CartonFit.Fits(new Size3(float.NaN, 1f, 1f), CartonSize.Large), "NaN fits nothing");
    }

    // ---- Fill ----

    private static void FillItemNeedsBase()
    {
        var fill = new VoidFillState(2, 3, 1);
        AssertEqual(ItemResult.NeedsBaseFill, fill.TryAddItem(), "item on an empty box");
        AssertEqual(FillResult.Added, fill.TryAddFill(), "pillow 1");
        AssertEqual(ItemResult.NeedsBaseFill, fill.TryAddItem(), "item on one pillow");
        AssertEqual(FillResult.Added, fill.TryAddFill(), "pillow 2");
        Assert(fill.IsBaseMet, "base met");
        AssertEqual(ItemResult.Added, fill.TryAddItem(), "item on the base");
    }

    private static void FillPhasesInOrder()
    {
        var fill = new VoidFillState(2, 3, 1);
        AssertEqual(FillPhase.Base, fill.Phase, "empty");
        fill.TryAddFill();
        fill.TryAddFill();
        AssertEqual(FillPhase.Items, fill.Phase, "after base");
        fill.TryAddItem();
        AssertEqual(FillPhase.Top, fill.Phase, "after item");
        for (int i = 0; i < 3; i++) fill.TryAddFill();
        AssertEqual(FillPhase.Full, fill.Phase, "after top");
        Assert(fill.IsFull, "IsFull");
        AssertEqual(5, fill.Fill, "fill count");
    }

    private static void FillRefusesOutOfPhase()
    {
        var fill = new VoidFillState(2, 3, 1);
        fill.TryAddFill();
        fill.TryAddFill();
        AssertEqual(FillResult.PlaceItemsFirst, fill.TryAddFill(), "pillow while the item is missing");
        AssertEqual(2, fill.Fill, "refused pillow not counted");
        fill.TryAddItem();
        for (int i = 0; i < 3; i++) fill.TryAddFill();
        AssertEqual(FillResult.AlreadyFull, fill.TryAddFill(), "pillow into a full box");
        AssertEqual(5, fill.Fill, "still full, not over");
    }

    private static void FillAllItemsBeforeTop()
    {
        var fill = new VoidFillState(2, 3, 3);
        fill.TryAddFill();
        fill.TryAddFill();
        fill.TryAddItem();
        AssertEqual(FillResult.PlaceItemsFirst, fill.TryAddFill(), "top fill waits for all three items");
        fill.TryAddItem();
        fill.TryAddItem();
        AssertEqual(FillResult.Added, fill.TryAddFill(), "top fill once all items are in");
        Assert(!fill.IsFull, "not full yet");
    }

    private static void FillRefusesExtraItem()
    {
        var fill = new VoidFillState(2, 3, 1);
        fill.TryAddFill();
        fill.TryAddFill();
        fill.TryAddItem();
        AssertEqual(ItemResult.NoRoom, fill.TryAddItem(), "second item");
        AssertEqual(1, fill.ItemsPlaced, "one item");
    }

    private static void FillPrePackedNeedsTop()
    {
        var fill = new VoidFillState(2, 3, 1);
        fill.Set(1, 1);
        AssertEqual(FillPhase.Top, fill.Phase, "under-filled pre-packed box takes top fill");
        int added = 0;
        while (fill.TryAddFill() == FillResult.Added) added++;
        AssertEqual(4, added, "pillows to full");
        Assert(fill.IsFull, "full");
    }

    private static void FillNoItems()
    {
        var fill = new VoidFillState(2, 3, 0);
        AssertEqual(FillPhase.Top, fill.Phase, "no items: top fill straight away");
        AssertEqual(ItemResult.NoRoom, fill.TryAddItem(), "no item accepted");
    }

    private static void FillHeightShares()
    {
        var fill = new VoidFillState(2, 3, 1);
        AssertNear(0f, fill.FillHeight(0.15f, 0.9f), "empty");
        fill.TryAddFill();
        AssertNear(0.075f, fill.FillHeight(0.15f, 0.9f), "half base");
        fill.TryAddFill();
        AssertNear(0.15f, fill.FillHeight(0.15f, 0.9f), "base");
        fill.TryAddItem();
        fill.TryAddFill();
        AssertNear(0.4f, fill.FillHeight(0.15f, 0.9f), "one third of the top");
        fill.TryAddFill();
        fill.TryAddFill();
        AssertNear(0.9f, fill.FillHeight(0.15f, 0.9f), "full");
    }

    // ---- Seam ----

    private static void SeamCompletesAtThreshold()
    {
        var seam = new SeamCoverage(12, 0.85f);
        for (int i = 0; i < 10; i++) seam.Mark((i + 0.5f) / 12f);
        Assert(!seam.IsComplete, "10 of 12 bins is not enough");
        seam.Mark(10.5f / 12f);
        Assert(seam.IsComplete, "11 of 12 bins completes");
        AssertNear(11f / 12f, seam.Fraction, "fraction");
    }

    private static void SeamCountsBinOnce()
    {
        var seam = new SeamCoverage(12, 0.85f);
        Assert(seam.Mark(0.01f), "first mark");
        Assert(!seam.Mark(0.02f), "same bin again");
        AssertNear(1f / 12f, seam.Fraction, "one bin");
    }

    private static void SeamIgnoresOutside()
    {
        var seam = new SeamCoverage(12, 0.85f);
        Assert(!seam.Mark(-0.1f), "before the start");
        Assert(!seam.Mark(1.2f), "past the end");
        Assert(!seam.Mark(float.NaN), "NaN");
        Assert(seam.Mark(1f), "the very end counts");
        AssertNear(1f / 12f, seam.Fraction, "one bin");
    }

    private static void SeamReset()
    {
        var seam = new SeamCoverage(4, 0.85f);
        for (int i = 0; i < 4; i++) seam.Mark(i / 4f + 0.1f);
        Assert(seam.IsComplete, "complete");
        seam.Reset();
        AssertNear(0f, seam.Fraction, "cleared");
    }

    // ---- Segment ----

    private static void SegmentClosest()
    {
        float t = SegmentMath.Closest(new Vec3(0.5f, 0.1f, 0f), new Vec3(0f, 0f, 0f), new Vec3(1f, 0f, 0f), out float distance);
        AssertNear(0.5f, t, "parameter");
        AssertNear(0.1f, distance, "distance");
    }

    private static void SegmentBeyondEnd()
    {
        float t = SegmentMath.Closest(new Vec3(1.5f, 0f, 0f), new Vec3(0f, 0f, 0f), new Vec3(1f, 0f, 0f), out float distance);
        AssertNear(1.5f, t, "unclamped parameter");
        AssertNear(0.5f, distance, "distance to the end");
    }

    private static void SegmentDegenerate()
    {
        var a = new Vec3(0f, 0f, 0f);
        float t = SegmentMath.Closest(new Vec3(0f, 3f, 4f), a, a, out float distance);
        AssertNear(0f, t, "parameter");
        AssertNear(5f, distance, "distance");
    }

    // ---- Gaze ----

    private static void GazeFoundAfterDwell()
    {
        var gaze = new GazeDwell(12f, 4.5f, 0.5f);
        Assert(!gaze.Update(5f, 2f, 0.3f), "not yet at 0.3 s");
        Assert(gaze.Update(5f, 2f, 0.3f), "found at 0.6 s");
        Assert(gaze.Found, "Found");
    }

    private static void GazeLookAwayRestarts()
    {
        var gaze = new GazeDwell(12f, 4.5f, 0.5f);
        gaze.Update(5f, 2f, 0.4f);
        gaze.Update(30f, 2f, 0.1f);
        Assert(!gaze.Update(5f, 2f, 0.4f), "dwell restarted after looking away");
        Assert(gaze.Update(5f, 2f, 0.2f), "found after a full dwell");
    }

    private static void GazeOutOfRange()
    {
        var gaze = new GazeDwell(12f, 4.5f, 0.5f);
        Assert(!gaze.Update(13f, 2f, 1f), "outside the angle");
        Assert(!gaze.Update(5f, 4.6f, 1f), "too far");
        Assert(!gaze.Found, "never found");
    }

    private static void GazeNaNAndOnce()
    {
        var gaze = new GazeDwell(12f, 4.5f, 0.5f);
        Assert(!gaze.Update(float.NaN, 1f, 1f), "NaN angle");
        Assert(gaze.Update(1f, 1f, 1f), "found");
        Assert(!gaze.Update(1f, 1f, 1f), "reported once");
        gaze.Reset();
        Assert(!gaze.Found, "reset");
    }

    private static void GazeAngles()
    {
        AssertNear(0f, GazeDwell.AngleBetween(new Vec3(0f, 0f, 1f), new Vec3(0f, 0f, 5f)), "same direction", 1e-3f);
        AssertNear(90f, GazeDwell.AngleBetween(new Vec3(1f, 0f, 0f), new Vec3(0f, 1f, 0f)), "right angle", 1e-3f);
        AssertNear(180f, GazeDwell.AngleBetween(new Vec3(0f, 0f, 1f), new Vec3(0f, 0f, -2f)), "opposite", 1e-3f);
        Assert(float.IsNaN(GazeDwell.AngleBetween(new Vec3(0f, 0f, 0f), new Vec3(1f, 0f, 0f))), "zero vector");
    }

    // ---- Orientation / region ----

    private static void OrientationFaceUp()
    {
        float Cos(double degrees) => (float)Math.Cos(degrees * Math.PI / 180.0);
        Assert(Orientation.IsFaceUp(1f, 35f), "flat");
        Assert(Orientation.IsFaceUp(Cos(30), 35f), "30 degrees");
        Assert(Orientation.IsFaceUp(Cos(35), 35f), "35 degrees (edge)");
        Assert(!Orientation.IsFaceUp(Cos(40), 35f), "40 degrees");
        Assert(!Orientation.IsFaceUp(-1f, 35f), "upside down");
        Assert(!Orientation.IsFaceUp(0f, 35f), "on its side");
        Assert(!Orientation.IsFaceUp(float.NaN, 35f), "NaN");
    }

    private static void RegionContains()
    {
        var region = new BoxRegion(new Vec3(0f, 0.1f, 0f), new Vec3(0.3f, 0.2f, 0.2f));
        Assert(region.Contains(new Vec3(0.29f, 0f, -0.19f)), "inside");
        Assert(!region.Contains(new Vec3(0.31f, 0.1f, 0f)), "outside X");
        Assert(region.Contains(new Vec3(0.31f, 0.1f, 0f), 0.02f), "within tolerance");
        Assert(!region.Contains(new Vec3(float.NaN, 0f, 0f)), "NaN");
    }

    // ---- Scenario ----

    private static void ScenarioEarlyBaseFillCompletesOnArm()
    {
        var fill = new VoidFillState(2, 3, 1);
        var latch = new ArmedLatch();
        bool armed = false;
        int completions = 0;
        Func<bool> tryComplete = () =>
        {
            if (!armed) return false;
            completions++;
            return true;
        };

        fill.TryAddFill();
        fill.TryAddFill(); // the trainee fills the base while M2_03 is still open
        Assert(!latch.Offer(fill.IsBaseMet, tryComplete), "not accepted before the step opens");
        armed = true; // M2_04 opens and the station re-offers on Armed
        Assert(latch.Offer(fill.IsBaseMet, tryComplete), "accepted when the step arms");
        Assert(!latch.Offer(fill.IsBaseMet, tryComplete), "completes once");
        AssertEqual(1, completions, "one completion");
    }
}
