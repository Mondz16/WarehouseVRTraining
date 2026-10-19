# Module 2 — Packing and Dispatch — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make all 11 Packing and Dispatch steps (M2_01–M2_11) playable and challenge-gated at a new packing station, and restore the missing M1_09 assessment panel.

**Architecture:** Plain-C# rules (`PackingLogic.cs`, unit-tested with a console runner) sit under small MonoBehaviours wired by direct object references (MCP cannot wire UnityEvents). A carton-centric state machine (`PackingCarton`) polls pillows, products and tools; `PackingStation`, `ShippingLabel`, `ConveyorLoadZone`, `QcReview` and `GazeDwellTarget` turn carton state into arm-aware `ChallengeTutorialTrigger` completions via `ArmedLatch`. A re-runnable editor builder creates the prefabs, the station and all wiring.

**Tech Stack:** Unity 6000.3.12f1 (URP, C# 9), AutoHand, TextMeshPro, uGUI, Valari tutorial framework, .NET 10 console runner for tests, MCP for Unity (`mcpforunity`) to drive the live editor.

**Spec:** `docs/superpowers/specs/2026-10-06-packing-dispatch-module-design.md`

## Global Constraints

- Live editor runs on the `main` checkout; no worktree. Scripts are written straight into `Assets/`, then `refresh_unity` (mode `if_dirty`, compile `request`) and `read_console` for `error CS`. A forced refresh crashed the machine once this session — prefer `if_dirty`.
- `execute_code` is broken; editor work runs through `[MenuItem]` scripts executed with `execute_menu_item`.
- Call `set_active_instance WarehouseVRTraining@238c2776` before Unity tools.
- In Play mode use only `execute_menu_item`, `read_console`, resource reads and `manage_editor` (other tools recompile mid-play). Save the scene before entering Play mode (leaving Play mode can crash the editor).
- UI strings shown in VR are plain ASCII (no ✓, ×, —) so the default TMP font renders them.
- Every bridge is arm-aware: offer completion through `ArmedLatch.Offer(condition, () => trigger == null || trigger.TryComplete())` and re-offer on `ChallengeTutorialTrigger.Armed`.
- Nothing is committed unless the user asks (the user's working tree holds ~150 unrelated uncommitted files). The "commit" step of each task is replaced by a checkpoint note.
- Carton inner sizes (cm, L × H × W): Small 30×15×20, Medium 45×25×35, Large 60×40×50; clearance 5 cm per side. Void fill: base 2, top 3. Assessment pass mark 80 % (10/12).

## File Structure

| File | Responsibility |
|---|---|
| `Assets/Scripts/ImmersiveTraining/TrainingInteractions/Packing/PackingLogic.cs` | Plain C#: fit rule, void-fill phases, seam coverage, segment math, gaze dwell, orientation, box region |
| `Tests/PackingDispatchLogic/PackingDispatchLogic.Tests.csproj`, `Program.cs` | Console unit tests (links `PackingLogic.cs` + `ChallengeBridgeLogic.cs`) |
| `…/Packing/PackingFeedback.cs` | One coloured TMP message line |
| `…/Packing/PackableItem.cs` | Product identity, home pose, pack into a carton |
| `…/Packing/VoidFillPiece.cs`, `VoidFillDispenser.cs` | Pooled air pillows |
| `…/Packing/SeamTool.cs` | Tape gun / box cutter head (registry) |
| `…/Packing/IScanListener.cs` + edit `ScannerTool.cs` | Scanned items can react |
| `…/Packing/PackingCarton.cs` | Carton state: contents, flaps, seams, label, lock |
| `…/Packing/ShippingLabel.cs` | Label scan verification + applying to a sealed carton (M2_06) |
| `…/Packing/ConveyorLoadZone.cs` | Load zone, belt transport, dispatch (M2_07) |
| `…/Packing/GazeDwellTarget.cs` | Zone sign found by gaze (M2_02) |
| `…/Packing/QcReview.cs` | QC answer buttons + correction check (M2_09) |
| `…/Packing/PackingStation.cs` | Orders, carton rack, spot, M2_03–M2_08 bridges, module reset |
| edit `…/TrainingInteractions/AssessmentLauncher.cs` | Optional quiz placement anchor |
| `Assets/Editor/PackingStationBuilder.cs` | Builds prefabs, station, wiring, M2_10 data, M1_09 restore |
| `Assets/Editor/PackingDispatchPlaytestTemp.cs` | Temporary Play-mode walkthrough (deleted at the end) |
| `CLAUDE.md`, memory files | Docs |

---

### Task 1: Packing rules (plain C#) + unit tests

**Files:**
- Create: `Assets/Scripts/ImmersiveTraining/TrainingInteractions/Packing/PackingLogic.cs`
- Create: `Tests/PackingDispatchLogic/PackingDispatchLogic.Tests.csproj`
- Create: `Tests/PackingDispatchLogic/Program.cs`

**Interfaces:**
- Consumes: `ArmedLatch` (`ChallengeBridgeLogic.cs`).
- Produces: `CartonSize`, `CartonVerdict`, `Size3(x,y,z)`, `CartonFit.InnerSize/UsableSize/Fits/SmallestFit/Judge`, `FillPhase`, `FillResult`, `ItemResult`, `VoidFillState(base, top, items)` with `TryAddFill()`, `TryAddItem()`, `Set(fill, items)`, `Fill`, `ItemsPlaced`, `ItemsRequired`, `FullLevel`, `IsBaseMet`, `IsFull`, `AllItemsPlaced`, `Phase`, `FillHeight(baseShare, fullShare)`; `SeamCoverage(bins, completeFraction)` with `Mark(t)`, `Fraction`, `IsComplete`, `Reset()`; `Vec3`; `SegmentMath.Closest(p, a, b, out distance)`; `GazeDwell(maxAngle, maxDistance, dwell)` with `Update(angle, distance, dt)`, `Found`, `Reset()`, `AngleBetween(a, b)`; `Orientation.IsFaceUp(normalUp, maxTiltDeg)`; `BoxRegion(center, halfExtents).Contains(p, tolerance)`.

- [ ] **Step 1: Write the failing tests**

<!-- write: Tests/PackingDispatchLogic/PackingDispatchLogic.Tests.csproj -->
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <!--
    Unity-free unit tests for the Packing and Dispatch (M2) rules in
    Assets/Scripts/ImmersiveTraining/TrainingInteractions/Packing/PackingLogic.cs, plus ArmedLatch scenarios from
    Assets/Scripts/ImmersiveTraining/TrainingInteractions/ChallengeBridgeLogic.cs.
    Run from the repo root with the command "dotnet run" and the project option pointing at Tests/PackingDispatchLogic.
    LangVersion matches Unity's C# 9 so the linked files are checked against the same language rules.
  -->
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <Nullable>disable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="Program.cs" />
    <Compile Include="..\..\Assets\Scripts\ImmersiveTraining\TrainingInteractions\Packing\PackingLogic.cs" Link="PackingLogic.cs" />
    <Compile Include="..\..\Assets\Scripts\ImmersiveTraining\TrainingInteractions\ChallengeBridgeLogic.cs" Link="ChallengeBridgeLogic.cs" />
  </ItemGroup>

</Project>
```

<!-- write: Tests/PackingDispatchLogic/Program.cs -->
```csharp
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
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet run --project Tests/PackingDispatchLogic`
Expected: build error — `PackingLogic.cs` not found / `CartonFit` does not exist.

- [ ] **Step 3: Write the implementation**

<!-- write: Assets/Scripts/ImmersiveTraining/TrainingInteractions/Packing/PackingLogic.cs -->
```csharp
using System;

namespace ImmersiveTraining.TrainingInteractions
{
    // Plain C# (no UnityEngine) rules behind the Packing and Dispatch module (M2), kept separate so they can be
    // unit-tested outside Unity: dotnet run --project Tests/PackingDispatchLogic

    public enum CartonSize { Small = 0, Medium = 1, Large = 2 }

    public enum CartonVerdict { Correct, TooSmall, Oversized }

    /// <summary>Three sides of a box (centimetres for the fit rule).</summary>
    public readonly struct Size3
    {
        public readonly float X, Y, Z;

        public Size3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>The sides, longest first (orientation does not matter for fitting).</summary>
        public float[] SortedDescending()
        {
            float[] sides = { X, Y, Z };
            Array.Sort(sides);
            Array.Reverse(sides);
            return sides;
        }

        public override string ToString() => $"{X:0.#} x {Y:0.#} x {Z:0.#}";
    }

    /// <summary>
    /// The minimum-fit rule (M2_03 / M2_08): use the smallest carton whose usable size - its inner size less the
    /// void-fill clearance on every side - holds the packed order in any orientation.
    /// </summary>
    public static class CartonFit
    {
        public const float DefaultClearanceCm = 5f;

        private static readonly CartonSize[] SmallestFirst = { CartonSize.Small, CartonSize.Medium, CartonSize.Large };

        /// <summary>Inner size in centimetres: length (X), height (Y), width (Z).</summary>
        public static Size3 InnerSize(CartonSize size)
        {
            switch (size)
            {
                case CartonSize.Small: return new Size3(30f, 15f, 20f);
                case CartonSize.Medium: return new Size3(45f, 25f, 35f);
                default: return new Size3(60f, 40f, 50f);
            }
        }

        /// <summary>The inner size less the clearance on both sides of every axis.</summary>
        public static Size3 UsableSize(CartonSize size, float clearanceCm = DefaultClearanceCm)
        {
            Size3 inner = InnerSize(size);
            float both = 2f * clearanceCm;
            return new Size3(Math.Max(0f, inner.X - both), Math.Max(0f, inner.Y - both), Math.Max(0f, inner.Z - both));
        }

        public static bool Fits(Size3 packed, CartonSize size, float clearanceCm = DefaultClearanceCm)
        {
            float[] need = packed.SortedDescending();
            float[] room = UsableSize(size, clearanceCm).SortedDescending();
            for (int i = 0; i < 3; i++)
                if (!(need[i] <= room[i])) return false; // NaN never fits
            return true;
        }

        /// <summary>The smallest carton that fits, or null when even the largest is too small.</summary>
        public static CartonSize? SmallestFit(Size3 packed, float clearanceCm = DefaultClearanceCm)
        {
            foreach (CartonSize size in SmallestFirst)
                if (Fits(packed, size, clearanceCm)) return size;
            return null;
        }

        public static CartonVerdict Judge(CartonSize chosen, Size3 packed, float clearanceCm = DefaultClearanceCm)
        {
            CartonSize? best = SmallestFit(packed, clearanceCm);
            if (best == null || chosen < best.Value) return CartonVerdict.TooSmall;
            return chosen == best.Value ? CartonVerdict.Correct : CartonVerdict.Oversized;
        }
    }

    public enum FillPhase { Base, Items, Top, Full }

    public enum FillResult { Added, PlaceItemsFirst, AlreadyFull }

    public enum ItemResult { Added, NeedsBaseFill, NoRoom }

    /// <summary>
    /// Void fill inside one carton (M2_04 / M2_08 / M2_09): a base layer first, then the item(s), then fill over and
    /// around them until nothing can move. Counts are in pillows.
    /// </summary>
    public sealed class VoidFillState
    {
        public VoidFillState(int baseRequired, int topRequired, int itemsRequired)
        {
            BaseRequired = Math.Max(0, baseRequired);
            TopRequired = Math.Max(0, topRequired);
            ItemsRequired = Math.Max(0, itemsRequired);
        }

        public int BaseRequired { get; }
        public int TopRequired { get; }
        public int ItemsRequired { get; }
        public int Fill { get; private set; }
        public int ItemsPlaced { get; private set; }

        public int FullLevel => BaseRequired + TopRequired;
        public bool AllItemsPlaced => ItemsPlaced >= ItemsRequired;
        public bool IsBaseMet => Fill >= BaseRequired;
        public bool IsFull => AllItemsPlaced && Fill >= FullLevel;

        public FillPhase Phase
        {
            get
            {
                if (!AllItemsPlaced) return IsBaseMet ? FillPhase.Items : FillPhase.Base;
                return Fill < FullLevel ? FillPhase.Top : FillPhase.Full;
            }
        }

        public FillResult TryAddFill()
        {
            switch (Phase)
            {
                case FillPhase.Items: return FillResult.PlaceItemsFirst;
                case FillPhase.Full: return FillResult.AlreadyFull;
                default:
                    Fill++;
                    return FillResult.Added;
            }
        }

        public ItemResult TryAddItem()
        {
            if (AllItemsPlaced) return ItemResult.NoRoom;
            if (!IsBaseMet) return ItemResult.NeedsBaseFill;
            ItemsPlaced++;
            return ItemResult.Added;
        }

        /// <summary>Puts the box straight into a given state (the pre-packed QC box).</summary>
        public void Set(int fill, int itemsPlaced)
        {
            Fill = Math.Max(0, Math.Min(fill, FullLevel));
            ItemsPlaced = Math.Max(0, Math.Min(itemsPlaced, ItemsRequired));
        }

        public void Reset() => Set(0, 0);

        /// <summary>
        /// Height of the fill as a share of the inner height: the base layer grows to <paramref name="baseShare"/>,
        /// the top fill from there to <paramref name="fullShare"/>.
        /// </summary>
        public float FillHeight(float baseShare, float fullShare)
        {
            if (FullLevel == 0) return fullShare;
            if (Fill <= BaseRequired) return BaseRequired == 0 ? 0f : baseShare * Fill / BaseRequired;
            float top = Math.Min(1f, (float)(Fill - BaseRequired) / Math.Max(1, TopRequired));
            return baseShare + (fullShare - baseShare) * top;
        }
    }

    /// <summary>How much of a seam a tool has swept (tape gun M2_05 / M2_09, box cutter M2_09).</summary>
    public sealed class SeamCoverage
    {
        private readonly bool[] _bins;

        public SeamCoverage(int bins = 12, float completeFraction = 0.85f)
        {
            _bins = new bool[Math.Max(1, bins)];
            CompleteFraction = completeFraction;
        }

        public float CompleteFraction { get; }

        public float Fraction
        {
            get
            {
                int covered = 0;
                foreach (bool bin in _bins)
                    if (bin) covered++;
                return (float)covered / _bins.Length;
            }
        }

        public bool IsComplete => Fraction >= CompleteFraction;

        /// <summary>
        /// Marks the bin at <paramref name="t"/> (0 = seam start, 1 = seam end). Values outside 0-1 are ignored.
        /// Returns true if that bin was not covered yet.
        /// </summary>
        public bool Mark(float t)
        {
            if (float.IsNaN(t) || t < 0f || t > 1f) return false;
            int index = Math.Min(_bins.Length - 1, (int)(t * _bins.Length));
            if (_bins[index]) return false;
            _bins[index] = true;
            return true;
        }

        public void Reset() => Array.Clear(_bins, 0, _bins.Length);
    }

    public readonly struct Vec3
    {
        public readonly float X, Y, Z;

        public Vec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator *(Vec3 a, float s) => new Vec3(a.X * s, a.Y * s, a.Z * s);

        public float Dot(Vec3 other) => X * other.X + Y * other.Y + Z * other.Z;
        public float Length => (float)Math.Sqrt(Dot(this));
    }

    public static class SegmentMath
    {
        /// <summary>
        /// Parameter of the point on line ab closest to <paramref name="p"/> (0 = a, 1 = b, not clamped) and
        /// <paramref name="p"/>'s distance to the segment itself (clamped to its ends).
        /// </summary>
        public static float Closest(Vec3 p, Vec3 a, Vec3 b, out float distance)
        {
            Vec3 ab = b - a;
            float lengthSq = ab.Dot(ab);
            float t = lengthSq <= 1e-12f ? 0f : (p - a).Dot(ab) / lengthSq;
            float clamped = Math.Max(0f, Math.Min(1f, t));
            distance = (p - (a + ab * clamped)).Length;
            return t;
        }
    }

    /// <summary>"Look at it for a moment" (M2_02): the gaze must stay within an angle and distance for a dwell time.</summary>
    public sealed class GazeDwell
    {
        private float _timer;

        public GazeDwell(float maxAngleDeg = 12f, float maxDistance = 4.5f, float dwellSeconds = 0.5f)
        {
            MaxAngleDeg = maxAngleDeg;
            MaxDistance = maxDistance;
            DwellSeconds = dwellSeconds;
        }

        public float MaxAngleDeg { get; }
        public float MaxDistance { get; }
        public float DwellSeconds { get; }
        public bool Found { get; private set; }

        /// <summary>Advances the dwell; returns true on the update that finds the target.</summary>
        public bool Update(float angleDeg, float distance, float deltaTime)
        {
            if (Found) return false;
            bool looking = angleDeg <= MaxAngleDeg && distance <= MaxDistance; // false for NaN
            _timer = looking ? _timer + Math.Max(0f, deltaTime) : 0f;
            if (_timer < DwellSeconds) return false;
            Found = true;
            return true;
        }

        public void Reset()
        {
            Found = false;
            _timer = 0f;
        }

        /// <summary>Angle in degrees between two directions of any length; NaN if either is zero.</summary>
        public static float AngleBetween(Vec3 a, Vec3 b)
        {
            float la = a.Length, lb = b.Length;
            if (la < 1e-6f || lb < 1e-6f) return float.NaN;
            double cos = Math.Max(-1.0, Math.Min(1.0, a.Dot(b) / (la * lb)));
            return (float)(Math.Acos(cos) * 180.0 / Math.PI);
        }
    }

    public static class Orientation
    {
        /// <summary>
        /// True if a face whose unit normal has world-up component <paramref name="normalUp"/> points up within
        /// <paramref name="maxTiltDeg"/> (M2_07: the label must face up).
        /// </summary>
        public static bool IsFaceUp(float normalUp, float maxTiltDeg)
        {
            if (float.IsNaN(normalUp)) return false;
            return normalUp >= Math.Cos(maxTiltDeg * Math.PI / 180.0) - 1e-6;
        }
    }

    /// <summary>An axis-aligned box in some local space (carton interior, packing spot, conveyor load zone).</summary>
    public readonly struct BoxRegion
    {
        public readonly Vec3 Center, HalfExtents;

        public BoxRegion(Vec3 center, Vec3 halfExtents)
        {
            Center = center;
            HalfExtents = halfExtents;
        }

        public bool Contains(Vec3 p, float tolerance = 0f) =>
            Math.Abs(p.X - Center.X) <= HalfExtents.X + tolerance &&
            Math.Abs(p.Y - Center.Y) <= HalfExtents.Y + tolerance &&
            Math.Abs(p.Z - Center.Z) <= HalfExtents.Z + tolerance;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet run --project Tests/PackingDispatchLogic`
Expected: `31 passed, 0 failed`. Also re-run `dotnet run --project Tests/OrderPickingLogic` → still `45 passed, 0 failed`.

- [ ] **Step 5: Checkpoint** — no commit (user rule); note the test counts.

---

### Task 2: Runtime components (compile against the live editor)

**Files:**
- Create: `…/Packing/PackingFeedback.cs`, `PackableItem.cs`, `VoidFillPiece.cs`, `VoidFillDispenser.cs`, `SeamTool.cs`, `IScanListener.cs`, `PackingCarton.cs`, `ShippingLabel.cs`, `ConveyorLoadZone.cs`, `GazeDwellTarget.cs`, `QcReview.cs`, `PackingStation.cs`
- Modify: `Assets/Scripts/ImmersiveTraining/TrainingInteractions/ScannerTool.cs` (notify `IScanListener`s)
- Modify: `Assets/Scripts/ImmersiveTraining/TrainingInteractions/AssessmentLauncher.cs` (quiz anchor)

**Interfaces:**
- Consumes: Task 1 types; `ArmedLatch`; `ChallengeTutorialTrigger.TryComplete/IsArmed/Armed`; `MultiCounterGate.SetSourceMet/ResetGate`; AutoHand `Grabbable.IsHeld/ForceHandsRelease/isGrabbable`; `TutorialManager.OnTrainingStartedEvent/OnUpdateMistakeCountEvent`.
- Produces (used by the builder and the playtest driver):
  - `PackingCarton`: `All`, `WallThickness`, `SeamCentre/SeamEndA/SeamEndB/SeamCount`, `Changed`, `Size`, `InnerSize`, `OrderId`, `Fill`, `IsFull`, `IsBaseFilled`, `ItemCount`, `IsClosed`, `HasBeenOpened`, `IsSealed`, `TapedCount`, `IsTaped(i)`, `IsLabelled`, `IsLocked`, `IsPackable`, `IsOnConveyor`, `IsHeld`, `Body`, `DropPoint`, `TopPoint`, `Configure(orderId, items, feedback)`, `ResetToPrePacked()`, `LockAt(pos, rot)`, `Unlock()`, `PutOnConveyor()`, `IsOnTopFace(world)`, `TryApplyLabel()` → `LabelResult`, `SeamWorldEnds(i, out a, out b)`.
  - `PackingStation`: `OrderIndex`, `ActiveCarton`, `Spot`, `Feedback`, `CurrentOrder`, `SupplyCarton(size)`, `SupplySlot(size)`, `LooseOrderItems()`, `HandleDispatched(carton)`.
  - `PackableItem`: `All`, `DisplayName`, `SizeMetres`, `OrderId`, `IsPacked`, `IsHeld`, `SetHome`, `ReturnHome`, `PackInto`.
  - `VoidFillDispenser`: `SlotCount`, `PieceInSlot(i)`, `Return(piece)`; `VoidFillPiece`: `All`, `Dispenser`, `IsHeld`, `Body`, `Recycle()`.
  - `SeamTool`: `All`, `Kind`, `HeadPosition`, `ForceActive`, `IsActive`; `SeamToolKind { Tape, Cutter }`.
  - `ShippingLabel`: `IsVerified`, `IsApplied`, `ResetLabel()`; `ConveyorLoadZone`: `LoadPoint`, `HasAccepted`, `ResetZone()`; `GazeDwellTarget`: `HeadOverride`, `IsFound`, `Activate()`; `QcReview`: `Carton`, `IsIdentified`, `IsCorrected`, `ResultText`, `ButtonFor(element)`, `Answer(element)`, `ResetReview()`; `QcElement { Label, Seal, VoidFill }`; `PackingFeedback.Show(msg, good)`, `LastMessage`.
  - `AssessmentLauncher` gains serialized `_quizRoot`, `_quizAnchor`.

- [ ] **Step 1: Write the small components**

<!-- write: Assets/Scripts/ImmersiveTraining/TrainingInteractions/Packing/PackingFeedback.cs -->
```csharp
using TMPro;
using UnityEngine;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// One line of packing-station feedback (M2): green for progress, red for a rule the trainee broke. The line
    /// clears after a few seconds.
    /// </summary>
    public class PackingFeedback : MonoBehaviour
    {
        [SerializeField] private TMP_Text _text;
        [SerializeField] private float _holdSeconds = 6f;
        [SerializeField] private Color _goodColor = new Color(0.3f, 0.9f, 0.4f);
        [SerializeField] private Color _badColor = new Color(1f, 0.4f, 0.3f);

        private float _clearAt;

        /// <summary>The last message shown (also read by the Play-mode test driver).</summary>
        public string LastMessage { get; private set; } = string.Empty;

        private void Awake()
        {
            if (_text == null) _text = GetComponent<TMP_Text>();
            if (_text != null) _text.text = string.Empty;
        }

        public void Show(string message, bool good)
        {
            LastMessage = message ?? string.Empty;
            Debug.Log($"#{nameof(PackingFeedback)}# {(good ? "OK" : "NO")}: {LastMessage}");
            if (_text == null) return;

            _text.text = LastMessage;
            _text.color = good ? _goodColor : _badColor;
            _clearAt = Time.time + _holdSeconds;
        }

        private void Update()
        {
            if (_clearAt <= 0f || Time.time < _clearAt) return;
            if (_text != null) _text.text = string.Empty;
            _clearAt = 0f;
        }
    }
}
```

<!-- write: Assets/Scripts/ImmersiveTraining/TrainingInteractions/Packing/PackableItem.cs -->
```csharp
using System.Collections.Generic;
using Autohand;
using UnityEngine;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// A product that is packed into a <see cref="PackingCarton"/> (M2). Cartons find loose products through
    /// <see cref="All"/>; an accepted product is made inert and parented into the carton, a refused one goes back to
    /// where it was spawned.
    /// </summary>
    public class PackableItem : MonoBehaviour
    {
        public static readonly List<PackableItem> All = new List<PackableItem>();

        [SerializeField] private string _displayName;

        [Tooltip("Outer size in centimetres: length (local X), height (Y), width (Z).")]
        [SerializeField] private Vector3 _sizeCm = new Vector3(20f, 10f, 10f);

        [SerializeField] private Grabbable _grabbable;
        [SerializeField] private Rigidbody _body;

        private Vector3 _homePosition;
        private Quaternion _homeRotation;

        public string DisplayName => _displayName;
        public Vector3 SizeMetres => _sizeCm * 0.01f;

        /// <summary>Order the product belongs to (set by the station when it spawns the product).</summary>
        public string OrderId { get; set; }

        public bool IsPacked { get; private set; }
        public bool IsHeld => _grabbable != null && _grabbable.IsHeld();

        private void Awake()
        {
            if (_grabbable == null) _grabbable = GetComponent<Grabbable>();
            if (_body == null) _body = GetComponent<Rigidbody>();
            SetHome(transform.position, transform.rotation);
        }

        private void OnEnable() => All.Add(this);

        private void OnDisable() => All.Remove(this);

        public void SetHome(Vector3 position, Quaternion rotation)
        {
            _homePosition = position;
            _homeRotation = rotation;
        }

        /// <summary>Puts a refused product back where it was spawned.</summary>
        public void ReturnHome()
        {
            if (IsPacked) return;
            if (_grabbable != null) _grabbable.ForceHandsRelease();
            transform.SetPositionAndRotation(_homePosition, _homeRotation);
            if (_body == null || _body.isKinematic) return;
            _body.position = _homePosition;
            _body.rotation = _homeRotation;
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
        }

        /// <summary>Makes the product part of the carton: no longer grabbable or physical, parented at a slot.</summary>
        public void PackInto(Transform parent, Vector3 localPosition, Quaternion localRotation)
        {
            if (_grabbable != null)
            {
                _grabbable.ForceHandsRelease();
                _grabbable.isGrabbable = false;
            }

            if (_body != null)
            {
                _body.isKinematic = true;
                _body.detectCollisions = false;
            }

            transform.SetParent(parent, false);
            transform.localPosition = localPosition;
            transform.localRotation = localRotation;
            IsPacked = true;
        }
    }
}
```

<!-- write: Assets/Scripts/ImmersiveTraining/TrainingInteractions/Packing/VoidFillPiece.cs -->
```csharp
using System.Collections.Generic;
using Autohand;
using UnityEngine;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// One air pillow (M2 void fill). A <see cref="PackingCarton"/> absorbs it into its fill level, or refuses it;
    /// either way it goes back to its <see cref="VoidFillDispenser"/> pool.
    /// </summary>
    public class VoidFillPiece : MonoBehaviour
    {
        public static readonly List<VoidFillPiece> All = new List<VoidFillPiece>();

        [SerializeField] private Grabbable _grabbable;
        [SerializeField] private Rigidbody _body;

        public VoidFillDispenser Dispenser { get; set; }
        public bool IsHeld => _grabbable != null && _grabbable.IsHeld();
        public Rigidbody Body => _body;

        private void Awake()
        {
            if (_grabbable == null) _grabbable = GetComponent<Grabbable>();
            if (_body == null) _body = GetComponent<Rigidbody>();
        }

        private void OnEnable() => All.Add(this);

        private void OnDisable() => All.Remove(this);

        public void Recycle()
        {
            if (_grabbable != null) _grabbable.ForceHandsRelease();
            if (Dispenser != null) Dispenser.Return(this);
            else gameObject.SetActive(false);
        }
    }
}
```

<!-- write: Assets/Scripts/ImmersiveTraining/TrainingInteractions/Packing/VoidFillDispenser.cs -->
```csharp
using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// Keeps a few air pillows ready at the void-fill dispenser (M2_04 / M2_08 / M2_09). Pillows are pooled: a carton
    /// that absorbs or refuses one hands it back, and an empty slot is refilled shortly after its pillow is taken.
    /// </summary>
    public class VoidFillDispenser : MonoBehaviour
    {
        [SerializeField] private VoidFillPiece _piecePrefab;
        [SerializeField] private Transform[] _slots = new Transform[0];
        [SerializeField] private int _poolSize = 12;

        [Tooltip("A pillow further than this from its slot counts as taken (metres).")]
        [SerializeField] private float _takenDistance = 0.15f;

        [SerializeField] private float _refillDelay = 0.6f;

        private readonly Queue<VoidFillPiece> _pool = new Queue<VoidFillPiece>();
        private VoidFillPiece[] _inSlot;
        private float[] _refillAt;

        public int SlotCount => _slots.Length;

        /// <summary>The pillow waiting in a slot, or null.</summary>
        public VoidFillPiece PieceInSlot(int slot) =>
            _inSlot != null && slot >= 0 && slot < _inSlot.Length ? _inSlot[slot] : null;

        private void Awake()
        {
            _inSlot = new VoidFillPiece[_slots.Length];
            _refillAt = new float[_slots.Length];
            if (_piecePrefab == null)
            {
                Debug.LogWarning($"#{GetType().Name}# No pillow prefab set; the dispenser stays empty.", this);
                return;
            }

            for (int i = 0; i < _poolSize; i++)
            {
                VoidFillPiece piece = Instantiate(_piecePrefab, transform.position, transform.rotation, transform);
                piece.name = $"{_piecePrefab.name}_{i:00}";
                piece.Dispenser = this;
                piece.gameObject.SetActive(false);
                _pool.Enqueue(piece);
            }
        }

        private void Update()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                VoidFillPiece piece = _inSlot[i];
                if (piece != null)
                {
                    bool waiting = piece.gameObject.activeSelf && !piece.IsHeld &&
                                   (piece.transform.position - _slots[i].position).sqrMagnitude <= _takenDistance * _takenDistance;
                    if (waiting) continue;

                    _inSlot[i] = null; // taken
                    _refillAt[i] = Time.time + _refillDelay;
                }

                if (Time.time >= _refillAt[i]) FillSlot(i);
            }
        }

        private void FillSlot(int slot)
        {
            if (_pool.Count == 0 || _slots[slot] == null) return;

            VoidFillPiece piece = _pool.Dequeue();
            piece.transform.SetPositionAndRotation(_slots[slot].position, _slots[slot].rotation);
            piece.gameObject.SetActive(true);
            if (piece.Body != null && !piece.Body.isKinematic)
            {
                piece.Body.position = _slots[slot].position;
                piece.Body.rotation = _slots[slot].rotation;
                piece.Body.linearVelocity = Vector3.zero;
                piece.Body.angularVelocity = Vector3.zero;
            }

            _inSlot[slot] = piece;
        }

        /// <summary>Takes a pillow back into the pool.</summary>
        public void Return(VoidFillPiece piece)
        {
            if (piece == null || !piece.gameObject.activeSelf) return;

            for (int i = 0; i < _inSlot.Length; i++)
                if (_inSlot[i] == piece) _inSlot[i] = null;

            piece.gameObject.SetActive(false);
            if (piece.transform.parent != transform) piece.transform.SetParent(transform, true);
            _pool.Enqueue(piece);
        }
    }
}
```

<!-- write: Assets/Scripts/ImmersiveTraining/TrainingInteractions/Packing/SeamTool.cs -->
```csharp
using System.Collections.Generic;
using Autohand;
using UnityEngine;

namespace ImmersiveTraining.TrainingInteractions
{
    public enum SeamToolKind { Tape, Cutter }

    /// <summary>
    /// The working tip of a tape gun (tapes a closed carton's seams, M2_05 / M2_09) or box cutter (cuts the QC box's
    /// centre seam open, M2_09). Cartons find tools through <see cref="All"/> and only react while the tool is held.
    /// </summary>
    public class SeamTool : MonoBehaviour
    {
        public static readonly List<SeamTool> All = new List<SeamTool>();

        [SerializeField] private SeamToolKind _kind;

        [Tooltip("Point that touches the seam (tape roller / blade tip). Defaults to this transform.")]
        [SerializeField] private Transform _head;

        [SerializeField] private Grabbable _grabbable;

        /// <summary>Lets the Play-mode test driver use the tool without a hand holding it.</summary>
        public bool ForceActive { get; set; }

        public SeamToolKind Kind => _kind;
        public Vector3 HeadPosition => (_head != null ? _head : transform).position;
        public bool IsActive => ForceActive || (_grabbable != null && _grabbable.IsHeld());

        private void Awake()
        {
            if (_grabbable == null) _grabbable = GetComponent<Grabbable>();
        }

        private void OnEnable() => All.Add(this);

        private void OnDisable() => All.Remove(this);
    }
}
```

<!-- write: Assets/Scripts/ImmersiveTraining/TrainingInteractions/Packing/IScanListener.cs -->
```csharp
namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// Implemented by components on a scannable item that react to being scanned by a <see cref="ScannerTool"/>
    /// (e.g. the M2 shipping label shows its verification readout).
    /// </summary>
    public interface IScanListener
    {
        void OnScanned(ScannerTool scanner, bool verified);
    }
}
```

- [ ] **Step 2: Hook `ScannerTool.Scan` up to `IScanListener`**

In `Assets/Scripts/ImmersiveTraining/TrainingInteractions/ScannerTool.cs`, replace:

```csharp
            bool matches = string.IsNullOrEmpty(_expectedSku) || item.Sku == _expectedSku;
```

with:

```csharp
            bool matches = string.IsNullOrEmpty(_expectedSku) || item.Sku == _expectedSku;

            // Scanned items can react (e.g. the M2 shipping label shows its verification readout).
            foreach (IScanListener listener in item.GetComponents<IScanListener>())
                listener.OnScanned(this, matches);
```

- [ ] **Step 3: Write `PackingCarton`**

<!-- write: Assets/Scripts/ImmersiveTraining/TrainingInteractions/Packing/PackingCarton.cs -->
```csharp
using System;
using System.Collections.Generic;
using Autohand;
using UnityEngine;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// A shipping carton and everything that happens to it at the packing station (M2): void fill and products going
    /// in, the flaps closing once it is full, the three H-pattern seams being taped (or the QC box's centre seam being
    /// cut open) and the shipping label. The rules live in <see cref="VoidFillState"/> and <see cref="SeamCoverage"/>;
    /// this component polls the loose pillows, products and tools near it and raises <see cref="Changed"/> on every
    /// state change, which the station / label / conveyor / QC bridges turn into challenge completions.
    ///
    /// Local axes: length along X, height along Y (pivot at the outside bottom centre), width along Z. Contents are
    /// only taken while the carton is packable (locked on the packing spot or the QC table) and open.
    /// </summary>
    public class PackingCarton : MonoBehaviour
    {
        public static readonly List<PackingCarton> All = new List<PackingCarton>();

        /// <summary>Wall and lid board thickness in metres (the builder uses the same value).</summary>
        public const float WallThickness = 0.008f;

        public const int SeamCentre = 0, SeamEndA = 1, SeamEndB = 2, SeamCount = 3;

        private const float BaseShare = 0.15f, FullShare = 0.9f;

        public enum LabelResult { Applied, NotSealed, AlreadyLabelled }

        [Header("Carton")]
        [SerializeField] private CartonSize _size = CartonSize.Medium;

        [Tooltip("Inner size in metres: length (X), height (Y), width (Z).")]
        [SerializeField] private Vector3 _innerSize = new Vector3(0.45f, 0.25f, 0.35f);

        [SerializeField] private Grabbable _grabbable;
        [SerializeField] private Rigidbody _body;

        [Header("Rules")]
        [SerializeField] private int _baseFill = 2;
        [SerializeField] private int _topFill = 3;

        [Tooltip("How close (metres) a tool head must pass to a seam line to work on it.")]
        [SerializeField] private float _seamRadius = 0.07f;

        [Tooltip("If true, the box cutter can cut the taped centre seam open (the QC box, M2_09).")]
        [SerializeField] private bool _cuttable;

        [Header("Visuals")]
        [SerializeField] private GameObject _openFlaps;

        [Tooltip("Closed top, including its collider.")]
        [SerializeField] private GameObject _lid;

        [SerializeField] private Transform _fillVisual;

        [Tooltip("Tape strips in seam order: centre, end A (-X), end B (+X). Each is scaled along its local X while taping.")]
        [SerializeField] private Transform[] _tapeStrips = new Transform[SeamCount];

        [Tooltip("Guides showing where the tape goes; shown on untaped seams while the box is closed.")]
        [SerializeField] private GameObject[] _seamGuides = new GameObject[SeamCount];

        [SerializeField] private GameObject _labelVisual;
        [SerializeField] private Transform _contentsRoot;

        [Header("Pre-packed state (QC box)")]
        [SerializeField] private bool _prePacked;
        [SerializeField] private string _prePackedOrderId = "ORD-1874";
        [SerializeField] private int _prePackedFill = 1;
        [SerializeField] private int _prePackedItems = 1;

        [Header("Feedback (optional; the station sets it on the cartons it spawns)")]
        [SerializeField] private PackingFeedback _feedback;

        private readonly SeamCoverage[] _coverage = { new SeamCoverage(), new SeamCoverage(), new SeamCoverage() };
        private readonly bool[] _taped = new bool[SeamCount];
        private readonly SeamCoverage _cut = new SeamCoverage();
        private readonly List<PackableItem> _items = new List<PackableItem>();
        private readonly List<PackableItem> _itemScratch = new List<PackableItem>();
        private readonly List<VoidFillPiece> _pieceScratch = new List<VoidFillPiece>();
        private VoidFillState _fill;
        private string _orderId;
        private bool _closed, _labelled, _locked, _packable, _onConveyor;

        /// <summary>Raised on every state change (fill, item, flaps, seam, label, lock).</summary>
        public event Action<PackingCarton> Changed;

        public CartonSize Size => _size;
        public Vector3 InnerSize => _innerSize;
        public string OrderId => _orderId;
        public VoidFillState Fill => _fill;
        public bool IsFull => _fill.IsFull;
        public bool IsBaseFilled => _fill.IsBaseMet;
        public int ItemCount => _items.Count;
        public bool IsClosed => _closed;
        public bool HasBeenOpened { get; private set; }
        public bool IsSealed => _closed && TapedCount == SeamCount;
        public bool IsLabelled => _labelled;
        public bool IsLocked => _locked;
        public bool IsPackable => _packable;
        public bool IsOnConveyor => _onConveyor;
        public bool IsHeld => _grabbable != null && _grabbable.IsHeld();
        public Rigidbody Body => _body;

        public int TapedCount
        {
            get
            {
                int taped = 0;
                foreach (bool seam in _taped)
                    if (seam) taped++;
                return taped;
            }
        }

        public bool IsTaped(int seam) => _taped[seam];

        private float TopY => _innerSize.y + 2f * WallThickness;

        /// <summary>A point above the middle of the opening, for dropping things in (Play-mode test driver).</summary>
        public Vector3 DropPoint => transform.TransformPoint(new Vector3(0f, WallThickness + _innerSize.y * 0.7f, 0f));

        /// <summary>A point just above the top face (Play-mode test driver).</summary>
        public Vector3 TopPoint => transform.TransformPoint(new Vector3(_innerSize.x * 0.22f, TopY + 0.02f, 0f));

        private void Awake()
        {
            if (_grabbable == null) _grabbable = GetComponent<Grabbable>();
            if (_body == null) _body = GetComponent<Rigidbody>();

            if (_prePacked) ResetToPrePacked();
            else Configure(null, 1, _feedback);
        }

        private void OnEnable() => All.Add(this);

        private void OnDisable() => All.Remove(this);

        /// <summary>Sets the order this carton packs and how many products it takes (when the station locks it).</summary>
        public void Configure(string orderId, int itemsRequired, PackingFeedback feedback)
        {
            _orderId = orderId;
            _fill = new VoidFillState(_baseFill, _topFill, itemsRequired);
            if (feedback != null) _feedback = feedback;
            RefreshVisuals();
            RaiseChanged();
        }

        /// <summary>Back to the pre-packed QC state: closed, H-taped, labelled, product inside, under-filled, locked.</summary>
        public void ResetToPrePacked()
        {
            _orderId = _prePackedOrderId;
            _fill = new VoidFillState(_baseFill, _topFill, _prePackedItems);
            _fill.Set(_prePackedFill, _prePackedItems);
            _closed = true;
            _labelled = true;
            HasBeenOpened = false;
            for (int i = 0; i < SeamCount; i++)
            {
                _taped[i] = true;
                _coverage[i].Reset();
            }

            _cut.Reset();
            _locked = true;
            _packable = true;
            if (_grabbable != null) _grabbable.isGrabbable = false;
            if (_body != null) _body.isKinematic = true;
            RefreshVisuals();
            RaiseChanged();
        }

        /// <summary>Holds the carton still at a pose (packing spot) and starts taking contents.</summary>
        public void LockAt(Vector3 position, Quaternion rotation)
        {
            if (_grabbable != null)
            {
                _grabbable.ForceHandsRelease();
                _grabbable.isGrabbable = false;
            }

            if (_body != null) _body.isKinematic = true;
            transform.SetPositionAndRotation(position, rotation);
            _locked = true;
            _packable = true;
            RaiseChanged();
        }

        /// <summary>Releases the carton so it can be carried (after labelling).</summary>
        public void Unlock()
        {
            _locked = false;
            _packable = false;
            if (_grabbable != null) _grabbable.isGrabbable = true;
            if (_body != null) _body.isKinematic = false;
            RaiseChanged();
        }

        /// <summary>Taken over by the conveyor: no longer grabbable, moved by script.</summary>
        public void PutOnConveyor()
        {
            if (_grabbable != null)
            {
                _grabbable.ForceHandsRelease();
                _grabbable.isGrabbable = false;
            }

            if (_body != null) _body.isKinematic = true;
            _onConveyor = true;
            _packable = false;
            RaiseChanged();
        }

        private void Update()
        {
            if (_packable && !_closed) TakeContents();
            if (_closed) SweepSeams();
        }

        // ---- contents ----

        private void TakeContents()
        {
            _pieceScratch.Clear();
            _pieceScratch.AddRange(VoidFillPiece.All);
            foreach (VoidFillPiece piece in _pieceScratch)
            {
                if (_closed) return; // the last pillow filled the box
                if (piece == null || piece.IsHeld || !IsInside(piece.transform.position)) continue;
                AddPillow(piece);
            }

            _itemScratch.Clear();
            _itemScratch.AddRange(PackableItem.All);
            foreach (PackableItem item in _itemScratch)
            {
                if (_closed) return;
                if (item == null || item.IsPacked || item.IsHeld || !IsInside(item.transform.position)) continue;
                AddItem(item);
            }
        }

        private void AddPillow(VoidFillPiece piece)
        {
            FillResult result = _fill.TryAddFill();
            piece.Recycle();
            if (result != FillResult.Added)
            {
                Say(result == FillResult.AlreadyFull
                    ? "The box is already full - no more void fill needed."
                    : "Place the item before adding more fill.", false);
                return;
            }

            RefreshVisuals();
            if (_fill.IsFull)
            {
                Close();
                return;
            }

            if (_fill.Phase == FillPhase.Items)
            {
                int remaining = _fill.ItemsRequired - _fill.ItemsPlaced;
                Say(remaining == 1
                    ? "Base layer done - now place the item in the centre."
                    : $"Base layer done - now place the {remaining} items.", true);
            }
            else
            {
                Say($"Void fill {_fill.Fill}/{_fill.FullLevel}.", true);
            }

            RaiseChanged();
        }

        private void AddItem(PackableItem item)
        {
            if (item.OrderId != _orderId)
            {
                item.ReturnHome();
                Say($"The {item.DisplayName} is not part of this order.", false);
                return;
            }

            ItemResult result = _fill.TryAddItem();
            if (result != ItemResult.Added)
            {
                item.ReturnHome();
                Say(result == ItemResult.NeedsBaseFill
                    ? "Add void fill to the base of the box first."
                    : "This box already holds its items.", false);
                return;
            }

            PackItem(item);
            if (_fill.IsFull)
            {
                Close();
                return;
            }

            int remaining = _fill.ItemsRequired - _fill.ItemsPlaced;
            Say(remaining > 0
                ? $"{item.DisplayName} packed - {remaining} more to go."
                : "Item in - now fill the space on all sides and over the top.", true);
            RaiseChanged();
        }

        private void PackItem(PackableItem item)
        {
            int index = _items.Count;
            int count = Mathf.Max(1, _fill.ItemsRequired);
            float usable = _innerSize.x - 0.06f;
            float x = -usable * 0.5f + usable / count * (index + 0.5f);
            float y = WallThickness + _innerSize.y * BaseShare + item.SizeMetres.y * 0.5f;

            // Several products stand side by side with their long side across the box.
            Quaternion rotation = count > 1 ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
            item.PackInto(_contentsRoot != null ? _contentsRoot : transform, new Vector3(x, y, 0f), rotation);
            _items.Add(item);
        }

        private bool IsInside(Vector3 world)
        {
            Vector3 p = transform.InverseTransformPoint(world);
            float minY = WallThickness - 0.02f, maxY = TopY + 0.1f;
            var interior = new BoxRegion(
                new Vec3(0f, (minY + maxY) * 0.5f, 0f),
                new Vec3(_innerSize.x * 0.5f, (maxY - minY) * 0.5f, _innerSize.z * 0.5f));
            return interior.Contains(new Vec3(p.x, p.y, p.z));
        }

        private void Close()
        {
            _closed = true;
            RefreshVisuals();
            Say("Box full - nothing can move. Flaps closed: seal it with the H-tape pattern.", true);
            RaiseChanged();
        }

        private void Open()
        {
            _closed = false;
            HasBeenOpened = true;
            RefreshVisuals();
            Say("Tape cut - box open. Check the void fill: is there enough to stop the item moving?", true);
            RaiseChanged();
        }

        // ---- seams ----

        private void SweepSeams()
        {
            foreach (SeamTool tool in SeamTool.All)
            {
                if (tool == null || !tool.IsActive) continue;

                Vector3 local = transform.InverseTransformPoint(tool.HeadPosition);
                var head = new Vec3(local.x, local.y, local.z);
                for (int seam = 0; seam < SeamCount; seam++)
                {
                    SeamEnds(seam, out Vec3 a, out Vec3 b);
                    float t = SegmentMath.Closest(head, a, b, out float distance);
                    if (distance > _seamRadius) continue;

                    if (tool.Kind == SeamToolKind.Tape && !_taped[seam]) TapeAt(seam, t);
                    else if (tool.Kind == SeamToolKind.Cutter && _cuttable && seam == SeamCentre && _taped[seam]) CutAt(t);
                    if (!_closed) return; // cut open
                }
            }
        }

        private void SeamEnds(int seam, out Vec3 a, out Vec3 b)
        {
            float hx = _innerSize.x * 0.5f, hz = _innerSize.z * 0.5f, top = TopY;
            switch (seam)
            {
                case SeamCentre:
                    a = new Vec3(-hx, top, 0f);
                    b = new Vec3(hx, top, 0f);
                    break;
                case SeamEndA:
                    a = new Vec3(-hx, top, -hz);
                    b = new Vec3(-hx, top, hz);
                    break;
                default:
                    a = new Vec3(hx, top, -hz);
                    b = new Vec3(hx, top, hz);
                    break;
            }
        }

        /// <summary>World-space ends of a seam (Play-mode test driver).</summary>
        public void SeamWorldEnds(int seam, out Vector3 start, out Vector3 end)
        {
            SeamEnds(seam, out Vec3 a, out Vec3 b);
            start = transform.TransformPoint(new Vector3(a.X, a.Y, a.Z));
            end = transform.TransformPoint(new Vector3(b.X, b.Y, b.Z));
        }

        private void TapeAt(int seam, float t)
        {
            if (!_coverage[seam].Mark(t)) return;
            if (!_coverage[seam].IsComplete)
            {
                RefreshTape(seam);
                return;
            }

            _taped[seam] = true;
            RefreshTape(seam);
            Say(TapedCount == SeamCount
                ? "H-tape complete - the box is sealed."
                : $"Seam taped ({TapedCount}/{SeamCount}). A single strip is not enough - tape all three.", true);
            RaiseChanged();
        }

        private void CutAt(float t)
        {
            if (!_cut.Mark(t) || !_cut.IsComplete) return;

            _cut.Reset();
            _taped[SeamCentre] = false;
            _coverage[SeamCentre].Reset();
            Open();
        }

        // ---- label ----

        /// <summary>True if <paramref name="world"/> is on (just above) the top face - the largest flat face.</summary>
        public bool IsOnTopFace(Vector3 world)
        {
            Vector3 p = transform.InverseTransformPoint(world);
            return Mathf.Abs(p.x) <= _innerSize.x * 0.5f + 0.02f &&
                   Mathf.Abs(p.z) <= _innerSize.z * 0.5f + 0.02f &&
                   p.y >= TopY - 0.03f && p.y <= TopY + 0.1f;
        }

        public LabelResult TryApplyLabel()
        {
            if (_labelled) return LabelResult.AlreadyLabelled;
            if (!IsSealed) return LabelResult.NotSealed;

            _labelled = true;
            RefreshVisuals();
            Say("Label applied to the top face. Carry the box to the outbound conveyor.", true);
            RaiseChanged();
            return LabelResult.Applied;
        }

        // ---- visuals ----

        private void RefreshVisuals()
        {
            if (_openFlaps != null) _openFlaps.SetActive(!_closed);
            if (_lid != null) _lid.SetActive(_closed);
            if (_labelVisual != null) _labelVisual.SetActive(_labelled);

            if (_fillVisual != null && _fill != null)
            {
                float height = _innerSize.y * _fill.FillHeight(BaseShare, FullShare);
                _fillVisual.gameObject.SetActive(height > 0.001f);
                _fillVisual.localScale = new Vector3(_innerSize.x - 0.01f, Mathf.Max(0.001f, height), _innerSize.z - 0.01f);
                _fillVisual.localPosition = new Vector3(0f, WallThickness + height * 0.5f, 0f);
            }

            for (int i = 0; i < SeamCount; i++) RefreshTape(i);
        }

        private void RefreshTape(int seam)
        {
            Transform strip = seam < _tapeStrips.Length ? _tapeStrips[seam] : null;
            if (strip != null)
            {
                float fraction = _taped[seam] ? 1f : _closed ? _coverage[seam].Fraction : 0f;
                strip.gameObject.SetActive(fraction > 0f);
                Vector3 scale = strip.localScale;
                scale.x = Mathf.Max(0.001f, fraction * (seam == SeamCentre ? _innerSize.x : _innerSize.z));
                strip.localScale = scale;
            }

            GameObject guide = seam < _seamGuides.Length ? _seamGuides[seam] : null;
            if (guide != null) guide.SetActive(_closed && !_taped[seam]);
        }

        private void Say(string message, bool good)
        {
            if (_feedback != null) _feedback.Show(message, good);
        }

        private void RaiseChanged() => Changed?.Invoke(this);
    }
}
```

- [ ] **Step 4: Write `ShippingLabel`, `ConveyorLoadZone`, `GazeDwellTarget`, `QcReview`**

<!-- write: Assets/Scripts/ImmersiveTraining/TrainingInteractions/Packing/ShippingLabel.cs -->
```csharp
using Autohand;
using UnityEngine;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// The printed shipping label (M2_06). Scanning it with the bench scanner verifies it (address / weight class /
    /// carrier readout) and completes <see cref="_verifiedTrigger"/>. Once verified, letting go of it on the top face
    /// of a sealed carton applies it: the carton shows its own label and this card disappears.
    /// The verify completion is arm-aware: a scan made before M2_06 opens counts when it opens.
    /// </summary>
    public class ShippingLabel : MonoBehaviour, IScanListener
    {
        [SerializeField] private Grabbable _grabbable;
        [SerializeField] private Rigidbody _body;
        [SerializeField] private PackingFeedback _feedback;

        [Tooltip("Readout shown once a scan verifies the label (address / weight class / carrier).")]
        [SerializeField] private GameObject _verifiedReadout;

        [Tooltip("Completed when the label has been verified by a scan (TRIGGER_M2_LabelVerified).")]
        [SerializeField] private ChallengeTutorialTrigger _verifiedTrigger;

        [SerializeField] private string _verifiedMessage = "Label verified: address OK, weight class OK, carrier OK.";

        private readonly ArmedLatch _latch = new ArmedLatch();
        private Vector3 _homePosition;
        private Quaternion _homeRotation;

        public bool IsVerified { get; private set; }
        public bool IsApplied { get; private set; }
        public bool IsHeld => _grabbable != null && _grabbable.IsHeld();

        private void Awake()
        {
            if (_grabbable == null) _grabbable = GetComponent<Grabbable>();
            if (_body == null) _body = GetComponent<Rigidbody>();
            _homePosition = transform.position;
            _homeRotation = transform.rotation;
            if (_verifiedReadout != null) _verifiedReadout.SetActive(false);

            // Subscribed for the whole lifetime: the card is deactivated once applied, but a scan made before M2_06
            // opened must still count when it opens.
            if (_verifiedTrigger != null) _verifiedTrigger.Armed += HandleArmed;
        }

        private void OnDestroy()
        {
            if (_verifiedTrigger != null) _verifiedTrigger.Armed -= HandleArmed;
        }

        public void OnScanned(ScannerTool scanner, bool verified)
        {
            if (!verified || IsApplied) return;
            if (!IsVerified)
            {
                IsVerified = true;
                if (_verifiedReadout != null) _verifiedReadout.SetActive(true);
                Say(_verifiedMessage, true);
            }

            Offer();
        }

        private void HandleArmed(ChallengeTutorialTrigger trigger) => Offer();

        private void Offer() => _latch.Offer(IsVerified, () => _verifiedTrigger == null || _verifiedTrigger.TryComplete());

        private void Update()
        {
            if (IsApplied || IsHeld) return;

            foreach (PackingCarton carton in PackingCarton.All)
            {
                if (carton == null || !carton.IsOnTopFace(transform.position)) continue;

                if (!IsVerified)
                {
                    Say("Scan the label to verify it before you apply it.", false);
                    ReturnHome();
                    return;
                }

                switch (carton.TryApplyLabel())
                {
                    case PackingCarton.LabelResult.Applied:
                        IsApplied = true;
                        gameObject.SetActive(false);
                        return;
                    case PackingCarton.LabelResult.NotSealed:
                        Say("Seal the box with the H-tape pattern before you label it.", false);
                        ReturnHome();
                        return;
                    default:
                        ReturnHome();
                        return;
                }
            }
        }

        /// <summary>Back on the printer, unverified (module restart).</summary>
        public void ResetLabel()
        {
            IsVerified = false;
            IsApplied = false;
            _latch.Reset();
            if (_verifiedReadout != null) _verifiedReadout.SetActive(false);
            gameObject.SetActive(true);
            ReturnHome();
        }

        private void ReturnHome()
        {
            if (_grabbable != null) _grabbable.ForceHandsRelease();
            transform.SetPositionAndRotation(_homePosition, _homeRotation);
            if (_body == null || _body.isKinematic) return;
            _body.position = _homePosition;
            _body.rotation = _homeRotation;
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
        }

        private void Say(string message, bool good)
        {
            if (_feedback != null) _feedback.Show(message, good);
        }
    }
}
```

<!-- write: Assets/Scripts/ImmersiveTraining/TrainingInteractions/Packing/ConveyorLoadZone.cs -->
```csharp
using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// The outbound conveyor (M2_07). A sealed, labelled carton let go inside the load zone with its label (top face)
    /// facing up is taken over: it rides along the belt toward the dock and is dispatched at the end, which tells the
    /// station to start the next order. A box with the label down or on its side is refused with a message.
    /// Completion is arm-aware: a box loaded before M2_07 opens counts when it opens.
    /// </summary>
    public class ConveyorLoadZone : MonoBehaviour
    {
        [Tooltip("Load-zone centre. Its local axes define the zone box; the belt runs along its +X.")]
        [SerializeField] private Transform _zone;

        [SerializeField] private Vector3 _zoneHalfExtents = new Vector3(0.5f, 0.35f, 0.32f);
        [SerializeField] private float _beltSpeed = 0.5f;
        [SerializeField] private float _dispatchDistance = 4.2f;
        [SerializeField] private float _maxTiltDeg = 35f;

        [Tooltip("A box still moving faster than this (m/s) is not judged yet.")]
        [SerializeField] private float _maxRestSpeed = 0.4f;

        [SerializeField] private PackingStation _station;
        [SerializeField] private PackingFeedback _feedback;
        [SerializeField] private ChallengeTutorialTrigger _challengeTrigger;

        private readonly ArmedLatch _latch = new ArmedLatch();
        private readonly List<PackingCarton> _riding = new List<PackingCarton>();
        private readonly Dictionary<PackingCarton, float> _travelled = new Dictionary<PackingCarton, float>();
        private readonly HashSet<PackingCarton> _warned = new HashSet<PackingCarton>();
        private readonly List<PackingCarton> _scratch = new List<PackingCarton>();
        private bool _accepted;

        public bool HasAccepted => _accepted;
        public Vector3 LoadPoint => Zone.position;
        private Transform Zone => _zone != null ? _zone : transform;

        private void Awake()
        {
            if (_challengeTrigger != null) _challengeTrigger.Armed += HandleArmed;
        }

        private void OnDestroy()
        {
            if (_challengeTrigger != null) _challengeTrigger.Armed -= HandleArmed;
        }

        private void Update()
        {
            _scratch.Clear();
            _scratch.AddRange(PackingCarton.All);
            foreach (PackingCarton carton in _scratch) Judge(carton);
            MoveRiding();
        }

        private void Judge(PackingCarton carton)
        {
            if (carton == null || carton.IsOnConveyor || carton.IsLocked) return;
            if (!InZone(carton.transform.position) || carton.IsHeld)
            {
                _warned.Remove(carton); // a fresh attempt gets a fresh message
                return;
            }

            Rigidbody body = carton.Body;
            if (body != null && !body.isKinematic && body.linearVelocity.magnitude > _maxRestSpeed) return;

            if (!carton.IsLabelled)
            {
                Warn(carton, "Only sealed, labelled boxes go on the outbound conveyor.");
                return;
            }

            if (!Orientation.IsFaceUp(carton.transform.up.y, _maxTiltDeg))
            {
                Warn(carton, "The label must face UP - the sorter scanners read it from above. Turn the box over.");
                return;
            }

            Accept(carton);
        }

        private bool InZone(Vector3 world)
        {
            Vector3 p = Zone.InverseTransformPoint(world);
            var zone = new BoxRegion(new Vec3(0f, 0f, 0f), new Vec3(_zoneHalfExtents.x, _zoneHalfExtents.y, _zoneHalfExtents.z));
            return zone.Contains(new Vec3(p.x, p.y, p.z));
        }

        private void Warn(PackingCarton carton, string message)
        {
            if (_warned.Add(carton) && _feedback != null) _feedback.Show(message, false);
        }

        private void Accept(PackingCarton carton)
        {
            carton.PutOnConveyor();

            // Settle square on the belt with the label up, keeping the box's heading.
            Vector3 heading = Vector3.ProjectOnPlane(carton.transform.forward, Vector3.up);
            if (heading.sqrMagnitude < 1e-4f) heading = Zone.forward;
            carton.transform.rotation = Quaternion.LookRotation(heading, Vector3.up);

            _riding.Add(carton);
            _travelled[carton] = 0f;
            _warned.Remove(carton);
            _accepted = true;
            if (_feedback != null) _feedback.Show("Box loaded label-up - on its way to the dock.", true);
            Offer();
        }

        private void MoveRiding()
        {
            for (int i = _riding.Count - 1; i >= 0; i--)
            {
                PackingCarton carton = _riding[i];
                if (carton == null)
                {
                    _riding.RemoveAt(i);
                    continue;
                }

                float step = _beltSpeed * Time.deltaTime;
                carton.transform.position += Zone.right * step;
                float travelled = _travelled[carton] + step;
                _travelled[carton] = travelled;
                if (travelled < _dispatchDistance) continue;

                _riding.RemoveAt(i);
                _travelled.Remove(carton);
                carton.gameObject.SetActive(false);
                if (_station != null) _station.HandleDispatched(carton);
            }
        }

        private void HandleArmed(ChallengeTutorialTrigger trigger) => Offer();

        private void Offer() => _latch.Offer(_accepted, () => _challengeTrigger == null || _challengeTrigger.TryComplete());

        /// <summary>Clears the belt and the completion (module restart; the station destroys the cartons).</summary>
        public void ResetZone()
        {
            _riding.Clear();
            _travelled.Clear();
            _warned.Clear();
            _accepted = false;
            _latch.Reset();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.matrix = Zone.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, _zoneHalfExtents * 2f);
        }
    }
}
```

<!-- write: Assets/Scripts/ImmersiveTraining/TrainingInteractions/Packing/GazeDwellTarget.cs -->
```csharp
using UnityEngine;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// A station sign the trainee locates by looking at it (M2_02): half a second of gaze within 12 degrees from up to
    /// 4.5 m. Once found it turns green, shows its found mark and reports itself met to <see cref="_gate"/>, which
    /// completes the step when every sign is found. Detection starts when the station activates it (module start).
    /// </summary>
    public class GazeDwellTarget : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private MultiCounterGate _gate;
        [SerializeField] private float _maxAngleDeg = 12f;
        [SerializeField] private float _maxDistance = 4.5f;
        [SerializeField] private float _dwellSeconds = 0.5f;

        [Tooltip("Renderer tinted when the sign is found (the sign board).")]
        [SerializeField] private Renderer _board;

        [SerializeField] private Color _foundColor = new Color(0.15f, 0.6f, 0.25f);
        [SerializeField] private GameObject _foundMark;

        private GazeDwell _dwell;
        private MaterialPropertyBlock _block;
        private Color _baseColor = Color.white;
        private bool _active;

        /// <summary>Head used for the gaze test instead of the main camera (Play-mode test driver).</summary>
        public Transform HeadOverride { get; set; }

        public bool IsFound => _dwell != null && _dwell.Found;

        private void Awake()
        {
            _dwell = new GazeDwell(_maxAngleDeg, _maxDistance, _dwellSeconds);
            _block = new MaterialPropertyBlock();
            if (_board != null && _board.sharedMaterial != null && _board.sharedMaterial.HasProperty(BaseColorId))
                _baseColor = _board.sharedMaterial.GetColor(BaseColorId);
            ShowFound(false);
        }

        /// <summary>Starts (or restarts) detection, not found.</summary>
        public void Activate()
        {
            if (_dwell == null) Awake();
            _dwell.Reset();
            _active = true;
            ShowFound(false);
            if (_gate != null) _gate.SetSourceMet(this, false);
        }

        private void Update()
        {
            if (!_active || _dwell.Found) return;

            Transform head = HeadOverride != null ? HeadOverride : Camera.main != null ? Camera.main.transform : null;
            if (head == null) return;

            Vector3 toTarget = transform.position - head.position;
            Vector3 forward = head.forward;
            float angle = GazeDwell.AngleBetween(new Vec3(forward.x, forward.y, forward.z), new Vec3(toTarget.x, toTarget.y, toTarget.z));
            if (!_dwell.Update(angle, toTarget.magnitude, Time.deltaTime)) return;

            ShowFound(true);
            if (_gate != null) _gate.SetSourceMet(this, true);
        }

        private void ShowFound(bool found)
        {
            if (_foundMark != null) _foundMark.SetActive(found);
            if (_board == null) return;
            _board.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, found ? _foundColor : _baseColor);
            _board.SetPropertyBlock(_block);
        }
    }
}
```

<!-- write: Assets/Scripts/ImmersiveTraining/TrainingInteractions/Packing/QcReview.cs -->
```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Valari.Managers;

namespace ImmersiveTraining.TrainingInteractions
{
    public enum QcElement { Label, Seal, VoidFill }

    /// <summary>
    /// Quality review of the pre-packed box (M2_09). Three buttons let the trainee name the faulty element; the right
    /// answer (void fill) only counts once the box has been opened with the box cutter. The box must then be filled to
    /// full and the centre seam re-taped. Completes <see cref="_identifiedTrigger"/> and <see cref="_correctedTrigger"/>
    /// (in order, both arm-aware). Buttons are bound in code because MCP cannot wire UnityEvents.
    /// </summary>
    public class QcReview : MonoBehaviour
    {
        [SerializeField] private PackingCarton _carton;
        [SerializeField] private Button _labelButton;
        [SerializeField] private Button _sealButton;
        [SerializeField] private Button _voidFillButton;
        [SerializeField] private TMP_Text _resultText;
        [SerializeField] private QcElement _faultyElement = QcElement.VoidFill;
        [SerializeField] private ChallengeTutorialTrigger _identifiedTrigger;
        [SerializeField] private ChallengeTutorialTrigger _correctedTrigger;

        [TextArea] [SerializeField] private string _prompt =
            "Check the label, the seal and the void fill. Which one is wrong?";

        [SerializeField] private Color _neutralColor = Color.white;
        [SerializeField] private Color _goodColor = new Color(0.3f, 0.9f, 0.4f);
        [SerializeField] private Color _badColor = new Color(1f, 0.4f, 0.3f);

        private readonly ArmedLatch _identifiedLatch = new ArmedLatch();
        private readonly ArmedLatch _correctedLatch = new ArmedLatch();

        public PackingCarton Carton => _carton;
        public bool IsIdentified { get; private set; }
        public bool IsCorrected => _carton != null && _carton.IsFull && _carton.IsSealed;
        public string ResultText => _resultText != null ? _resultText.text : string.Empty;

        public Button ButtonFor(QcElement element) =>
            element == QcElement.Label ? _labelButton : element == QcElement.Seal ? _sealButton : _voidFillButton;

        private void Awake()
        {
            Bind(_labelButton, QcElement.Label);
            Bind(_sealButton, QcElement.Seal);
            Bind(_voidFillButton, QcElement.VoidFill);
            if (_carton != null) _carton.Changed += HandleCartonChanged;
            if (_identifiedTrigger != null) _identifiedTrigger.Armed += HandleArmed;
            if (_correctedTrigger != null) _correctedTrigger.Armed += HandleArmed;
            Show(_prompt, _neutralColor);
        }

        private void OnDestroy()
        {
            if (_carton != null) _carton.Changed -= HandleCartonChanged;
            if (_identifiedTrigger != null) _identifiedTrigger.Armed -= HandleArmed;
            if (_correctedTrigger != null) _correctedTrigger.Armed -= HandleArmed;
        }

        private void Bind(Button button, QcElement element)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => Answer(element));
        }

        public void Answer(QcElement element)
        {
            if (IsIdentified)
            {
                Show("Fault found - now fix it: fill the box to full, then re-tape the centre seam.", _goodColor);
                return;
            }

            if (element != _faultyElement)
            {
                Show($"The {Name(element)} on this box is correct - check the other elements.", _badColor);
                TutorialManager.OnUpdateMistakeCountEvent?.Invoke();
                return;
            }

            if (element == QcElement.VoidFill && _carton != null && !_carton.HasBeenOpened)
            {
                Show("You can't see the void fill yet - open the box by cutting the centre seam tape with the inspect tool (box cutter).", _badColor);
                return;
            }

            IsIdentified = true;
            Show("Correct - the box is under-filled, so the mugs can shift and break. Add void fill until it is full, then re-tape the centre seam.", _goodColor);
            Offer();
        }

        private static string Name(QcElement element) =>
            element == QcElement.Label ? "label" : element == QcElement.Seal ? "seal" : "void fill";

        private void HandleCartonChanged(PackingCarton carton) => Offer();

        private void HandleArmed(ChallengeTutorialTrigger trigger) => Offer();

        private void Offer()
        {
            _identifiedLatch.Offer(IsIdentified, () => _identifiedTrigger == null || _identifiedTrigger.TryComplete());
            if (_correctedLatch.Offer(IsCorrected, () => _correctedTrigger == null || _correctedTrigger.TryComplete()))
                Show("Box corrected and resealed - ready for dispatch.", _goodColor);
        }

        /// <summary>Puts the QC box back in its faulty state (module restart).</summary>
        public void ResetReview()
        {
            IsIdentified = false;
            _identifiedLatch.Reset();
            _correctedLatch.Reset();
            if (_carton != null) _carton.ResetToPrePacked();
            Show(_prompt, _neutralColor);
        }

        private void Show(string message, Color color)
        {
            if (_resultText == null) return;
            _resultText.text = message;
            _resultText.color = color;
        }
    }
}
```

- [ ] **Step 5: Write `PackingStation`**

<!-- write: Assets/Scripts/ImmersiveTraining/TrainingInteractions/Packing/PackingStation.cs -->
```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Valari.Managers;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>One customer order packed at the station.</summary>
    [Serializable]
    public class PackingOrder
    {
        public string Id = "ORD-0000";

        [Tooltip("Shown on the order panel above the bench.")]
        [TextArea(3, 8)] public string PanelText;

        [Tooltip("Size of all the order's products packed together, in centimetres (any orientation).")]
        public Vector3 PackedSizeCm = new Vector3(20f, 10f, 10f);

        [Tooltip("Products spawned on the bench's display slots when the order starts.")]
        public PackableItem[] ItemPrefabs = new PackableItem[0];
    }

    /// <summary>A rack slot that always holds one carton of its size.</summary>
    [Serializable]
    public class CartonSupply
    {
        public CartonSize Size;
        public PackingCarton Prefab;
        public Transform Slot;
        [NonSerialized] public PackingCarton Current;
    }

    /// <summary>
    /// The packing bench (M2_03 - M2_08). Runs the orders, keeps the carton rack stocked, judges a carton put on the
    /// packing spot with the minimum-fit rule, locks the right one there and turns its progress into challenge
    /// completions: order 1 - box selected (M2_03), base fill / top fill (M2_04), sealed (M2_05), label applied
    /// (M2_06); order 2 - multi-item packed (M2_08). The labelled order-1 box is released so it can be carried to the
    /// conveyor; when the conveyor dispatches it the station starts order 2. Every completion is arm-aware.
    /// When the Packing and Dispatch module (re)starts the whole station resets, including the label, conveyor,
    /// zone signs and QC box.
    /// </summary>
    public class PackingStation : MonoBehaviour
    {
        [SerializeField] private TrainingID _trainingId = TrainingID.PackingAndDispatch;
        [SerializeField] private PackingOrder[] _orders = new PackingOrder[0];

        [Tooltip("Where each order's products are spawned (first product on the first slot).")]
        [SerializeField] private Transform[] _displaySlots = new Transform[0];

        [SerializeField] private CartonSupply[] _supplies = new CartonSupply[0];

        [Header("Packing spot")]
        [Tooltip("Pose a chosen carton is locked to. A carton counts as placed when its base centre is inside the box below (spot-local, centred 0.1 m above the spot).")]
        [SerializeField] private Transform _spot;

        [SerializeField] private Vector3 _spotHalfExtents = new Vector3(0.35f, 0.25f, 0.4f);
        [SerializeField] private float _maxRestSpeed = 0.4f;
        [SerializeField] private float _rejectReturnDelay = 1.5f;

        [Header("UI")]
        [SerializeField] private TMP_Text _orderText;
        [SerializeField] private PackingFeedback _feedback;

        [Header("Station parts reset with the module")]
        [SerializeField] private ShippingLabel _label;
        [SerializeField] private ConveyorLoadZone _conveyor;
        [SerializeField] private MultiCounterGate _layoutGate;
        [SerializeField] private GazeDwellTarget[] _signs = new GazeDwellTarget[0];
        [SerializeField] private QcReview _qcReview;

        [Header("Challenges (direct wiring)")]
        [SerializeField] private ChallengeTutorialTrigger _boxSelectedTrigger;
        [SerializeField] private ChallengeTutorialTrigger _baseFillTrigger;
        [SerializeField] private ChallengeTutorialTrigger _topFillTrigger;
        [SerializeField] private ChallengeTutorialTrigger _sealedTrigger;
        [SerializeField] private ChallengeTutorialTrigger _labelAppliedTrigger;
        [SerializeField] private ChallengeTutorialTrigger _multiItemTrigger;

        private readonly ArmedLatch _boxSelectedLatch = new ArmedLatch();
        private readonly ArmedLatch _baseFillLatch = new ArmedLatch();
        private readonly ArmedLatch _topFillLatch = new ArmedLatch();
        private readonly ArmedLatch _sealedLatch = new ArmedLatch();
        private readonly ArmedLatch _labelAppliedLatch = new ArmedLatch();
        private readonly ArmedLatch _multiItemLatch = new ArmedLatch();
        private readonly List<PackingCarton> _cartons = new List<PackingCarton>();
        private readonly List<PackableItem> _items = new List<PackableItem>();
        private readonly HashSet<PackingCarton> _returning = new HashSet<PackingCarton>();

        // The carton locked for each order; kept after dispatch so its completions can still be offered.
        private PackingCarton[] _orderCartons = new PackingCarton[0];
        private PackingCarton _active;
        private int _orderIndex = -1;

        public int OrderIndex => _orderIndex;
        public PackingCarton ActiveCarton => _active;
        public Transform Spot => _spot;
        public PackingFeedback Feedback => _feedback;
        public PackingOrder CurrentOrder => _orderIndex >= 0 && _orderIndex < _orders.Length ? _orders[_orderIndex] : null;

        public PackingCarton SupplyCarton(CartonSize size)
        {
            foreach (CartonSupply supply in _supplies)
                if (supply.Size == size) return supply.Current;
            return null;
        }

        public Transform SupplySlot(CartonSize size)
        {
            foreach (CartonSupply supply in _supplies)
                if (supply.Size == size) return supply.Slot;
            return null;
        }

        /// <summary>Products of the current order that are not packed yet.</summary>
        public List<PackableItem> LooseOrderItems()
        {
            var loose = new List<PackableItem>();
            string id = CurrentOrder != null ? CurrentOrder.Id : null;
            foreach (PackableItem item in _items)
                if (item != null && !item.IsPacked && item.OrderId == id) loose.Add(item);
            return loose;
        }

        private void Awake()
        {
            _orderCartons = new PackingCarton[_orders.Length];
            TutorialManager.OnTrainingStartedEvent -= HandleTrainingStarted;
            TutorialManager.OnTrainingStartedEvent += HandleTrainingStarted;
            foreach (ChallengeTutorialTrigger trigger in Triggers())
                if (trigger != null) trigger.Armed += HandleArmed;
        }

        private void OnDestroy()
        {
            TutorialManager.OnTrainingStartedEvent -= HandleTrainingStarted;
            foreach (ChallengeTutorialTrigger trigger in Triggers())
                if (trigger != null) trigger.Armed -= HandleArmed;
        }

        private IEnumerable<ChallengeTutorialTrigger> Triggers()
        {
            yield return _boxSelectedTrigger;
            yield return _baseFillTrigger;
            yield return _topFillTrigger;
            yield return _sealedTrigger;
            yield return _labelAppliedTrigger;
            yield return _multiItemTrigger;
        }

        private IEnumerable<ArmedLatch> Latches()
        {
            yield return _boxSelectedLatch;
            yield return _baseFillLatch;
            yield return _topFillLatch;
            yield return _sealedLatch;
            yield return _labelAppliedLatch;
            yield return _multiItemLatch;
        }

        private void Start()
        {
            StockSupplies(); // the rack looks stocked before the module starts
            ShowOrderText();
        }

        private void HandleTrainingStarted(TrainingID id)
        {
            if (id != _trainingId) return;
            ResetStation();
            BeginOrder(0);
        }

        private void ResetStation()
        {
            StopAllCoroutines();
            _returning.Clear();
            if (_active != null) _active.Changed -= HandleCartonChanged;
            _active = null;
            for (int i = 0; i < _orderCartons.Length; i++) _orderCartons[i] = null;

            foreach (PackingCarton carton in _cartons)
                if (carton != null) Destroy(carton.gameObject);
            _cartons.Clear();
            foreach (CartonSupply supply in _supplies) supply.Current = null;

            foreach (PackableItem item in _items)
                if (item != null) Destroy(item.gameObject);
            _items.Clear();

            foreach (ArmedLatch latch in Latches()) latch.Reset();
            StockSupplies();

            if (_label != null) _label.ResetLabel();
            if (_conveyor != null) _conveyor.ResetZone();
            if (_layoutGate != null) _layoutGate.ResetGate();
            foreach (GazeDwellTarget sign in _signs)
                if (sign != null) sign.Activate();
            if (_qcReview != null) _qcReview.ResetReview();
        }

        private void StockSupplies()
        {
            foreach (CartonSupply supply in _supplies)
                if (supply.Current == null) Spawn(supply);
        }

        private void Spawn(CartonSupply supply)
        {
            if (supply.Prefab == null || supply.Slot == null) return;
            PackingCarton carton = Instantiate(supply.Prefab, supply.Slot.position, supply.Slot.rotation, transform);
            carton.name = supply.Prefab.name;
            supply.Current = carton;
            _cartons.Add(carton);
        }

        private CartonSupply SupplyOf(PackingCarton carton)
        {
            foreach (CartonSupply supply in _supplies)
                if (supply.Current == carton) return supply;
            return null;
        }

        private void BeginOrder(int index)
        {
            _orderIndex = index;
            PackingOrder order = CurrentOrder;
            if (order != null)
            {
                for (int i = 0; i < order.ItemPrefabs.Length; i++)
                {
                    PackableItem prefab = order.ItemPrefabs[i];
                    if (prefab == null) continue;
                    Transform slot = i < _displaySlots.Length && _displaySlots[i] != null ? _displaySlots[i] : transform;
                    PackableItem item = Instantiate(prefab, slot.position, slot.rotation, transform);
                    item.name = prefab.name;
                    item.OrderId = order.Id;
                    _items.Add(item);
                }
            }

            ShowOrderText();
            Offer();
        }

        private void ShowOrderText()
        {
            if (_orderText == null) return;
            PackingOrder order = CurrentOrder;
            if (order != null) _orderText.text = order.PanelText;
            else if (_orderIndex < 0) _orderText.text = "PACKING STATION\nWaiting for the next order.";
            else _orderText.text = "All orders packed.\nStation clear.";
        }

        private void Update()
        {
            if (_active != null || CurrentOrder == null) return;
            foreach (PackingCarton carton in _cartons)
            {
                if (!IsOnSpot(carton)) continue;
                Judge(carton); // may restock the rack (changes _cartons): stop iterating
                break;
            }
        }

        private bool IsOnSpot(PackingCarton carton)
        {
            if (carton == null || !carton.isActiveAndEnabled || carton.IsLocked || carton.IsOnConveyor || carton.IsHeld) return false;
            if (_returning.Contains(carton) || _spot == null) return false;

            Rigidbody body = carton.Body;
            if (body != null && !body.isKinematic && body.linearVelocity.magnitude > _maxRestSpeed) return false;

            Vector3 p = _spot.InverseTransformPoint(carton.transform.position);
            var region = new BoxRegion(new Vec3(0f, 0.1f, 0f), new Vec3(_spotHalfExtents.x, _spotHalfExtents.y, _spotHalfExtents.z));
            return region.Contains(new Vec3(p.x, p.y, p.z));
        }

        private void Judge(PackingCarton carton)
        {
            PackingOrder order = CurrentOrder;
            var packed = new Size3(order.PackedSizeCm.x, order.PackedSizeCm.y, order.PackedSizeCm.z);
            switch (CartonFit.Judge(carton.Size, packed))
            {
                case CartonVerdict.Correct:
                    Lock(carton);
                    return;
                case CartonVerdict.TooSmall:
                    Say($"{carton.Size} carton is too small: this order packs to {packed} cm and needs 5 cm of void fill on every side.", false);
                    break;
                default:
                    Say($"{carton.Size} carton is too big: use the smallest box that fits with 5 cm clearance. Oversized boxes waste material, cost dimensional weight and let items shift.", false);
                    break;
            }

            StartCoroutine(ReturnToRack(carton));
        }

        private void Lock(PackingCarton carton)
        {
            CartonSupply supply = SupplyOf(carton);
            carton.LockAt(_spot.position, _spot.rotation);
            carton.Configure(CurrentOrder.Id, ItemCount(CurrentOrder), _feedback);
            _active = carton;
            if (_orderIndex < _orderCartons.Length) _orderCartons[_orderIndex] = carton;
            carton.Changed += HandleCartonChanged;

            if (supply != null)
            {
                supply.Current = null;
                Spawn(supply); // restock the rack
            }

            Say($"{carton.Size} carton - correct: the smallest box with 5 cm clearance all round.", true);
            Offer();
        }

        private static int ItemCount(PackingOrder order)
        {
            int count = 0;
            foreach (PackableItem prefab in order.ItemPrefabs)
                if (prefab != null) count++;
            return count;
        }

        private IEnumerator ReturnToRack(PackingCarton carton)
        {
            _returning.Add(carton);
            yield return new WaitForSeconds(_rejectReturnDelay);
            _returning.Remove(carton);

            CartonSupply supply = SupplyOf(carton);
            if (carton == null || carton.IsHeld || carton.IsLocked || supply == null || supply.Slot == null) yield break;

            carton.transform.SetPositionAndRotation(supply.Slot.position, supply.Slot.rotation);
            Rigidbody body = carton.Body;
            if (body == null || body.isKinematic) yield break;
            body.position = supply.Slot.position;
            body.rotation = supply.Slot.rotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        private void HandleCartonChanged(PackingCarton carton)
        {
            // The labelled first order is carried to the conveyor by hand.
            if (_orderCartons.Length > 0 && carton == _orderCartons[0] && carton.IsLabelled && carton.IsLocked)
                carton.Unlock();
            Offer();
        }

        /// <summary>Called by the conveyor once it has taken a carton out to the dock.</summary>
        public void HandleDispatched(PackingCarton carton)
        {
            if (carton == null || carton != _active) return;

            carton.Changed -= HandleCartonChanged;
            _active = null;
            if (_orderIndex + 1 < _orders.Length)
            {
                BeginOrder(_orderIndex + 1);
                Say("Box dispatched. The next order is on the panel.", true);
            }
            else
            {
                _orderIndex = _orders.Length;
                ShowOrderText();
            }
        }

        private void HandleArmed(ChallengeTutorialTrigger trigger) => Offer();

        private void Offer()
        {
            PackingCarton first = _orderCartons.Length > 0 ? _orderCartons[0] : null;
            PackingCarton second = _orderCartons.Length > 1 ? _orderCartons[1] : null;
            Complete(_boxSelectedLatch, _boxSelectedTrigger, first != null);
            Complete(_baseFillLatch, _baseFillTrigger, first != null && first.IsBaseFilled);
            Complete(_topFillLatch, _topFillTrigger, first != null && first.IsFull);
            Complete(_sealedLatch, _sealedTrigger, first != null && first.IsSealed);
            Complete(_labelAppliedLatch, _labelAppliedTrigger, first != null && first.IsLabelled);
            Complete(_multiItemLatch, _multiItemTrigger, second != null && second.IsFull);
        }

        private static void Complete(ArmedLatch latch, ChallengeTutorialTrigger trigger, bool condition) =>
            latch.Offer(condition, () => trigger == null || trigger.TryComplete());

        private void Say(string message, bool good)
        {
            if (_feedback != null) _feedback.Show(message, good);
        }

        private void OnDrawGizmosSelected()
        {
            if (_spot == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.matrix = _spot.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0f, 0.1f, 0f), _spotHalfExtents * 2f);
        }
    }
}
```

- [ ] **Step 6: Give `AssessmentLauncher` an optional quiz anchor**

Replace the whole file `Assets/Scripts/ImmersiveTraining/TrainingInteractions/AssessmentLauncher.cs` with:

<!-- write: Assets/Scripts/ImmersiveTraining/TrainingInteractions/AssessmentLauncher.cs -->
```csharp
using UnityEngine;
using UnityEngine.Events;

using System.Collections;
using Valari.Managers;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// Launches the knowledge assessment when an assessment step's modal becomes active (M1_09, M2_10). Place this on
    /// the tutorial panel (or a child that is active only while that step is shown). On enable it calls
    /// <see cref="AssessmentManager.LaunchAssessment"/> so the quiz appears while the step's challenge is armed; a
    /// passing score is bridged back to the step by <see cref="AssessmentChallengeGate"/>.
    ///
    /// LaunchAssessment is a no-op until the assessment has been initialized (module started), so an
    /// early enable at scene load is ignored and does not consume the one-shot latch.
    ///
    /// The quiz UI lives at the spawn. With <see cref="_quizRoot"/> and <see cref="_quizAnchor"/> set, the quiz is
    /// moved to the anchor (where the trainee is) while this step runs and put back when the panel closes, so other
    /// modules' quizzes still appear at the spawn.
    /// </summary>
    public class AssessmentLauncher : MonoBehaviour
    {
        [Tooltip("The AssessmentManager to launch when this object is shown.")]
        [SerializeField] private AssessmentManager _assessmentManager;

        [Tooltip("If true, only launches the first time it is successfully shown.")]
        [SerializeField] private bool _launchOnce = true;

        [Tooltip("Optional: a UI object (the step's modal container) to hide while the quiz is on screen. AssessmentChallengeGate re-shows it on a pass.")]
        [SerializeField] private GameObject _hideDuringQuiz;

        [Header("Quiz placement (optional)")]
        [Tooltip("The quiz UI root (Canvas/AssesmentModalUI).")]
        [SerializeField] private Transform _quizRoot;

        [Tooltip("Pose the quiz root takes while this step runs.")]
        [SerializeField] private Transform _quizAnchor;

        [SerializeField] private UnityEvent _onLaunched;

        private bool _launched;
        private bool _quizMoved;
        private Vector3 _quizHomePosition;
        private Quaternion _quizHomeRotation;

        private void OnEnable()
        {
            if (_launchOnce && _launched) return;
            if (_assessmentManager == null) return;

            // Only latch if the launch actually happened (assessment initialized / ready).
            if (_assessmentManager.LaunchAssessment())
            {
                _launched = true;
                MoveQuizToAnchor();
                _onLaunched?.Invoke();
                if (_hideDuringQuiz != null)
                    StartCoroutine(HideDuringQuizRoutine());
            }
        }

        private void OnDisable() => RestoreQuiz();

        // The tutorial framework activates the modal container a frame or two after this panel is
        // enabled (via its show coroutine). Wait until it is visible, then hide it so only the quiz
        // is on screen; AssessmentChallengeGate re-shows it on a pass so "Next" is reachable.
        private IEnumerator HideDuringQuizRoutine()
        {
            float timeout = 3f;
            while (timeout > 0f && _hideDuringQuiz != null && !_hideDuringQuiz.activeSelf)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            if (_hideDuringQuiz != null)
                _hideDuringQuiz.SetActive(false);
        }

        private void MoveQuizToAnchor()
        {
            if (_quizRoot == null || _quizAnchor == null || _quizMoved) return;
            _quizHomePosition = _quizRoot.position;
            _quizHomeRotation = _quizRoot.rotation;
            _quizRoot.SetPositionAndRotation(_quizAnchor.position, _quizAnchor.rotation);
            _quizMoved = true;
        }

        private void RestoreQuiz()
        {
            if (!_quizMoved || _quizRoot == null) return;
            _quizRoot.SetPositionAndRotation(_quizHomePosition, _quizHomeRotation);
            _quizMoved = false;
        }

        public void ResetLauncher() => _launched = false;
    }
}
```

- [ ] **Step 7: Compile in the live editor**

Run (MCP): `refresh_unity` (mode `if_dirty`, scope `all`, compile `request`), then `read_console` (types error).
Expected: no `error CS`. Re-run `dotnet run --project Tests/PackingDispatchLogic` → 31 passed.

- [ ] **Step 8: Checkpoint** — no commit.

---

### Task 3: Builder — prefabs

**Files:**
- Create: `Assets/Editor/PackingStationBuilder.cs` (the whole builder; Task 4 runs its station/M1_09 menus)
- Output: `Assets/Prefabs/PackingDispatch/` — `Carton_Small/Medium/Large`, `AirPillow`, `Product_Speaker/DeskLamp/BookSet/CableKit`, `ShippingLabel_ORD-2001`, `TapeGun`, `BoxCutter` + `Materials/`

**Interfaces:**
- Consumes: Task 2 components (private serialized field names exactly as written there).
- Produces: menu items `Tools/Packing Dispatch/1. Build Prefabs`, `2. Build Station + Wire M2`, `3. Restore M1_09 Assessment`.

- [ ] **Step 1: Write the builder**

<!-- write: Assets/Editor/PackingStationBuilder.cs -->
```csharp
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autohand;
using ImmersiveTraining.TrainingInteractions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Valari.Data;
using Valari.Managers;
using Valari.UI;

/// <summary>
/// Builds Module 2 (Packing and Dispatch): prefabs, the packing station in BasicScene and its wiring to the M2
/// tutorial panels; also restores the M1_09 assessment panel. Re-runnable: "Build Station" replaces the previous
/// M2_PackingStation and QC panel (manual tweaks to them are lost) and re-creates the M2 trigger objects.
/// Each step logs a [PackingBuilder] summary. Spec: docs/superpowers/specs/2026-10-06-packing-dispatch-module-design.md
/// </summary>
public static class PackingStationBuilder
{
    private const string Folder = "Assets/Prefabs/PackingDispatch";
    private const string MaterialFolder = Folder + "/Materials";
    private const string StationName = "M2_PackingStation";
    private const string QcPanelName = "M2_QCPanel";
    private const string PanelGroupName = "PackingAndDispatch";
    private const string LabelSku = "LBL-ORD-2001";
    private const float Wall = PackingCarton.WallThickness;

    private static readonly Color Ink = new Color(0.08f, 0.08f, 0.1f);

    private const string Order1Label =
        "SHIP TO: R. OKAFOR\n18 WHARF RD, PORT KEMBLA NSW 2505\nORD-2001   WT CLASS B (1-2 kg)\nCARRIER: AXE - AXL EXPRESS";

    private const string QcLabel =
        "SHIP TO: T. NGUYEN\n9 BAYVIEW TCE, HOBART TAS 7000\nORD-1874   WT CLASS B (1-2 kg)\nCARRIER: AXE - AXL EXPRESS";

    private const string Order1Panel =
        "ORDER ORD-2001  (1 item)\n" +
        "Wireless Speaker   24 x 14 x 10 cm   1.2 kg\n" +
        "Ship to: R. Okafor, 18 Wharf Rd, Port Kembla NSW 2505\n" +
        "Weight class B (1-2 kg)    Carrier: AXE (AXL Express)\n" +
        "Cartons: S 30x20x15   M 45x35x25   L 60x50x40 cm";

    private const string Order2Panel =
        "ORDER ORD-2002  (3 items)\n" +
        "Desk Lamp 30x12x12 | Book Set 24x17x8 | Cable Kit 15x10x6 cm\n" +
        "Packed together: 32 x 30 x 14 cm   2.9 kg\n" +
        "Ship to: M. Delacroix, 4/77 Ocean St, Narrabeen NSW 2101\n" +
        "Weight class C (2-5 kg)    Carrier: AXE (AXL Express)\n" +
        "Cartons: S 30x20x15   M 45x35x25   L 60x50x40 cm";

    private const string QcOrderText =
        "Ceramic Mug Set 20 x 20 x 12 cm, 1.6 kg - Medium carton\n" +
        "Ship to: T. Nguyen, 9 Bayview Tce, Hobart TAS 7000\n" +
        "Weight class B (1-2 kg)    Carrier: AXE (AXL Express)";

    // Panel -> challenge trigger names, as in Assets/ReferenceData/training_flow_module2.json.
    private static readonly (string Panel, string[] Triggers)[] M2Steps =
    {
        ("M2_02_Station_Layout", new[] { "TRIGGER_M2_StationLayoutConfirmed" }),
        ("M2_03_Box_Selection", new[] { "TRIGGER_M2_CorrectBoxSelected" }),
        ("M2_04_Void_Fill_Application", new[] { "TRIGGER_M2_VoidFillBase", "TRIGGER_M2_VoidFillTop" }),
        ("M2_05_Sealing_the_Box", new[] { "TRIGGER_M2_BoxSealed" }),
        ("M2_06_Label_Verification", new[] { "TRIGGER_M2_LabelVerified", "TRIGGER_M2_LabelApplied" }),
        ("M2_07_Conveyor_Loading", new[] { "TRIGGER_M2_BoxOnConveyor" }),
        ("M2_08_Multi_Item_Consolidation", new[] { "TRIGGER_M2_MultiItemPacked" }),
        ("M2_09_Error_Identification", new[] { "TRIGGER_M2_ErrorIdentified", "TRIGGER_M2_ErrorCorrected" }),
        ("M2_10_Assessment", new[] { "TRIGGER_M2_AssessmentComplete" }),
    };

    private static readonly string[] M2PanelNames =
    {
        "M2_01_Welcome_Packing", "M2_02_Station_Layout", "M2_03_Box_Selection", "M2_04_Void_Fill_Application",
        "M2_05_Sealing_the_Box", "M2_06_Label_Verification", "M2_07_Conveyor_Loading", "M2_08_Multi_Item_Consolidation",
        "M2_09_Error_Identification", "M2_10_Assessment", "M2_11_Module_Complete",
    };

    // Modal poses (world). Trainee faces +x at the bench, so a panel's rotation is the direction the trainee looks.
    private static readonly Vector3 StationModalPosition = new Vector3(16.35f, 1.45f, 14.75f);
    private static readonly Quaternion StationModalRotation = Quaternion.Euler(0f, 35f, 0f);
    private static readonly Vector3 QcModalPosition = new Vector3(16.3f, 1.45f, 8.95f);
    private static readonly Quaternion QcModalRotation = Quaternion.Euler(0f, 134f, 0f);

    private static Dictionary<string, Material> _materials;

    // =====================================================================================================
    // 1. Prefabs
    // =====================================================================================================

    [MenuItem("Tools/Packing Dispatch/1. Build Prefabs")]
    public static void BuildPrefabs()
    {
        LoadMaterials();
        EnsureFolder(Folder);

        foreach (CartonSize size in new[] { CartonSize.Small, CartonSize.Medium, CartonSize.Large })
            BuildCarton(size);

        BuildProduct("Speaker", "Wireless Speaker", new Vector3(24f, 10f, 14f), 1.2f, _materials["ProductBlue"]);
        BuildProduct("DeskLamp", "Desk Lamp", new Vector3(30f, 12f, 12f), 1.4f, _materials["ProductOrange"]);
        BuildProduct("BookSet", "Book Set", new Vector3(24f, 8f, 17f), 1.2f, _materials["ProductPurple"]);
        BuildProduct("CableKit", "Cable Kit", new Vector3(15f, 6f, 10f), 0.3f, _materials["ProductGreen"]);
        BuildPillow();
        BuildLabel();
        BuildTapeGun();
        BuildBoxCutter();

        AssetDatabase.SaveAssets();
        Debug.Log($"[PackingBuilder] Prefabs built in {Folder}: 3 cartons, 4 products, air pillow, shipping label, tape gun, box cutter.");
    }

    private static void BuildCarton(CartonSize size)
    {
        Size3 cm = CartonFit.InnerSize(size);
        float l = cm.X * 0.01f, h = cm.Y * 0.01f, w = cm.Z * 0.01f, t = Wall, top = h + 2f * t;
        var root = new GameObject($"Carton_{size}");
        try
        {
            Material board = _materials["Cardboard"], dark = _materials["CardboardDark"];
            Transform walls = Empty("Walls", root.transform, Vector3.zero).transform;
            Box("Bottom", walls, new Vector3(0f, t / 2f, 0f), new Vector3(l + 2f * t, t, w + 2f * t), board, true);
            Box("Side_-Z", walls, new Vector3(0f, t + h / 2f, -(w / 2f + t / 2f)), new Vector3(l + 2f * t, h, t), board, true);
            Box("Side_+Z", walls, new Vector3(0f, t + h / 2f, w / 2f + t / 2f), new Vector3(l + 2f * t, h, t), board, true);
            Box("End_-X", walls, new Vector3(-(l / 2f + t / 2f), t + h / 2f, 0f), new Vector3(t, h, w), board, true);
            Box("End_+X", walls, new Vector3(l / 2f + t / 2f, t + h / 2f, 0f), new Vector3(t, h, w), board, true);

            GameObject flaps = Empty("OpenFlaps", root.transform, Vector3.zero);
            Flap(flaps.transform, "Flap_-Z", new Vector3(0f, h + t, -(w / 2f + t)), new Vector3(0f, 0.866f, -0.5f), l + 2f * t, w / 2f, dark);
            Flap(flaps.transform, "Flap_+Z", new Vector3(0f, h + t, w / 2f + t), new Vector3(0f, 0.866f, 0.5f), l + 2f * t, w / 2f, dark);
            Flap(flaps.transform, "Flap_-X", new Vector3(-(l / 2f + t), h + t, 0f), new Vector3(-0.5f, 0.866f, 0f), w, Mathf.Min(l, w) * 0.4f, dark);
            Flap(flaps.transform, "Flap_+X", new Vector3(l / 2f + t, h + t, 0f), new Vector3(0.5f, 0.866f, 0f), w, Mathf.Min(l, w) * 0.4f, dark);

            GameObject lid = Empty("Lid", root.transform, Vector3.zero);
            Box("LidPanel", lid.transform, new Vector3(0f, h + 1.5f * t, 0f), new Vector3(l + 2f * t, t, w + 2f * t), board, true);
            Box("FlapJoint", lid.transform, new Vector3(0f, top + 0.0004f, 0f), new Vector3(l, 0.0008f, 0.003f), _materials["CardboardSeam"]);
            lid.SetActive(false);

            GameObject fill = Box("Fill", root.transform, new Vector3(0f, t, 0f), new Vector3(l - 0.01f, 0.001f, w - 0.01f), _materials["Pillow"]);
            fill.SetActive(false);

            Transform tape = Empty("Tape", root.transform, Vector3.zero).transform;
            Quaternion across = Quaternion.Euler(0f, 90f, 0f);
            var strips = new Object[]
            {
                Box("Tape_Centre", tape, new Vector3(0f, top + 0.0015f, 0f), new Vector3(l, 0.002f, 0.048f), _materials["Tape"]).transform,
                Box("Tape_EndA", tape, new Vector3(-l / 2f, top + 0.0015f, 0f), new Vector3(w, 0.002f, 0.048f), _materials["Tape"], false, across).transform,
                Box("Tape_EndB", tape, new Vector3(l / 2f, top + 0.0015f, 0f), new Vector3(w, 0.002f, 0.048f), _materials["Tape"], false, across).transform,
            };
            var guides = new Object[]
            {
                Box("Guide_Centre", tape, new Vector3(0f, top + 0.0008f, 0f), new Vector3(l, 0.001f, 0.06f), _materials["Guide"]),
                Box("Guide_EndA", tape, new Vector3(-l / 2f, top + 0.0008f, 0f), new Vector3(w, 0.001f, 0.06f), _materials["Guide"], false, across),
                Box("Guide_EndB", tape, new Vector3(l / 2f, top + 0.0008f, 0f), new Vector3(w, 0.001f, 0.06f), _materials["Guide"], false, across),
            };
            foreach (Object o in strips) ((Transform)o).gameObject.SetActive(false);
            foreach (Object o in guides) ((GameObject)o).SetActive(false);

            GameObject label = Empty("LabelVisual", root.transform, new Vector3(l * 0.22f, top + 0.002f, 0f));
            LabelFace(label.transform, Order1Label);
            label.SetActive(false);

            Transform contents = Empty("Contents", root.transform, Vector3.zero).transform;

            string mark = $"{size.ToString().ToUpperInvariant()}\n{cm.X:0}x{cm.Z:0}x{cm.Y:0}";
            float markSize = Mathf.Clamp(h * 2.2f, 0.3f, 0.6f);
            Text3D("SizeMark_-X", root.transform, new Vector3(-(l / 2f + t + 0.002f), t + h / 2f, 0f), Quaternion.Euler(0f, 90f, 0f), mark, markSize, new Vector2(w * 0.9f, h * 0.8f), Ink);
            Text3D("SizeMark_+X", root.transform, new Vector3(l / 2f + t + 0.002f, t + h / 2f, 0f), Quaternion.Euler(0f, -90f, 0f), mark, markSize, new Vector2(w * 0.9f, h * 0.8f), Ink);

            float mass = size == CartonSize.Small ? 0.4f : size == CartonSize.Medium ? 0.7f : 1f;
            Grabbable grab = AddGrabbable(root, mass, false);
            grab.heldNoFriction = false;
            PackingCarton carton = root.AddComponent<PackingCarton>();
            Set(carton, "_size", (int)size);
            Set(carton, "_innerSize", new Vector3(l, h, w));
            Set(carton, "_grabbable", grab);
            Set(carton, "_body", root.GetComponent<Rigidbody>());
            Set(carton, "_openFlaps", flaps);
            Set(carton, "_lid", lid);
            Set(carton, "_fillVisual", fill.transform);
            SetArray(carton, "_tapeStrips", strips);
            SetArray(carton, "_seamGuides", guides);
            Set(carton, "_labelVisual", label);
            Set(carton, "_contentsRoot", contents);

            PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/Carton_{size}.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void Flap(Transform parent, string name, Vector3 hinge, Vector3 direction, float across, float depth, Material material)
    {
        Vector3 dir = direction.normalized;
        Box(name, parent, hinge + dir * depth / 2f, new Vector3(across, Wall, depth), material, false, Quaternion.LookRotation(dir, Vector3.up));
    }

    private static void BuildProduct(string id, string display, Vector3 sizeCm, float mass, Material material)
    {
        var root = new GameObject($"Product_{id}");
        try
        {
            Vector3 s = sizeCm * 0.01f;
            Box("Box", root.transform, Vector3.zero, s, material);
            Text3D("Name_Top", root.transform, new Vector3(0f, s.y / 2f + 0.001f, 0f), Quaternion.Euler(90f, 0f, 0f), display, 0.3f, new Vector2(s.x * 0.9f, s.z * 0.8f), Color.white);
            Text3D("Name_-Z", root.transform, new Vector3(0f, 0f, -(s.z / 2f + 0.001f)), Quaternion.identity, display, 0.3f, new Vector2(s.x * 0.9f, s.y * 0.8f), Color.white);
            Text3D("Name_+Z", root.transform, new Vector3(0f, 0f, s.z / 2f + 0.001f), Quaternion.Euler(0f, 180f, 0f), display, 0.3f, new Vector2(s.x * 0.9f, s.y * 0.8f), Color.white);

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size = s;
            Grabbable grab = AddGrabbable(root, mass, true);
            PackableItem item = root.AddComponent<PackableItem>();
            Set(item, "_displayName", display);
            Set(item, "_sizeCm", sizeCm);
            Set(item, "_grabbable", grab);
            Set(item, "_body", root.GetComponent<Rigidbody>());

            PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/Product_{id}.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void BuildPillow()
    {
        var root = new GameObject("AirPillow");
        try
        {
            Prim(PrimitiveType.Sphere, "Pillow", root.transform, Vector3.zero, new Vector3(0.11f, 0.045f, 0.085f), _materials["Pillow"]);
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.1f, 0.04f, 0.08f);
            Grabbable grab = AddGrabbable(root, 0.03f, true);
            VoidFillPiece piece = root.AddComponent<VoidFillPiece>();
            Set(piece, "_grabbable", grab);
            Set(piece, "_body", root.GetComponent<Rigidbody>());
            PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/AirPillow.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void BuildLabel()
    {
        var root = new GameObject("ShippingLabel_ORD-2001");
        try
        {
            LabelFace(root.transform, Order1Label);
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.15f, 0.012f, 0.1f);
            Grabbable grab = AddGrabbable(root, 0.02f, true);
            SkuItem sku = root.AddComponent<SkuItem>();
            Set(sku, "_sku", LabelSku);
            Set(sku, "_description", "Shipping label ORD-2001");
            ShippingLabel label = root.AddComponent<ShippingLabel>();
            Set(label, "_grabbable", grab);
            Set(label, "_body", root.GetComponent<Rigidbody>());
            PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/ShippingLabel_ORD-2001.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    /// <summary>A 15 x 10 cm white label lying flat (text up, reading along +X), barcode along the -Z edge.</summary>
    private static void LabelFace(Transform parent, string text)
    {
        Box("Card", parent, Vector3.zero, new Vector3(0.15f, 0.003f, 0.1f), _materials["LabelPaper"]);
        TextMeshPro tmp = Text3D("Text", parent, new Vector3(0f, 0.0018f, 0.012f), Quaternion.Euler(90f, 0f, 0f), text, 0.09f, new Vector2(0.14f, 0.065f), Ink, TextAlignmentOptions.TopLeft);
        tmp.fontSizeMin = 0.03f;
        float[] widths = { 0.002f, 0.001f, 0.003f, 0.001f, 0.002f, 0.004f, 0.001f, 0.002f, 0.001f, 0.003f, 0.002f, 0.001f, 0.003f, 0.001f, 0.002f, 0.004f, 0.001f, 0.002f };
        float x = -0.05f;
        foreach (float width in widths)
        {
            Box("Bar", parent, new Vector3(x + width / 2f, 0.0017f, -0.035f), new Vector3(width, 0.0004f, 0.02f), _materials["Ink"]);
            x += width + 0.0025f;
        }
    }

    private static void BuildTapeGun()
    {
        var root = new GameObject("TapeGun");
        try
        {
            Box("Body", root.transform, new Vector3(0f, 0.05f, 0f), new Vector3(0.035f, 0.035f, 0.17f), _materials["ToolRed"]);
            Box("Handle", root.transform, new Vector3(0f, 0f, -0.055f), new Vector3(0.03f, 0.11f, 0.035f), _materials["ToolRed"], false, Quaternion.Euler(-15f, 0f, 0f));
            Prim(PrimitiveType.Cylinder, "TapeRoll", root.transform, new Vector3(0f, 0.07f, 0.02f), new Vector3(0.1f, 0.025f, 0.1f), _materials["Tape"], false, Quaternion.Euler(0f, 0f, 90f));
            Box("Head", root.transform, new Vector3(0f, -0.005f, 0.08f), new Vector3(0.05f, 0.02f, 0.03f), _materials["ToolGrey"]);
            GameObject head = Empty("HeadPoint", root.transform, new Vector3(0f, -0.02f, 0.09f));
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.03f, 0.01f);
            collider.size = new Vector3(0.06f, 0.14f, 0.2f);
            Grabbable grab = AddGrabbable(root, 0.5f, true);
            SeamTool tool = root.AddComponent<SeamTool>();
            Set(tool, "_kind", (int)SeamToolKind.Tape);
            Set(tool, "_head", head.transform);
            Set(tool, "_grabbable", grab);
            PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/TapeGun.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void BuildBoxCutter()
    {
        var root = new GameObject("BoxCutter");
        try
        {
            Box("Body", root.transform, Vector3.zero, new Vector3(0.028f, 0.018f, 0.13f), _materials["CutterYellow"]);
            Box("Blade", root.transform, new Vector3(0f, -0.002f, 0.078f), new Vector3(0.003f, 0.012f, 0.026f), _materials["Steel"]);
            GameObject head = Empty("HeadPoint", root.transform, new Vector3(0f, -0.006f, 0.09f));
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0f, 0.015f);
            collider.size = new Vector3(0.03f, 0.02f, 0.16f);
            Grabbable grab = AddGrabbable(root, 0.15f, true);
            SeamTool tool = root.AddComponent<SeamTool>();
            Set(tool, "_kind", (int)SeamToolKind.Cutter);
            Set(tool, "_head", head.transform);
            Set(tool, "_grabbable", grab);
            PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/BoxCutter.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // =====================================================================================================
    // 2. Station + M2 wiring
    // =====================================================================================================

    [MenuItem("Tools/Packing Dispatch/2. Build Station + Wire M2")]
    public static void BuildStation()
    {
        LoadMaterials();
        var cartons = new Dictionary<CartonSize, PackingCarton>();
        foreach (CartonSize size in new[] { CartonSize.Small, CartonSize.Medium, CartonSize.Large })
            cartons[size] = LoadPrefab<PackingCarton>($"Carton_{size}");
        PackableItem speaker = LoadPrefab<PackableItem>("Product_Speaker");
        PackableItem lamp = LoadPrefab<PackableItem>("Product_DeskLamp");
        PackableItem books = LoadPrefab<PackableItem>("Product_BookSet");
        PackableItem cables = LoadPrefab<PackableItem>("Product_CableKit");
        VoidFillPiece pillow = LoadPrefab<VoidFillPiece>("AirPillow");
        ShippingLabel labelPrefab = LoadPrefab<ShippingLabel>("ShippingLabel_ORD-2001");
        SeamTool tapeGunPrefab = LoadPrefab<SeamTool>("TapeGun");
        SeamTool cutterPrefab = LoadPrefab<SeamTool>("BoxCutter");
        if (cartons.Values.Any(c => c == null) || new Object[] { speaker, lamp, books, cables, pillow, labelPrefab, tapeGunPrefab, cutterPrefab }.Any(o => o == null))
        {
            Debug.LogError("[PackingBuilder] Prefabs missing - run 'Tools/Packing Dispatch/1. Build Prefabs' first.");
            return;
        }

        GameObject canvas = FindRoot("Canvas");
        TutorialManager tutorialManager = Object.FindFirstObjectByType<TutorialManager>(FindObjectsInactive.Include);
        AssessmentManager assessmentManager = Object.FindFirstObjectByType<AssessmentManager>(FindObjectsInactive.Include);
        AssessmentUI quizUi = Object.FindFirstObjectByType<AssessmentUI>(FindObjectsInactive.Include);
        AssessmentFailPanel failPanel = Object.FindFirstObjectByType<AssessmentFailPanel>(FindObjectsInactive.Include);
        GameObject m1Scanner = FindRoot("M1_Scanner");
        Dictionary<string, TutorialModalUI> panels = ScenePanels();
        string missing = string.Join(", ", new (string, Object)[]
        {
            ("Canvas", canvas), ("TutorialManager", tutorialManager), ("AssessmentManager", assessmentManager),
            ("AssessmentUI", quizUi), ("AssessmentFailPanel", failPanel), ("M1_Scanner", m1Scanner),
        }.Where(p => p.Item2 == null).Select(p => p.Item1).Concat(M2PanelNames.Where(n => !panels.ContainsKey(n))));
        if (missing.Length > 0)
        {
            Debug.LogError($"[PackingBuilder] Scene objects missing: {missing}");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Build packing station");
        int undoGroup = Undo.GetCurrentGroup();

        GameObject oldStation = FindRoot(StationName);
        if (oldStation != null) Undo.DestroyObjectImmediate(oldStation);
        Transform oldQc = canvas.transform.Find(QcPanelName);
        if (oldQc != null) Undo.DestroyObjectImmediate(oldQc.gameObject);

        // ---- panels and triggers first (the station wires to the triggers) ----
        TutorialModalUI m2_10 = EnsureChallengeModal(panels, "M2_10_Assessment", "M2_09_Error_Identification", tutorialManager, TrainingID.PackingAndDispatch);
        panels["M2_10_Assessment"] = m2_10;
        GameObject group = EnsurePanelGroup(canvas.transform);
        Transform m1Welcome = panels.TryGetValue("M1_01_Welcome_Order_Picking", out TutorialModalUI w) ? w.transform : null;
        foreach (string name in M2PanelNames)
        {
            Transform panel = panels[name].transform;
            if (panel.parent != group.transform) Undo.SetTransformParent(panel, group.transform, "Move M2 panel");
            Undo.RecordObject(panel, "Place M2 panel");
            if (name == "M2_01_Welcome_Packing" || name == "M2_02_Station_Layout")
            {
                if (m1Welcome != null) panel.SetPositionAndRotation(m1Welcome.position, m1Welcome.rotation);
            }
            else if (name == "M2_09_Error_Identification") panel.SetPositionAndRotation(QcModalPosition, QcModalRotation);
            else panel.SetPositionAndRotation(StationModalPosition, StationModalRotation);
        }

        var triggers = new Dictionary<string, ChallengeTutorialTrigger>();
        foreach ((string panelName, string[] names) in M2Steps)
        {
            TutorialModalUI modal = panels[panelName];
            RemoveChildren(modal.transform, "Trigger_");
            var list = new List<Object>();
            foreach (string triggerName in names)
            {
                ChallengeTutorialTrigger trigger = CreateTrigger(modal.transform, triggerName);
                triggers[triggerName] = trigger;
                list.Add(trigger);
            }

            Set(modal, "_hasChallenges", true);
            Set(modal, "_enableAutoNext", panelName == "M2_02_Station_Layout");
            SetArray(modal, "_challengeTutorialTriggerList", list);
            if (Get<Transform>(modal, "_challengeHolder") == null)
                Debug.LogError($"[PackingBuilder] {panelName} has no challenge holder; its checklist will not show.");
        }

        UpdateM2AssessmentData();

        // ---- station ----
        var root = new GameObject(StationName);
        Undo.RegisterCreatedObjectUndo(root, "Build packing station");

        GameObject bench = BuildBench(root.transform, out Transform spot, out Transform[] displaySlots, out TextMeshPro orderText,
            out PackingFeedback feedback, out GameObject readout);
        PackingStation station = bench.AddComponent<PackingStation>();

        Transform[] rackSlots = BuildCartonRack(root.transform);
        VoidFillDispenser dispenser = BuildDispenser(root.transform, pillow);
        ShippingLabel label = BuildPrinterAndLabel(root.transform, labelPrefab);

        var tapeGun = (SeamTool)PrefabUtility.InstantiatePrefab(tapeGunPrefab, root.transform);
        tapeGun.transform.SetPositionAndRotation(new Vector3(15.75f, 0.96f, 12.6f), Quaternion.Euler(0f, 90f, 0f));

        GameObject scanner = Object.Instantiate(m1Scanner, root.transform);
        scanner.name = "M2_Scanner";
        scanner.transform.SetPositionAndRotation(new Vector3(15.75f, 0.98f, 13.9f), m1Scanner.transform.rotation);
        ScannerTool scannerTool = scanner.GetComponent<ScannerTool>();
        Set(scannerTool, "_expectedSku", LabelSku);
        Set(scannerTool, "_challengeTrigger", null);

        ConveyorLoadZone conveyor = BuildConveyor(root.transform);
        PackingCarton qcCarton = BuildQcTable(root.transform, cartons[CartonSize.Medium], cutterPrefab);
        (QcReview qc, PackingFeedback qcFeedback) = BuildQcPanel(canvas.transform);
        Set(qcCarton, "_feedback", qcFeedback);

        var layoutGate = Empty("M2_StationLayoutGate", root.transform, Vector3.zero).AddComponent<MultiCounterGate>();
        Set(layoutGate, "_requiredCount", 3);
        Set(layoutGate, "_challengeTrigger", triggers["TRIGGER_M2_StationLayoutConfirmed"]);
        var signs = new[]
        {
            BuildSign(root.transform, "Sign_Cartons", "CARTONS", new Vector3(16.25f, 2.0f, 15.05f), layoutGate),
            BuildSign(root.transform, "Sign_WorkArea", "WORK AREA", new Vector3(16.45f, 2.0f, 13.0f), layoutGate),
            BuildSign(root.transform, "Sign_LabelsConveyor", "LABELS & CONVEYOR", new Vector3(16.25f, 2.0f, 11.7f), layoutGate),
        };

        // ---- wiring ----
        SetOrders(station, new[]
        {
            ("ORD-2001", Order1Panel, new Vector3(24f, 10f, 14f), new Object[] { speaker }),
            ("ORD-2002", Order2Panel, new Vector3(32f, 14f, 30f), new Object[] { lamp, books, cables }),
        });
        SetArray(station, "_displaySlots", displaySlots);
        SetSupplies(station, new[]
        {
            (CartonSize.Small, cartons[CartonSize.Small], rackSlots[0]),
            (CartonSize.Medium, cartons[CartonSize.Medium], rackSlots[1]),
            (CartonSize.Large, cartons[CartonSize.Large], rackSlots[2]),
        });
        Set(station, "_spot", spot);
        Set(station, "_orderText", orderText);
        Set(station, "_feedback", feedback);
        Set(station, "_label", label);
        Set(station, "_conveyor", conveyor);
        Set(station, "_layoutGate", layoutGate);
        SetArray(station, "_signs", signs);
        Set(station, "_qcReview", qc);
        Set(station, "_boxSelectedTrigger", triggers["TRIGGER_M2_CorrectBoxSelected"]);
        Set(station, "_baseFillTrigger", triggers["TRIGGER_M2_VoidFillBase"]);
        Set(station, "_topFillTrigger", triggers["TRIGGER_M2_VoidFillTop"]);
        Set(station, "_sealedTrigger", triggers["TRIGGER_M2_BoxSealed"]);
        Set(station, "_labelAppliedTrigger", triggers["TRIGGER_M2_LabelApplied"]);
        Set(station, "_multiItemTrigger", triggers["TRIGGER_M2_MultiItemPacked"]);

        Set(label, "_feedback", feedback);
        Set(label, "_verifiedReadout", readout);
        Set(label, "_verifiedTrigger", triggers["TRIGGER_M2_LabelVerified"]);

        Set(conveyor, "_station", station);
        Set(conveyor, "_feedback", feedback);
        Set(conveyor, "_challengeTrigger", triggers["TRIGGER_M2_BoxOnConveyor"]);

        Set(qc, "_carton", qcCarton);
        Set(qc, "_identifiedTrigger", triggers["TRIGGER_M2_ErrorIdentified"]);
        Set(qc, "_correctedTrigger", triggers["TRIGGER_M2_ErrorCorrected"]);

        // ---- M2_10 assessment ----
        GameObject container = Get<GameObject>(m2_10, "_container");
        Transform quizContainer = quizUi.transform.Find("Container");
        GameObject quizAnchor = Empty("QuizAnchor_M2", root.transform, Vector3.zero);
        PlaceQuizAnchor(quizAnchor.transform, quizUi.transform, quizContainer, container != null ? container.transform : m2_10.transform);
        AssessmentLauncher launcher = m2_10.GetComponent<AssessmentLauncher>();
        if (launcher == null) launcher = Undo.AddComponent<AssessmentLauncher>(m2_10.gameObject);
        Set(launcher, "_assessmentManager", assessmentManager);
        Set(launcher, "_launchOnce", true);
        Set(launcher, "_hideDuringQuiz", container);
        Set(launcher, "_quizRoot", quizUi.transform);
        Set(launcher, "_quizAnchor", quizAnchor.transform);

        AssessmentChallengeGate gate = Empty("M2_AssessmentGate", root.transform, Vector3.zero).AddComponent<AssessmentChallengeGate>();
        Set(gate, "_assessmentManager", assessmentManager);
        Set(gate, "_trainingId", (int)TrainingID.PackingAndDispatch);
        Set(gate, "_passPercent", 80);
        Set(gate, "_retakeOnFail", true);
        Set(gate, "_challengeTrigger", triggers["TRIGGER_M2_AssessmentComplete"]);
        Set(gate, "_showOnPass", container);
        Set(gate, "_failPanel", failPanel);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[PackingBuilder] Station built: {root.transform.childCount} station parts, {triggers.Count} M2 triggers wired, " +
                  $"M2 panels moved under Canvas/{PanelGroupName}, M2_10 is a challenge modal with launcher + gate. Save the scene.");
    }

    private static GameObject BuildBench(Transform root, out Transform spot, out Transform[] displaySlots, out TextMeshPro orderText,
        out PackingFeedback feedback, out GameObject readout)
    {
        GameObject bench = Empty("Bench", root, new Vector3(16f, 0f, 13f));
        Transform b = bench.transform;
        Box("Top", b, new Vector3(0f, 0.88f, 0f), new Vector3(0.8f, 0.04f, 2.4f), _materials["BenchTop"], true);
        Box("Shelf", b, new Vector3(0f, 0.25f, 0f), new Vector3(0.74f, 0.02f, 2.3f), _materials["Steel"]);
        foreach (float x in new[] { -0.36f, 0.36f })
        foreach (float z in new[] { -1.15f, 1.15f })
            Box("Leg", b, new Vector3(x, 0.43f, z), new Vector3(0.05f, 0.86f, 0.05f), _materials["Steel"]);
        foreach (float z in new[] { -0.5f, 0.5f })
            Box("PanelPost", b, new Vector3(0.43f, 1.33f, z), new Vector3(0.04f, 0.86f, 0.04f), _materials["Steel"]);
        Box("OrderBoard", b, new Vector3(0.43f, 1.43f, 0f), new Vector3(0.02f, 0.56f, 0.95f), _materials["PanelBack"]);

        Quaternion facing = Quaternion.Euler(0f, 90f, 0f); // readable from the trainee's side (looking +x)
        orderText = Text3D("OrderText", b, new Vector3(0.415f, 1.52f, 0f), facing, "", 0.2f, new Vector2(0.88f, 0.34f), Color.white, TextAlignmentOptions.TopLeft);
        orderText.fontSizeMin = 0.08f;
        TextMeshPro readoutText = Text3D("LabelReadout", b, new Vector3(0.415f, 1.3f, 0f), facing,
            "LABEL CHECK: address OK - weight class OK - carrier OK", 0.16f, new Vector2(0.88f, 0.06f), new Color(0.3f, 0.9f, 0.4f));
        readoutText.fontSizeMin = 0.06f;
        readout = readoutText.gameObject;
        readout.SetActive(false);
        TextMeshPro feedbackText = Text3D("Feedback", b, new Vector3(0.415f, 1.2f, 0f), facing, "", 0.17f, new Vector2(0.88f, 0.11f), Color.white);
        feedbackText.fontSizeMin = 0.06f;
        feedback = feedbackText.gameObject.AddComponent<PackingFeedback>();
        Set(feedback, "_text", feedbackText);

        spot = Empty("PackingSpot", b, new Vector3(-0.15f, 0.9f, 0.2f), Quaternion.Euler(0f, 90f, 0f)).transform;
        Material guide = _materials["Guide"];
        Box("SpotLine_Front", b, new Vector3(-0.38f, 0.901f, 0.2f), new Vector3(0.012f, 0.002f, 0.66f), guide);
        Box("SpotLine_Back", b, new Vector3(0.12f, 0.901f, 0.2f), new Vector3(0.012f, 0.002f, 0.66f), guide);
        Box("SpotLine_Left", b, new Vector3(-0.13f, 0.901f, 0.53f), new Vector3(0.5f, 0.002f, 0.012f), guide);
        Box("SpotLine_Right", b, new Vector3(-0.13f, 0.901f, -0.13f), new Vector3(0.5f, 0.002f, 0.012f), guide);
        Text3D("PackHere", b, new Vector3(-0.36f, 0.902f, 0.2f), Quaternion.Euler(90f, 90f, 0f), "PACK HERE", 0.3f, new Vector2(0.5f, 0.05f), guide.GetColor("_BaseColor"));

        displaySlots = new[]
        {
            Empty("DisplaySlot_0", b, new Vector3(0.25f, 1f, -0.4f), Quaternion.Euler(0f, 90f, 0f)).transform,
            Empty("DisplaySlot_1", b, new Vector3(0.25f, 1f, 0.2f), Quaternion.Euler(0f, 90f, 0f)).transform,
            Empty("DisplaySlot_2", b, new Vector3(0.25f, 1f, 0.8f), Quaternion.Euler(0f, 90f, 0f)).transform,
        };
        return bench;
    }

    private static Transform[] BuildCartonRack(Transform root)
    {
        GameObject rack = Empty("CartonRack", root, new Vector3(16f, 0f, 15.05f));
        Transform r = rack.transform;
        Box("Top", r, new Vector3(0f, 0.735f, 0f), new Vector3(0.8f, 0.03f, 1.5f), _materials["BenchTop"], true);
        foreach (float x in new[] { -0.36f, 0.36f })
        foreach (float z in new[] { -0.7f, 0.7f })
            Box("Leg", r, new Vector3(x, 0.36f, z), new Vector3(0.05f, 0.72f, 0.05f), _materials["Steel"]);

        // Cartons stand with their length along +x (the trainee sees the size mark on the -x end).
        return new[]
        {
            Empty("Slot_Small", root, new Vector3(15.95f, 0.75f, 14.5f)).transform,
            Empty("Slot_Medium", root, new Vector3(15.95f, 0.75f, 14.87f)).transform,
            Empty("Slot_Large", root, new Vector3(15.95f, 0.75f, 15.42f)).transform,
        };
    }

    private static VoidFillDispenser BuildDispenser(Transform root, VoidFillPiece pillow)
    {
        GameObject go = Empty("VoidFillDispenser", root, new Vector3(15.85f, 0.9f, 12.15f));
        Transform d = go.transform;
        Box("Tray", d, new Vector3(0f, 0.005f, 0f), new Vector3(0.22f, 0.01f, 0.44f), _materials["ToolGrey"]);
        Box("Rim_-X", d, new Vector3(-0.11f, 0.025f, 0f), new Vector3(0.01f, 0.04f, 0.44f), _materials["ToolGrey"]);
        Box("Rim_+X", d, new Vector3(0.11f, 0.025f, 0f), new Vector3(0.01f, 0.04f, 0.44f), _materials["ToolGrey"]);
        Box("SignCard", d, new Vector3(0.12f, 0.1f, 0f), new Vector3(0.005f, 0.1f, 0.3f), _materials["PanelBack"]);
        Text3D("Sign", d, new Vector3(0.116f, 0.1f, 0f), Quaternion.Euler(0f, 90f, 0f), "VOID FILL", 0.3f, new Vector2(0.28f, 0.08f), Color.white);

        var slots = new Object[]
        {
            Empty("Slot_0", d, new Vector3(0f, 0.04f, -0.13f)).transform,
            Empty("Slot_1", d, new Vector3(0f, 0.04f, 0f)).transform,
            Empty("Slot_2", d, new Vector3(0f, 0.04f, 0.13f)).transform,
        };
        VoidFillDispenser dispenser = go.AddComponent<VoidFillDispenser>();
        Set(dispenser, "_piecePrefab", pillow);
        SetArray(dispenser, "_slots", slots);
        return dispenser;
    }

    private static ShippingLabel BuildPrinterAndLabel(Transform root, ShippingLabel labelPrefab)
    {
        GameObject printer = Empty("LabelPrinter", root, new Vector3(16.22f, 0.9f, 12.05f));
        Transform p = printer.transform;
        Box("Body", p, new Vector3(0f, 0.08f, 0f), new Vector3(0.22f, 0.16f, 0.24f), _materials["ToolGrey"], true);
        Box("Screen", p, new Vector3(-0.111f, 0.12f, 0.06f), new Vector3(0.002f, 0.04f, 0.07f), _materials["PanelBack"]);
        Text3D("Name", p, new Vector3(-0.112f, 0.06f, 0f), Quaternion.Euler(0f, 90f, 0f), "LABEL PRINTER", 0.18f, new Vector2(0.2f, 0.04f), Color.white);

        var label = (ShippingLabel)PrefabUtility.InstantiatePrefab(labelPrefab, root);
        label.transform.SetPositionAndRotation(new Vector3(16.2f, 1.07f, 12.05f), Quaternion.Euler(0f, 90f, 0f));
        return label;
    }

    private static ConveyorLoadZone BuildConveyor(Transform root)
    {
        GameObject conveyor = Empty("Conveyor", root, Vector3.zero);
        Transform c = conveyor.transform;
        Box("Belt", c, new Vector3(18.05f, 0.83f, 11.3f), new Vector3(5f, 0.04f, 0.6f), _materials["Belt"], true);
        Box("Rail_-Z", c, new Vector3(18.05f, 0.875f, 10.985f), new Vector3(5f, 0.05f, 0.03f), _materials["Rail"]);
        Box("Rail_+Z", c, new Vector3(18.05f, 0.875f, 11.615f), new Vector3(5f, 0.05f, 0.03f), _materials["Rail"]);
        Box("EndStop", c, new Vector3(20.57f, 0.9f, 11.3f), new Vector3(0.04f, 0.14f, 0.66f), _materials["Rail"], true);
        foreach (float x in new[] { 15.65f, 16.9f, 18.15f, 19.4f, 20.45f })
        foreach (float z in new[] { 11.05f, 11.55f })
            Box("Leg", c, new Vector3(x, 0.405f, z), new Vector3(0.05f, 0.81f, 0.05f), _materials["Steel"]);
        Material guide = _materials["Guide"];
        Box("Zone_Start", c, new Vector3(15.6f, 0.851f, 11.3f), new Vector3(0.012f, 0.002f, 0.6f), guide);
        Box("Zone_End", c, new Vector3(16.6f, 0.851f, 11.3f), new Vector3(0.012f, 0.002f, 0.6f), guide);
        Box("SignPost", c, new Vector3(15.5f, 0.6f, 10.9f), new Vector3(0.04f, 1.2f, 0.04f), _materials["Steel"]);
        Box("SignBoard", c, new Vector3(15.52f, 1.3f, 10.9f), new Vector3(0.02f, 0.2f, 0.42f), _materials["PanelBack"]);
        Text3D("SignText", c, new Vector3(15.508f, 1.3f, 10.9f), Quaternion.Euler(0f, 90f, 0f), "OUTBOUND\nLABEL UP", 0.35f, new Vector2(0.4f, 0.18f), Color.white);

        GameObject zone = Empty("LoadZone", c, new Vector3(16.1f, 1f, 11.3f));
        ConveyorLoadZone loadZone = zone.AddComponent<ConveyorLoadZone>();
        Set(loadZone, "_zone", zone.transform);
        return loadZone;
    }

    private static PackingCarton BuildQcTable(Transform root, PackingCarton mediumPrefab, SeamTool cutterPrefab)
    {
        GameObject table = Empty("QCTable", root, new Vector3(16f, 0f, 10f));
        Transform t = table.transform;
        Box("Top", t, new Vector3(0f, 0.88f, 0f), new Vector3(0.8f, 0.04f, 1.2f), _materials["BenchTop"], true);
        foreach (float x in new[] { -0.36f, 0.36f })
        foreach (float z in new[] { -0.55f, 0.55f })
            Box("Leg", t, new Vector3(x, 0.43f, z), new Vector3(0.05f, 0.86f, 0.05f), _materials["Steel"]);

        var carton = (PackingCarton)PrefabUtility.InstantiatePrefab(mediumPrefab, root);
        carton.name = "QC_Carton_ORD-1874";
        carton.transform.SetPositionAndRotation(new Vector3(15.95f, 0.9f, 10f), Quaternion.Euler(0f, 90f, 0f));
        Set(carton, "_prePacked", true);
        Set(carton, "_cuttable", true);
        Set(carton, "_prePackedOrderId", "ORD-1874");
        Set(carton, "_prePackedFill", 1);
        Set(carton, "_prePackedItems", 1);
        Set(carton.GetComponent<Grabbable>(), "isGrabbable", false);
        Set(carton.GetComponent<Rigidbody>(), "m_IsKinematic", true);

        // The mug set sitting on a too-thin layer of fill.
        Vector3 inner = new Vector3(0.45f, 0.25f, 0.35f);
        Transform contents = carton.transform.Find("Contents");
        float floor = Wall + inner.y * 0.15f * 0.5f;
        Box("MugSet", contents, new Vector3(0f, floor + 0.06f, 0f), new Vector3(0.2f, 0.12f, 0.2f), _materials["ProductRed"]);
        Text3D("MugSetName", contents, new Vector3(0f, floor + 0.121f, 0f), Quaternion.Euler(90f, 0f, 0f), "Ceramic Mug Set - FRAGILE", 0.2f, new Vector2(0.18f, 0.1f), Color.white);
        Transform labelText = carton.transform.Find("LabelVisual/Text");
        if (labelText != null) labelText.GetComponent<TextMeshPro>().text = QcLabel;

        var cutter = (SeamTool)PrefabUtility.InstantiatePrefab(cutterPrefab, root);
        cutter.transform.SetPositionAndRotation(new Vector3(15.72f, 0.93f, 10.45f), Quaternion.Euler(0f, 90f, 0f));
        return carton;
    }

    private static (QcReview, PackingFeedback) BuildQcPanel(Transform canvas)
    {
        var panel = new GameObject(QcPanelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(panel, "Build QC panel");
        panel.layer = 5;
        panel.transform.SetParent(canvas, false);
        var rt = (RectTransform)panel.transform;
        rt.sizeDelta = new Vector2(440f, 300f);
        rt.localScale = Vector3.one * 0.002f;
        rt.SetPositionAndRotation(new Vector3(16.43f, 1.33f, 10f), Quaternion.Euler(0f, 90f, 0f));
        panel.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.11f, 0.94f);

        UiText(panel.transform, "Title", new Vector2(0f, 122f), new Vector2(420f, 40f), "QC REVIEW - ORD-1874", 26f);
        UiText(panel.transform, "Order", new Vector2(0f, 68f), new Vector2(420f, 70f), QcOrderText, 15f);
        Button labelButton = UiButton(panel.transform, "Button_Label", new Vector2(-140f, 0f), "LABEL");
        Button sealButton = UiButton(panel.transform, "Button_Seal", new Vector2(0f, 0f), "SEAL");
        Button fillButton = UiButton(panel.transform, "Button_VoidFill", new Vector2(140f, 0f), "VOID FILL");
        TextMeshProUGUI result = UiText(panel.transform, "Result", new Vector2(0f, -72f), new Vector2(420f, 70f), "", 15f);
        TextMeshProUGUI feedbackText = UiText(panel.transform, "Feedback", new Vector2(0f, -125f), new Vector2(420f, 40f), "", 14f);

        PackingFeedback feedback = feedbackText.gameObject.AddComponent<PackingFeedback>();
        Set(feedback, "_text", feedbackText);
        QcReview review = panel.AddComponent<QcReview>();
        Set(review, "_labelButton", labelButton);
        Set(review, "_sealButton", sealButton);
        Set(review, "_voidFillButton", fillButton);
        Set(review, "_resultText", result);
        return (review, feedback);
    }

    private static GazeDwellTarget BuildSign(Transform root, string name, string text, Vector3 position, MultiCounterGate gate)
    {
        GameObject sign = Empty(name, root, position);
        GameObject board = Box("Board", sign.transform, Vector3.zero, new Vector3(0.02f, 0.2f, 0.62f), _materials["SignBoard"]);
        Quaternion facing = Quaternion.Euler(0f, 90f, 0f);
        Text3D("Title", sign.transform, new Vector3(-0.012f, 0.025f, 0f), facing, text, 0.55f, new Vector2(0.6f, 0.11f), Color.white);
        TextMeshPro found = Text3D("Found", sign.transform, new Vector3(-0.012f, -0.065f, 0f), facing, "FOUND", 0.3f, new Vector2(0.6f, 0.06f), new Color(0.75f, 1f, 0.75f));
        GazeDwellTarget target = sign.AddComponent<GazeDwellTarget>();
        Set(target, "_gate", gate);
        Set(target, "_board", board.GetComponent<Renderer>());
        Set(target, "_foundMark", found.gameObject);
        return target;
    }

    private static void UpdateM2AssessmentData()
    {
        var data = AssetDatabase.LoadAssetAtPath<TutorialData>("Assets/SO/TutorialData/M2_10_Assessment.asset");
        var so = new SerializedObject(data);
        so.FindProperty("<HasChallenges>k__BackingField").boolValue = true;
        so.FindProperty("<IsInOrder>k__BackingField").boolValue = true;
        SerializedProperty list = so.FindProperty("TutorialChallengeLists");
        list.arraySize = 1;
        SerializedProperty entry = list.GetArrayElementAtIndex(0);
        entry.FindPropertyRelative("<ChallengeTitle>k__BackingField").stringValue = "Complete the Packing and Dispatch Assessment";
        entry.FindPropertyRelative("<ChallengeTriggerName>k__BackingField").stringValue = "TRIGGER_M2_AssessmentComplete";
        entry.FindPropertyRelative("<IsComplete>k__BackingField").boolValue = false;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// A plain modal (no challenge checklist) is replaced by a copy of a challenge modal pointed at the same data,
    /// and swapped into the training's flow list.
    /// </summary>
    private static TutorialModalUI EnsureChallengeModal(Dictionary<string, TutorialModalUI> panels, string name, string templateName,
        TutorialManager tutorialManager, TrainingID training)
    {
        TutorialModalUI modal = panels[name];
        if (Get<Transform>(modal, "_challengeHolder") != null) return modal;

        TutorialModalUI template = panels[templateName];
        GameObject copy = Object.Instantiate(template.gameObject, modal.transform.parent);
        Undo.RegisterCreatedObjectUndo(copy, $"Replace {name}");
        copy.name = name;
        copy.SetActive(modal.gameObject.activeSelf);
        copy.transform.SetSiblingIndex(modal.transform.GetSiblingIndex());
        copy.transform.SetPositionAndRotation(modal.transform.position, modal.transform.rotation);
        RemoveChildren(copy.transform, "Trigger_");
        TutorialModalUI replacement = copy.GetComponent<TutorialModalUI>();
        Set(replacement, "_tutorialData", Get<TutorialData>(modal, "_tutorialData"));
        ReplaceInFlow(tutorialManager, training, modal, replacement);
        Undo.DestroyObjectImmediate(modal.gameObject);
        Debug.Log($"[PackingBuilder] {name} was a plain modal; replaced with a challenge modal copied from {templateName}.");
        return replacement;
    }

    private static GameObject EnsurePanelGroup(Transform canvas)
    {
        Transform existing = canvas.Find(PanelGroupName);
        if (existing != null) return existing.gameObject;
        var group = new GameObject(PanelGroupName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(group, "Create M2 panel group");
        group.layer = 5;
        group.transform.SetParent(canvas, false);
        return group;
    }

    // =====================================================================================================
    // 3. Restore M1_09
    // =====================================================================================================

    [MenuItem("Tools/Packing Dispatch/3. Restore M1_09 Assessment")]
    public static void RestoreM1Assessment()
    {
        Dictionary<string, TutorialModalUI> panels = ScenePanels();
        if (panels.ContainsKey("M1_09_Assessment"))
        {
            Debug.Log("[PackingBuilder] M1_09_Assessment already exists - nothing to restore.");
            return;
        }

        if (!panels.TryGetValue("M1_08_Path_Efficiency_Review", out TutorialModalUI template) ||
            !panels.TryGetValue("M1_10_Module_Complete", out TutorialModalUI last))
        {
            Debug.LogError("[PackingBuilder] M1_08 or M1_10 panel not found.");
            return;
        }

        TutorialManager tutorialManager = Object.FindFirstObjectByType<TutorialManager>(FindObjectsInactive.Include);
        AssessmentManager assessmentManager = Object.FindFirstObjectByType<AssessmentManager>(FindObjectsInactive.Include);
        AssessmentUI quizUi = Object.FindFirstObjectByType<AssessmentUI>(FindObjectsInactive.Include);
        AssessmentChallengeGate gate = Object.FindObjectsByType<AssessmentChallengeGate>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(g => new SerializedObject(g).FindProperty("_trainingId").intValue == (int)TrainingID.OrderPicking);
        var data = AssetDatabase.LoadAssetAtPath<TutorialData>("Assets/SO/TutorialData/M1_09_Assessment.asset");
        if (tutorialManager == null || assessmentManager == null || quizUi == null || gate == null || data == null)
        {
            Debug.LogError("[PackingBuilder] TutorialManager, AssessmentManager, AssessmentUI, the OrderPicking AssessmentChallengeGate or M1_09 data missing.");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Restore M1_09 assessment");
        int undoGroup = Undo.GetCurrentGroup();

        GameObject copy = Object.Instantiate(template.gameObject, template.transform.parent);
        Undo.RegisterCreatedObjectUndo(copy, "Restore M1_09");
        copy.name = "M1_09_Assessment";
        copy.SetActive(template.gameObject.activeSelf);
        copy.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1);
        copy.transform.SetPositionAndRotation(template.transform.position, template.transform.rotation);
        foreach (RouteReviewPresenter presenter in copy.GetComponents<RouteReviewPresenter>()) Object.DestroyImmediate(presenter);
        RemoveChildren(copy.transform, "Trigger_");

        TutorialModalUI modal = copy.GetComponent<TutorialModalUI>();
        Set(modal, "_tutorialData", data);
        Set(modal, "_hasChallenges", true);
        Set(modal, "_enableAutoNext", false);
        ChallengeTutorialTrigger trigger = CreateTrigger(copy.transform, "TRIGGER_M1_AssessmentComplete");
        SetArray(modal, "_challengeTutorialTriggerList", new List<Object> { trigger });

        GameObject container = Get<GameObject>(modal, "_container");
        GameObject anchor = Empty("QuizAnchor_M1", template.transform.parent, Vector3.zero);
        Undo.RegisterCreatedObjectUndo(anchor, "Restore M1_09");
        PlaceQuizAnchor(anchor.transform, quizUi.transform, quizUi.transform.Find("Container"), container != null ? container.transform : copy.transform);

        AssessmentLauncher launcher = Undo.AddComponent<AssessmentLauncher>(copy);
        Set(launcher, "_assessmentManager", assessmentManager);
        Set(launcher, "_launchOnce", true);
        Set(launcher, "_hideDuringQuiz", container);
        Set(launcher, "_quizRoot", quizUi.transform);
        Set(launcher, "_quizAnchor", anchor.transform);

        Set(gate, "_challengeTrigger", trigger);
        Set(gate, "_showOnPass", container);

        InsertInFlow(tutorialManager, TrainingID.OrderPicking, modal, last);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[PackingBuilder] M1_09_Assessment restored (copy of M1_08 at the A-aisle spot): trigger TRIGGER_M1_AssessmentComplete, " +
                  "AssessmentLauncher (quiz moved to the A aisle while it runs), OrderPicking gate re-wired, inserted before M1_10. Save the scene.");
    }

    // =====================================================================================================
    // Helpers
    // =====================================================================================================

    private static void LoadMaterials()
    {
        _materials = new Dictionary<string, Material>
        {
            ["Cardboard"] = Mat("M2_Cardboard", new Color(0.72f, 0.55f, 0.36f)),
            ["CardboardDark"] = Mat("M2_CardboardDark", new Color(0.62f, 0.46f, 0.29f)),
            ["CardboardSeam"] = Mat("M2_CardboardSeam", new Color(0.35f, 0.26f, 0.16f)),
            ["Tape"] = Mat("M2_Tape", new Color(0.62f, 0.47f, 0.27f), 0.75f),
            ["Guide"] = Mat("M2_Guide", new Color(1f, 0.82f, 0.1f)),
            ["Pillow"] = Mat("M2_AirPillow", new Color(0.93f, 0.95f, 0.98f), 0.85f),
            ["LabelPaper"] = Mat("M2_LabelPaper", new Color(0.97f, 0.97f, 0.95f)),
            ["Ink"] = Mat("M2_Ink", Ink),
            ["BenchTop"] = Mat("M2_BenchTop", new Color(0.45f, 0.34f, 0.24f)),
            ["Steel"] = Mat("M2_Steel", new Color(0.56f, 0.58f, 0.62f), 0.5f, 0.6f),
            ["PanelBack"] = Mat("M2_PanelBack", new Color(0.08f, 0.09f, 0.11f)),
            ["SignBoard"] = Mat("M2_SignBoard", new Color(0.1f, 0.2f, 0.38f)),
            ["Belt"] = Mat("M2_Belt", new Color(0.12f, 0.12f, 0.13f), 0.2f),
            ["Rail"] = Mat("M2_Rail", new Color(0.95f, 0.75f, 0.1f), 0.4f),
            ["ToolRed"] = Mat("M2_ToolRed", new Color(0.8f, 0.15f, 0.1f), 0.5f),
            ["ToolGrey"] = Mat("M2_ToolGrey", new Color(0.3f, 0.32f, 0.35f), 0.4f),
            ["CutterYellow"] = Mat("M2_CutterYellow", new Color(0.98f, 0.8f, 0.1f), 0.5f),
            ["ProductBlue"] = Mat("M2_ProductBlue", new Color(0.15f, 0.35f, 0.75f), 0.4f),
            ["ProductOrange"] = Mat("M2_ProductOrange", new Color(0.9f, 0.45f, 0.1f), 0.4f),
            ["ProductPurple"] = Mat("M2_ProductPurple", new Color(0.45f, 0.25f, 0.6f), 0.4f),
            ["ProductGreen"] = Mat("M2_ProductGreen", new Color(0.2f, 0.6f, 0.3f), 0.4f),
            ["ProductRed"] = Mat("M2_ProductRed", new Color(0.7f, 0.15f, 0.2f), 0.4f),
        };
    }

    private static Material Mat(string name, Color color, float smoothness = 0.25f, float metallic = 0f)
    {
        EnsureFolder(MaterialFolder);
        string path = $"{MaterialFolder}/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", metallic);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static T LoadPrefab<T>(string name) where T : Component
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/{name}.prefab");
        return go != null ? go.GetComponent<T>() : null;
    }

    private static GameObject FindRoot(string name) =>
        SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == name);

    private static Dictionary<string, TutorialModalUI> ScenePanels() =>
        Resources.FindObjectsOfTypeAll<TutorialModalUI>()
            .Where(p => p.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(p))
            .GroupBy(p => p.name)
            .ToDictionary(g => g.Key, g => g.First());

    private static GameObject Empty(string name, Transform parent, Vector3 localPosition, Quaternion? localRotation = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = localRotation ?? Quaternion.identity;
        return go;
    }

    private static GameObject Box(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material,
        bool keepCollider = false, Quaternion? localRotation = null) =>
        Prim(PrimitiveType.Cube, name, parent, localPosition, size, material, keepCollider, localRotation);

    private static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 scale,
        Material material, bool keepCollider = false, Quaternion? localRotation = null)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = localRotation ?? Quaternion.identity;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    /// <summary>World-space TextMeshPro: readable when looking along the object's +Z.</summary>
    private static TextMeshPro Text3D(string name, Transform parent, Vector3 localPosition, Quaternion localRotation, string text,
        float fontSize, Vector2 size, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        go.transform.localPosition = localPosition;
        go.transform.localRotation = localRotation;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMax = fontSize;
        tmp.fontSizeMin = fontSize * 0.4f;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.rectTransform.sizeDelta = size;
        return tmp;
    }

    private static TextMeshProUGUI UiText(Transform parent, string name, Vector2 position, Vector2 size, string text, float fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Button UiButton(Transform parent, string name, Vector2 position, string text)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = position;
        rt.sizeDelta = new Vector2(124f, 48f);
        var image = go.GetComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        image.color = new Color(0.18f, 0.42f, 0.82f);
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        UiText(go.transform, "Text", Vector2.zero, new Vector2(124f, 48f), text, 20f);
        return button;
    }

    private static Grabbable AddGrabbable(GameObject go, float mass, bool singleHand)
    {
        var body = go.AddComponent<Rigidbody>();
        body.mass = mass;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        var grab = go.AddComponent<Grabbable>();
        grab.singleHandOnly = singleHand;
        grab.heldNoFriction = true;
        return grab;
    }

    private static ChallengeTutorialTrigger CreateTrigger(Transform panel, string triggerName)
    {
        var go = new GameObject("Trigger_" + triggerName.Substring("TRIGGER_".Length));
        Undo.RegisterCreatedObjectUndo(go, "Create challenge trigger");
        go.transform.SetParent(panel, false);
        var trigger = go.AddComponent<ChallengeTutorialTrigger>();
        Set(trigger, "_triggerName", triggerName);
        Set(trigger, "_tutorialType", (int)TutorialType.Trigger);
        return trigger;
    }

    private static void RemoveChildren(Transform parent, string prefix)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child.name.StartsWith(prefix)) Undo.DestroyObjectImmediate(child.gameObject);
        }
    }

    /// <summary>Poses the quiz root so its question container lands exactly on <paramref name="target"/>.</summary>
    private static void PlaceQuizAnchor(Transform anchor, Transform quizRoot, Transform quizContainer, Transform target)
    {
        if (quizContainer == null) quizContainer = quizRoot;
        Quaternion inverseRoot = Quaternion.Inverse(quizRoot.rotation);
        Vector3 offset = inverseRoot * (quizContainer.position - quizRoot.position);
        Quaternion relative = inverseRoot * quizContainer.rotation;
        Quaternion rootRotation = target.rotation * Quaternion.Inverse(relative);
        anchor.SetPositionAndRotation(target.position - rootRotation * offset, rootRotation);
    }

    private static void ReplaceInFlow(TutorialManager manager, TrainingID training, Object oldModal, Object newModal)
    {
        var so = new SerializedObject(manager);
        SerializedProperty list = FlowList(so, training);
        for (int i = 0; list != null && i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == oldModal)
                list.GetArrayElementAtIndex(i).objectReferenceValue = newModal;
        so.ApplyModifiedProperties();
    }

    private static void InsertInFlow(TutorialManager manager, TrainingID training, Object modal, Object before)
    {
        var so = new SerializedObject(manager);
        SerializedProperty list = FlowList(so, training);
        if (list == null) return;
        int index = list.arraySize;
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == before) index = i;
        list.InsertArrayElementAtIndex(index);
        list.GetArrayElementAtIndex(index).objectReferenceValue = modal;
        so.ApplyModifiedProperties();
    }

    private static SerializedProperty FlowList(SerializedObject tutorialManager, TrainingID training)
    {
        SerializedProperty flows = tutorialManager.FindProperty("_tutorialFlowList");
        for (int i = 0; i < flows.arraySize; i++)
        {
            SerializedProperty flow = flows.GetArrayElementAtIndex(i);
            if (flow.FindPropertyRelative("ID").intValue == (int)training) return flow.FindPropertyRelative("List");
        }

        Debug.LogError($"[PackingBuilder] TutorialManager has no flow list for {training}.");
        return null;
    }

    private static void SetOrders(PackingStation station, (string Id, string Panel, Vector3 Size, Object[] Items)[] orders)
    {
        var so = new SerializedObject(station);
        SerializedProperty list = so.FindProperty("_orders");
        list.arraySize = orders.Length;
        for (int i = 0; i < orders.Length; i++)
        {
            SerializedProperty order = list.GetArrayElementAtIndex(i);
            order.FindPropertyRelative("Id").stringValue = orders[i].Id;
            order.FindPropertyRelative("PanelText").stringValue = orders[i].Panel;
            order.FindPropertyRelative("PackedSizeCm").vector3Value = orders[i].Size;
            SerializedProperty items = order.FindPropertyRelative("ItemPrefabs");
            items.arraySize = orders[i].Items.Length;
            for (int j = 0; j < orders[i].Items.Length; j++)
                items.GetArrayElementAtIndex(j).objectReferenceValue = orders[i].Items[j];
        }

        so.ApplyModifiedProperties();
    }

    private static void SetSupplies(PackingStation station, (CartonSize Size, PackingCarton Prefab, Transform Slot)[] supplies)
    {
        var so = new SerializedObject(station);
        SerializedProperty list = so.FindProperty("_supplies");
        list.arraySize = supplies.Length;
        for (int i = 0; i < supplies.Length; i++)
        {
            SerializedProperty supply = list.GetArrayElementAtIndex(i);
            supply.FindPropertyRelative("Size").intValue = (int)supplies[i].Size;
            supply.FindPropertyRelative("Prefab").objectReferenceValue = supplies[i].Prefab;
            supply.FindPropertyRelative("Slot").objectReferenceValue = supplies[i].Slot;
        }

        so.ApplyModifiedProperties();
    }

    private static T Get<T>(Object target, string property) where T : Object
    {
        SerializedProperty p = new SerializedObject(target).FindProperty(property);
        return p != null ? p.objectReferenceValue as T : null;
    }

    private static void Set(Object target, string property, object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(property);
        if (p == null)
        {
            Debug.LogError($"[PackingBuilder] {target.GetType().Name} has no serialized field '{property}'.");
            return;
        }

        switch (value)
        {
            case null: p.objectReferenceValue = null; break;
            case Object o: p.objectReferenceValue = o; break;
            case string s: p.stringValue = s; break;
            case bool b: p.boolValue = b; break;
            case int i: p.intValue = i; break;
            case float f: p.floatValue = f; break;
            case Vector3 v: p.vector3Value = v; break;
            default:
                Debug.LogError($"[PackingBuilder] Unsupported value type {value.GetType().Name} for '{property}'.");
                return;
        }

        so.ApplyModifiedProperties();
    }

    private static void SetArray(Object target, string property, IList<Object> values)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(property);
        if (p == null)
        {
            Debug.LogError($"[PackingBuilder] {target.GetType().Name} has no serialized field '{property}'.");
            return;
        }

        p.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedProperties();
    }
}
```

- [ ] **Step 2: Compile** — `refresh_unity` (if_dirty, compile request) + `read_console` errors → none.

- [ ] **Step 3: Run `Tools/Packing Dispatch/1. Build Prefabs`** (`execute_menu_item`), then `read_console`.
Expected log: `[PackingBuilder] Prefabs built in Assets/Prefabs/PackingDispatch: 3 cartons, 4 products, ...`; no errors.
Verify: `Assets/Prefabs/PackingDispatch/` lists 11 prefabs + `Materials/` (22 materials).

- [ ] **Step 4: Checkpoint** — no commit.

---

### Task 4: Build the station, wire M2, restore M1_09

**Files:**
- Modify (via builder): `Assets/Scenes/BasicScene.unity`, `Assets/SO/TutorialData/M2_10_Assessment.asset`

- [ ] **Step 1: Run `Tools/Packing Dispatch/2. Build Station + Wire M2`**, then `read_console`.
Expected: `[PackingBuilder] Station built: … 14 M2 triggers wired …` (13 from M2_02–M2_09 + 1 for M2_10) and possibly
`M2_10_Assessment was a plain modal; replaced …`. No `[PackingBuilder]` errors.

- [ ] **Step 2: Run `Tools/Packing Dispatch/3. Restore M1_09 Assessment`**, then `read_console`.
Expected: `[PackingBuilder] M1_09_Assessment restored …`.

- [ ] **Step 3: Set the auto-start module for M2 testing**
`manage_components set_property` on the TutorialManager object (`-----Manager-----/TutorialManager`), component
`TutorialManager`, property `_autoStartTraining`, value `3` (PackingAndDispatch).

- [ ] **Step 4: Verify the wiring by reading resources**
- `find_gameobjects` `M2_PackingStation` → read `Bench` `PackingStation` component: 2 orders, 3 supplies, 6 triggers non-null.
- `M2_10_Assessment` `TutorialModalUI`: `_hasChallenges` true, `_challengeHolder` set, 1 trigger; has `AssessmentLauncher`.
- `TutorialManager` flow lists: OrderPicking has 10 entries with `M1_09_Assessment` before `M1_10`; PackingAndDispatch has 11.
- `M2_10_Assessment.asset`: `HasChallenges: 1`, one challenge `TRIGGER_M2_AssessmentComplete`.

- [ ] **Step 5: Screenshots** (`manage_camera screenshot`, `include_image`):
  1. Trainee view at the bench: `view_position [15.0, 1.6, 13.0]`, `view_rotation [15, 90, 0]`.
  2. Overview: `view_position [13.2, 2.6, 13.0]`, `view_rotation [25, 90, 0]`.
  3. QC table: `view_position [15.0, 1.6, 10.0]`, `view_rotation [15, 90, 0]`.
  4. Modal from the bench: `view_position [15.2, 1.6, 13.0]`, `view_rotation [5, 20, 0]`.
  Check: bench, rack with three cartons, dispenser, printer + label, scanner, tape gun, conveyor, QC table, signs, order
  panel text, modal readable and not blocking the bench. Adjust builder constants and re-run step 1 if anything
  overlaps (Build Station is re-runnable).

- [ ] **Step 6: Save** — `manage_scene save`. Checkpoint, no commit.

---

### Task 5: Play-mode walkthrough

**Files:**
- Create: `Assets/Editor/PackingDispatchPlaytestTemp.cs` (deleted in Task 6)

**Interfaces:**
- Consumes: Task 2 public APIs; framework: `TutorialModalUI.OnTriggerNextTutorial()`, private `_container`, `_tutorialChallengeList`;
  `AssessmentManager.SubmitAnswer`, private `_currentAssessmentDataList`, `_assessmentIndex`; `AssessmentUI.OnCloseButtonClicked`;
  `AssessmentFailPanel.OnRetry`; `TutorialManager.StartTraining`, `OpenNextTutorialModal`.

- [ ] **Step 1: Write the driver**

<!-- write: Assets/Editor/PackingDispatchPlaytestTemp.cs -->
```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using ImmersiveTraining.TrainingInteractions;
using UnityEditor;
using UnityEngine;
using Valari.Data;
using Valari.Managers;
using Valari.UI;
using Object = UnityEngine.Object;

// TEMPORARY Play-mode walkthrough of Module 2 and a smoke test of the restored M1_09. Delete after verification.
// Enter Play mode (auto-start = PackingAndDispatch), run Tools/Temp/M2 Playtest; results go to Temp/m2_playtest.txt.
public static class PackingDispatchPlaytestTemp
{
    private const string LogPath = "Temp/m2_playtest.txt";
    private static readonly Stack<IEnumerator> Routines = new Stack<IEnumerator>();
    private static double _resumeAt;
    private static StringBuilder _log;
    private static int _passes, _failures;

    [MenuItem("Tools/Temp/M2 Playtest")]
    public static void RunM2() => Begin(M2Flow());

    [MenuItem("Tools/Temp/M1_09 Smoke Test")]
    public static void RunM1() => Begin(M1Flow());

    private static void Begin(IEnumerator flow)
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError("[M2Playtest] Enter Play mode first.");
            return;
        }

        Routines.Clear();
        Routines.Push(flow);
        _log = new StringBuilder();
        _passes = _failures = 0;
        _resumeAt = 0;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Log("started");
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying)
        {
            Finish("left Play mode");
            return;
        }

        if (EditorApplication.timeSinceStartup < _resumeAt) return;
        try
        {
            if (Routines.Count == 0)
            {
                Finish("done");
                return;
            }

            IEnumerator top = Routines.Peek();
            if (!top.MoveNext())
            {
                Routines.Pop();
                return;
            }

            if (top.Current is IEnumerator nested) Routines.Push(nested);
            else if (top.Current is float seconds) _resumeAt = EditorApplication.timeSinceStartup + seconds;
        }
        catch (Exception e)
        {
            Fail("exception: " + e);
            Finish("aborted");
        }
    }

    private static void Finish(string why)
    {
        EditorApplication.update -= Tick;
        Routines.Clear();
        Log($"FINISHED ({why}): {_passes} passed, {_failures} failed");
        System.IO.File.WriteAllText(LogPath, _log.ToString());
        Debug.Log($"[M2Playtest] {why}: {_passes} passed, {_failures} failed - see {LogPath}");
    }

    private static void Log(string message)
    {
        _log?.AppendLine($"[{Time.time,7:0.0}] {message}");
        Debug.Log("[M2Playtest] " + message);
    }

    private static void Pass(string what)
    {
        _passes++;
        Log("PASS " + what);
    }

    private static void Fail(string what)
    {
        _failures++;
        Log("FAIL " + what);
    }

    private static void Check(bool ok, string what)
    {
        if (ok) Pass(what);
        else Fail(what);
    }

    private static IEnumerator WaitFor(Func<bool> condition, float timeout, string what)
    {
        double end = EditorApplication.timeSinceStartup + timeout;
        while (!Safe(condition))
        {
            if (EditorApplication.timeSinceStartup > end)
            {
                Fail(what + " (timed out)");
                yield break;
            }

            yield return 0.05f;
        }

        Pass(what);
    }

    private static bool Safe(Func<bool> condition)
    {
        try
        {
            return condition();
        }
        catch
        {
            return false;
        }
    }

    // ---------- framework access ----------

    private static T Field<T>(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        return field != null ? (T)field.GetValue(target) : default;
    }

    private static TutorialModalUI Panel(string name) =>
        Resources.FindObjectsOfTypeAll<TutorialModalUI>().FirstOrDefault(p => p.gameObject.scene.IsValid() && p.name == name);

    private static ChallengeTutorialTrigger Trigger(string name) =>
        Resources.FindObjectsOfTypeAll<ChallengeTutorialTrigger>().FirstOrDefault(t => t.gameObject.scene.IsValid() && t.TriggerName == name);

    private static bool PanelShown(string name)
    {
        TutorialModalUI panel = Panel(name);
        var container = Field<GameObject>(panel, "_container");
        return panel != null && panel.gameObject.activeInHierarchy && container != null && container.activeInHierarchy;
    }

    private static bool ChallengeDone(string panel, string trigger)
    {
        var list = Field<List<TutorialChallengeData>>(Panel(panel), "_tutorialChallengeList");
        TutorialChallengeData challenge = list?.FirstOrDefault(c => c.ChallengeTriggerName == trigger);
        return challenge != null && challenge.IsComplete;
    }

    private static IEnumerator Next(string panel, string nextPanel)
    {
        Panel(panel).OnTriggerNextTutorial();
        if (nextPanel != null) yield return WaitFor(() => PanelShown(nextPanel), 15f, $"{nextPanel} shown");
        else yield return 1f;
    }

    private static IEnumerator Armed(string trigger) =>
        WaitFor(() => Trigger(trigger) != null && Trigger(trigger).IsArmed, 10f, $"{trigger} armed");

    private static IEnumerator Done(string panel, string trigger) =>
        WaitFor(() => ChallengeDone(panel, trigger), 5f, $"{trigger} complete");

    // ---------- scene helpers ----------

    private static void Place(Component component, Vector3 position, Quaternion rotation)
    {
        component.transform.SetPositionAndRotation(position, rotation);
        Rigidbody body = component.GetComponent<Rigidbody>();
        if (body == null) return;
        body.position = position;
        body.rotation = rotation;
        if (body.isKinematic) return;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
    }

    private static IEnumerator DropPillows(VoidFillDispenser dispenser, PackingCarton carton, int count)
    {
        for (int i = 0; i < count; i++)
        {
            VoidFillPiece piece = null;
            double end = EditorApplication.timeSinceStartup + 5;
            while (piece == null && EditorApplication.timeSinceStartup < end)
            {
                for (int s = 0; s < dispenser.SlotCount && piece == null; s++) piece = dispenser.PieceInSlot(s);
                if (piece == null) yield return 0.1f;
            }

            if (piece == null)
            {
                Fail("no pillow available at the dispenser");
                yield break;
            }

            Place(piece, carton.DropPoint, Quaternion.identity);
            yield return 0.3f;
        }
    }

    private static IEnumerator Sweep(SeamTool tool, PackingCarton carton, int seam)
    {
        Rigidbody body = tool.GetComponent<Rigidbody>();
        bool wasKinematic = body != null && body.isKinematic;
        if (body != null) body.isKinematic = true;
        tool.ForceActive = true;
        carton.SeamWorldEnds(seam, out Vector3 a, out Vector3 b);
        const int steps = 30;
        for (int i = 0; i <= steps; i++)
        {
            Vector3 target = Vector3.Lerp(a, b, i / (float)steps) + Vector3.up * 0.01f;
            tool.transform.position += target - tool.HeadPosition;
            yield return 0.02f;
        }

        tool.ForceActive = false;
        if (body != null) body.isKinematic = wasKinematic;
    }

    private static void AimScanner(ScannerTool scanner, Vector3 target)
    {
        Transform muzzle = Field<Transform>(scanner, "_muzzle");
        if (muzzle == null) muzzle = scanner.transform;
        Rigidbody body = scanner.GetComponent<Rigidbody>();
        if (body != null) body.isKinematic = true;
        Transform t = scanner.transform;
        t.rotation = Quaternion.FromToRotation(muzzle.forward, Vector3.down) * t.rotation;
        t.position += target + Vector3.up * 0.3f - muzzle.position;
    }

    private static bool QuizShowing()
    {
        AssessmentUI ui = Object.FindFirstObjectByType<AssessmentUI>(FindObjectsInactive.Include);
        var questions = Field<GameObject>(ui, "_questionContainer");
        return questions != null && questions.activeInHierarchy;
    }

    private static IEnumerator AnswerQuiz(bool correct)
    {
        AssessmentManager manager = Object.FindFirstObjectByType<AssessmentManager>(FindObjectsInactive.Include);
        var questions = Field<List<AssessmentData>>(manager, "_currentAssessmentDataList");
        for (int i = 0; i < questions.Count; i++)
        {
            AssessmentData question = questions[Field<int>(manager, "_assessmentIndex")];
            string choice = correct
                ? question.Choices[question.AnswerIndex]
                : question.Choices[(question.AnswerIndex + 1) % question.Choices.Count];
            manager.SubmitAnswer(choice);
            yield return 0.05f;
        }

        yield return 0.3f;
        Object.FindFirstObjectByType<AssessmentUI>(FindObjectsInactive.Include).OnCloseButtonClicked();
        yield return 0.3f;
    }

    // ---------- M2 ----------

    private static IEnumerator M2Flow()
    {
        PackingStation station = Object.FindFirstObjectByType<PackingStation>();
        TutorialManager manager = Object.FindFirstObjectByType<TutorialManager>();
        Transform quizRoot = Object.FindFirstObjectByType<AssessmentUI>(FindObjectsInactive.Include).transform;
        Vector3 quizHome = quizRoot.position;

        yield return WaitFor(() => station.OrderIndex == 0, 20f, "PackingAndDispatch started (auto-start)");
        if (station.OrderIndex != 0)
        {
            manager.StartTraining(TrainingID.PackingAndDispatch);
            yield return WaitFor(() => station.OrderIndex == 0, 3f, "PackingAndDispatch started manually");
        }

        PackingFeedback feedback = station.Feedback;
        VoidFillDispenser dispenser = Object.FindFirstObjectByType<VoidFillDispenser>();
        SeamTool tapeGun = SeamTool.All.First(t => t.Kind == SeamToolKind.Tape);
        SeamTool cutter = SeamTool.All.First(t => t.Kind == SeamToolKind.Cutter);

        // M2_01
        yield return WaitFor(() => PanelShown("M2_01_Welcome_Packing"), 15f, "M2_01 shown");
        yield return Next("M2_01_Welcome_Packing", "M2_02_Station_Layout");

        // M2_02 - gaze at the three signs
        yield return Armed("TRIGGER_M2_StationLayoutConfirmed");
        GazeDwellTarget[] signs = Object.FindObjectsByType<GazeDwellTarget>(FindObjectsSortMode.None);
        Check(signs.Length == 3, "three zone signs");
        var head = new GameObject("PlaytestHead").transform;
        foreach (GazeDwellTarget sign in signs) sign.HeadOverride = head;
        head.position = signs[0].transform.position + new Vector3(-2.5f, -0.3f, 0f);
        head.rotation = Quaternion.LookRotation(Vector3.left);
        yield return 1f;
        Check(!signs[0].IsFound, "looking away does not find a sign");
        foreach (GazeDwellTarget sign in signs)
        {
            head.position = sign.transform.position + new Vector3(-2.5f, -0.3f, 0f);
            head.LookAt(sign.transform.position);
            yield return WaitFor(() => sign.IsFound, 3f, $"{sign.name} found by gaze");
        }

        yield return Done("M2_02_Station_Layout", "TRIGGER_M2_StationLayoutConfirmed");
        yield return WaitFor(() => PanelShown("M2_03_Box_Selection"), 10f, "M2_02 auto-advanced to M2_03");
        foreach (GazeDwellTarget sign in signs) sign.HeadOverride = null;
        Object.Destroy(head.gameObject);

        // M2_03 - box selection
        yield return Armed("TRIGGER_M2_CorrectBoxSelected");
        PackingCarton small = station.SupplyCarton(CartonSize.Small);
        PackingCarton large = station.SupplyCarton(CartonSize.Large);
        PackingCarton medium = station.SupplyCarton(CartonSize.Medium);
        Vector3 onSpot = station.Spot.position + Vector3.up * 0.03f;
        Place(small, onSpot, station.Spot.rotation);
        yield return WaitFor(() => feedback.LastMessage.Contains("too small"), 3f, "Small carton refused as too small");
        yield return WaitFor(() => (small.transform.position - station.SupplySlot(CartonSize.Small).position).magnitude < 0.15f, 4f, "refused Small returned to the rack");
        Place(large, onSpot, station.Spot.rotation);
        yield return WaitFor(() => feedback.LastMessage.Contains("too big"), 3f, "Large carton refused as oversized");
        yield return 2f;
        Check(!ChallengeDone("M2_03_Box_Selection", "TRIGGER_M2_CorrectBoxSelected"), "wrong cartons do not complete M2_03");
        Place(medium, onSpot, station.Spot.rotation);
        yield return WaitFor(() => station.ActiveCarton == medium && medium.IsLocked, 3f, "Medium locked on the packing spot");
        yield return WaitFor(() => station.SupplyCarton(CartonSize.Medium) != null && station.SupplyCarton(CartonSize.Medium) != medium, 2f, "rack restocked with a new Medium");
        yield return Done("M2_03_Box_Selection", "TRIGGER_M2_CorrectBoxSelected");
        yield return Next("M2_03_Box_Selection", "M2_04_Void_Fill_Application");

        // M2_04 - void fill
        yield return Armed("TRIGGER_M2_VoidFillBase");
        PackingCarton box = medium;
        PackableItem speaker = station.LooseOrderItems().FirstOrDefault();
        Check(speaker != null, "order 1 product on the bench");
        Place(speaker, box.DropPoint, Quaternion.identity);
        yield return WaitFor(() => feedback.LastMessage.Contains("base of the box first"), 3f, "product refused before the base fill");
        Check(!speaker.IsPacked, "product not packed before the base fill");
        yield return DropPillows(dispenser, box, 2);
        yield return Done("M2_04_Void_Fill_Application", "TRIGGER_M2_VoidFillBase");
        yield return DropPillows(dispenser, box, 1);
        yield return WaitFor(() => feedback.LastMessage.Contains("Place the item"), 2f, "extra pillow refused until the product is in");
        Check(box.Fill.Fill == 2, "refused pillow not counted");
        Place(speaker, box.DropPoint, Quaternion.identity);
        yield return WaitFor(() => speaker.IsPacked && box.ItemCount == 1, 3f, "product packed on the base layer");
        yield return DropPillows(dispenser, box, 3);
        yield return WaitFor(() => box.IsFull && box.IsClosed, 3f, "box full and flaps closed");
        yield return Done("M2_04_Void_Fill_Application", "TRIGGER_M2_VoidFillTop");
        yield return Next("M2_04_Void_Fill_Application", "M2_05_Sealing_the_Box");

        // M2_05 - H-tape
        yield return Armed("TRIGGER_M2_BoxSealed");
        Vector3 tapeHome = tapeGun.transform.position;
        Quaternion tapeHomeRotation = tapeGun.transform.rotation;
        yield return Sweep(tapeGun, box, PackingCarton.SeamCentre);
        yield return 0.3f;
        Check(box.IsTaped(PackingCarton.SeamCentre) && !box.IsSealed, "centre strip alone does not seal");
        Check(!ChallengeDone("M2_05_Sealing_the_Box", "TRIGGER_M2_BoxSealed"), "M2_05 not complete with one strip");
        yield return Sweep(tapeGun, box, PackingCarton.SeamEndA);
        yield return Sweep(tapeGun, box, PackingCarton.SeamEndB);
        yield return WaitFor(() => box.IsSealed, 2f, "H-tape seals the box");
        yield return Done("M2_05_Sealing_the_Box", "TRIGGER_M2_BoxSealed");
        Place(tapeGun, tapeHome, tapeHomeRotation);
        yield return Next("M2_05_Sealing_the_Box", "M2_06_Label_Verification");

        // M2_06 - label
        yield return Armed("TRIGGER_M2_LabelVerified");
        ShippingLabel label = Object.FindFirstObjectByType<ShippingLabel>();
        Place(label, box.TopPoint, Quaternion.identity);
        yield return WaitFor(() => feedback.LastMessage.Contains("Scan the label"), 2f, "unscanned label refused");
        Check(!box.IsLabelled, "label not applied before scanning");
        ScannerTool scanner = Object.FindObjectsByType<ScannerTool>(FindObjectsSortMode.None).First(s => s.ExpectedSku == "LBL-ORD-2001");
        yield return 0.3f;
        AimScanner(scanner, label.transform.position);
        scanner.Scan();
        yield return WaitFor(() => label.IsVerified, 2f, "scan verifies the label");
        yield return Done("M2_06_Label_Verification", "TRIGGER_M2_LabelVerified");
        Place(label, box.TopPoint, Quaternion.identity);
        yield return WaitFor(() => box.IsLabelled && label.IsApplied, 3f, "label applied to the top face");
        yield return Done("M2_06_Label_Verification", "TRIGGER_M2_LabelApplied");
        yield return WaitFor(() => !box.IsLocked, 2f, "labelled box released for carrying");
        Rigidbody scannerBody = scanner.GetComponent<Rigidbody>();
        if (scannerBody != null) scannerBody.isKinematic = false;
        yield return Next("M2_06_Label_Verification", "M2_07_Conveyor_Loading");

        // M2_07 - conveyor
        yield return Armed("TRIGGER_M2_BoxOnConveyor");
        ConveyorLoadZone zone = Object.FindFirstObjectByType<ConveyorLoadZone>();
        Place(box, zone.LoadPoint + Vector3.up * (box.InnerSize.y + 0.1f), Quaternion.Euler(180f, 0f, 0f));
        yield return WaitFor(() => feedback.LastMessage.Contains("must face UP"), 5f, "label-down box refused");
        Check(!box.IsOnConveyor, "label-down box not taken");
        Place(box, zone.LoadPoint, Quaternion.identity);
        yield return WaitFor(() => box.IsOnConveyor, 5f, "label-up box taken by the conveyor");
        yield return Done("M2_07_Conveyor_Loading", "TRIGGER_M2_BoxOnConveyor");
        yield return WaitFor(() => station.OrderIndex == 1, 15f, "box dispatched and order 2 started");
        Check(station.LooseOrderItems().Count == 3, "order 2's three products on the bench");
        yield return Next("M2_07_Conveyor_Loading", "M2_08_Multi_Item_Consolidation");

        // M2_08 - multi-item order
        yield return Armed("TRIGGER_M2_MultiItemPacked");
        PackingCarton medium2 = station.SupplyCarton(CartonSize.Medium);
        PackingCarton large2 = station.SupplyCarton(CartonSize.Large);
        Place(medium2, onSpot, station.Spot.rotation);
        yield return WaitFor(() => feedback.LastMessage.Contains("too small"), 3f, "Medium refused for the three-item order");
        yield return 2f;
        Place(large2, onSpot, station.Spot.rotation);
        yield return WaitFor(() => station.ActiveCarton == large2 && large2.IsLocked, 3f, "Large locked for order 2");
        List<PackableItem> products = station.LooseOrderItems();
        Place(products[0], large2.DropPoint, Quaternion.identity);
        yield return WaitFor(() => feedback.LastMessage.Contains("base of the box first"), 3f, "order 2 product refused before base fill");
        yield return DropPillows(dispenser, large2, 2);
        foreach (PackableItem product in products)
        {
            Place(product, large2.DropPoint, Quaternion.identity);
            yield return WaitFor(() => product.IsPacked, 3f, $"{product.DisplayName} packed");
        }

        Check(!ChallengeDone("M2_08_Multi_Item_Consolidation", "TRIGGER_M2_MultiItemPacked"), "M2_08 waits for the top fill");
        yield return DropPillows(dispenser, large2, 3);
        yield return WaitFor(() => large2.IsFull, 3f, "Large carton full");
        yield return Done("M2_08_Multi_Item_Consolidation", "TRIGGER_M2_MultiItemPacked");
        yield return Next("M2_08_Multi_Item_Consolidation", "M2_09_Error_Identification");

        // M2_09 - QC
        yield return Armed("TRIGGER_M2_ErrorIdentified");
        QcReview qc = Object.FindFirstObjectByType<QcReview>();
        PackingCarton qcBox = qc.Carton;
        qc.ButtonFor(QcElement.Seal).onClick.Invoke();
        yield return WaitFor(() => qc.ResultText.Contains("is correct"), 1f, "wrong answer (seal) explained");
        Check(!qc.IsIdentified, "wrong answer does not identify the fault");
        qc.ButtonFor(QcElement.VoidFill).onClick.Invoke();
        yield return WaitFor(() => qc.ResultText.Contains("open the box"), 1f, "void-fill answer needs the box opened first");
        yield return Sweep(cutter, qcBox, PackingCarton.SeamCentre);
        yield return WaitFor(() => qcBox.HasBeenOpened && !qcBox.IsClosed, 2f, "box cutter opens the QC box");
        qc.ButtonFor(QcElement.VoidFill).onClick.Invoke();
        yield return WaitFor(() => qc.IsIdentified, 1f, "void fill identified");
        yield return Done("M2_09_Error_Identification", "TRIGGER_M2_ErrorIdentified");
        yield return DropPillows(dispenser, qcBox, 4);
        yield return WaitFor(() => qcBox.IsFull && qcBox.IsClosed, 3f, "QC box filled to full and closed");
        yield return Sweep(tapeGun, qcBox, PackingCarton.SeamCentre);
        yield return WaitFor(() => qcBox.IsSealed, 2f, "centre seam re-taped");
        yield return Done("M2_09_Error_Identification", "TRIGGER_M2_ErrorCorrected");
        yield return Next("M2_09_Error_Identification", "M2_10_Assessment");

        // M2_10 - assessment
        Transform anchor = GameObject.Find("M2_PackingStation/QuizAnchor_M2").transform;
        yield return WaitFor(() => (quizRoot.position - anchor.position).magnitude < 0.01f, 3f, "quiz moved to the packing station");
        yield return WaitFor(QuizShowing, 3f, "quiz launched at M2_10");
        yield return Armed("TRIGGER_M2_AssessmentComplete");
        yield return AnswerQuiz(false);
        AssessmentFailPanel failPanel = Object.FindFirstObjectByType<AssessmentFailPanel>(FindObjectsInactive.Include);
        yield return WaitFor(() => failPanel.gameObject.activeInHierarchy, 2f, "failing score shows the fail/retry screen");
        Check(!ChallengeDone("M2_10_Assessment", "TRIGGER_M2_AssessmentComplete"), "failed quiz does not complete M2_10");
        failPanel.OnRetry();
        yield return WaitFor(QuizShowing, 2f, "retake relaunches the quiz");
        yield return AnswerQuiz(true);
        yield return Done("M2_10_Assessment", "TRIGGER_M2_AssessmentComplete");
        yield return WaitFor(() => PanelShown("M2_10_Assessment"), 2f, "M2_10 modal shown again after the pass");
        yield return Next("M2_10_Assessment", "M2_11_Module_Complete");
        yield return WaitFor(() => (quizRoot.position - quizHome).magnitude < 0.01f, 2f, "quiz moved back to the spawn");

        // M2_11
        yield return Next("M2_11_Module_Complete", null);
        yield return 1f;
        Check(!PanelShown("M2_11_Module_Complete"), "module complete closed");
        Check(!QuizShowing(), "no quiz after the module-complete screen");
    }

    // ---------- M1_09 ----------

    private static IEnumerator M1Flow()
    {
        TutorialManager manager = Object.FindFirstObjectByType<TutorialManager>();
        Transform quizRoot = Object.FindFirstObjectByType<AssessmentUI>(FindObjectsInactive.Include).transform;
        manager.StartTraining(TrainingID.OrderPicking);
        yield return WaitFor(() => PanelShown("M1_01_Welcome_Order_Picking"), 10f, "Order Picking started");
        for (int i = 0; i < 8; i++)
        {
            manager.OpenNextTutorialModal();
            yield return 0.3f;
        }

        yield return WaitFor(() => Panel("M1_09_Assessment") != null && Panel("M1_09_Assessment").gameObject.activeInHierarchy, 5f, "M1_09 is the 9th Order Picking step");
        Transform anchor = GameObject.Find("Canvas/OrderPicking/QuizAnchor_M1").transform;
        yield return WaitFor(() => (quizRoot.position - anchor.position).magnitude < 0.01f, 3f, "M1 quiz moved to the A aisle");
        yield return WaitFor(QuizShowing, 3f, "M1 quiz launched");
        yield return Armed("TRIGGER_M1_AssessmentComplete");
        yield return AnswerQuiz(true);
        yield return Done("M1_09_Assessment", "TRIGGER_M1_AssessmentComplete");
        yield return WaitFor(() => PanelShown("M1_09_Assessment"), 2f, "M1_09 modal shown again after the pass");
    }
}
```

- [ ] **Step 2: Compile** — `refresh_unity` + `read_console` errors → none. Save the scene (`manage_scene save`).

- [ ] **Step 3: Run the walkthrough**
1. `manage_editor` play. Wait ~5 s (resource read `editor_state` until playing).
2. `execute_menu_item` `Tools/Temp/M2 Playtest`. Wait ~100 s, then read `Temp/m2_playtest.txt` (Bash).
Expected: `FINISHED (done): N passed, 0 failed`.
3. `read_console` errors/warnings: no exceptions from the M2 scripts.
4. `execute_menu_item` `Tools/Temp/M1_09 Smoke Test`; wait ~20 s; read `Temp/m2_playtest.txt` → `0 failed`.
5. `manage_editor` stop (save first — leaving Play mode can crash the editor).

- [ ] **Step 4: Fix and repeat** — for any FAIL, use superpowers:systematic-debugging, fix the component or builder
(re-run Build Station if the builder changed; re-save), and repeat Step 3 until 0 failed.

- [ ] **Step 5: Checkpoint** — no commit.

---

### Task 6: Docs and cleanup

**Files:**
- Modify: `CLAUDE.md` (overview, §3 scripts table, new §5b "Wiring Status — Module 2", §6 tracker/in-headset checklist, §8)
- Modify: memory `m1-order-picking-state.md` (M1_09 restored), create `m2-packing-dispatch-state.md`, update `MEMORY.md`
- Delete: `Assets/Editor/PackingDispatchPlaytestTemp.cs(.meta)`, `Assets/Editor/M2SurveyTemp.cs(.meta)`, screenshots taken this session (`Assets/Screenshots/screenshot-20261006-*`, `m2tmp_*`)

- [ ] **Step 1:** Update CLAUDE.md with the M2 wiring chains, station layout, builder usage, auto-start note (`_autoStartTraining` = PackingAndDispatch for M2 testing; WarehouseSafety for product), M1_09 restored, and the in-headset checklist (grab cartons two-handed, pillow drops, tape sweep feel, label press, carrying the box, laser-pressing QC buttons, modal placement).
- [ ] **Step 2:** Delete temp files; `refresh_unity`; `read_console` → no errors; save scene.
- [ ] **Step 3:** Final verification: both `dotnet run` test projects pass; console clean after a final compile.
- [ ] **Step 4:** Report to the user; offer to commit (user decides).

---

## Execution notes (2026-10-06)

- All tasks executed in-session on `main` (live editor). Unit tests 31/31 (mutation-checked: breaking the clearance,
  base-fill gate and bin dedupe fails 7 tests); M1 suite still 45/45.
- Task 4 Step 1 expected "14 M2 triggers" — the correct count is **12** (1+1+2+1+2+1+1+2+1); the builder reports 12.
- Builder tweaks after the first screenshots: the order board shows "PACKING STATION / Waiting for the next order" in
  edit mode, and the sign "FOUND" marks start hidden.
- Play-mode walkthrough: **88 passed, 0 failed** (twice; the second on the final saved scene). M1_09 smoke test on a fresh
  start: 7/7. Run after the M2 module in the same session it stalled — pre-existing trainer issue (M1_01 teleport +
  dynamic trainer Rigidbody), documented in CLAUDE.md §7, not fixed.
- Deviation: the walkthrough driver was **kept** as `Assets/Editor/PackingDispatchPlaytest.cs` (menu Tools ▸ Packing
  Dispatch ▸ Test: …) instead of being deleted — it is the only automated end-to-end check for M2. The survey script and
  session screenshots were deleted.
- Nothing committed (user rule).
