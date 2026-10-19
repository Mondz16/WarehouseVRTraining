using System;
using ImmersiveTraining.TrainingInteractions;

// Console test runner for the Unity-free Order Picking bridge logic (no test framework, no packages).
// Run from the repo root:  dotnet run --project Tests/OrderPickingLogic
internal static class Program
{
    private static int _passed;
    private static int _failed;

    private static int Main()
    {
        Run("Latch: a completion the challenge rejects (step not active yet) does not latch", LatchRejectedOfferDoesNotLatch);
        Run("Latch: an accepted completion latches and completes only once", LatchAcceptedOfferCompletesOnce);
        Run("Latch: an unmet condition never reaches the challenge", LatchUnmetConditionDoesNotComplete);
        Run("Latch: with no challenge wired the first met offer latches", LatchWithoutChallengeLatches);
        Run("Latch: a re-entrant offer during completion does not complete twice", LatchReentrantOfferCompletesOnce);
        Run("Latch: reset allows completing again", LatchResetAllowsCompletingAgain);

        Run("Tote: counts only the required SKU", ToteCountsOnlyRequiredSku);
        Run("Tote: an item entering twice counts once", ToteCountsAnItemOnce);
        Run("Tote: an item leaving is no longer counted", ToteExitRemovesItem);
        Run("Tote: target is met at the required count", ToteTargetMetAtRequiredCount);
        Run("Tote: a decoy blocks an exclusive target until it is removed", ToteDecoyBlocksExclusiveTarget);
        Run("Tote: ignored SKUs never block an exclusive target", ToteIgnoredSkusNeverBlock);
        Run("Tote: an empty required SKU accepts any item", ToteEmptyRequiredSkuAcceptsAny);

        Run("Gate: counts sources that are met right now", GateTracksLiveSourceState);
        Run("Gate: repeating a source's state is not a change", GateReportsOnlyChanges);
        Run("Gate: anonymous notifications count until reset", GateNotificationsCountUntilReset);

        Run("MisPick: the wrong item is removed once it has left the tote", MisPickRemovedAfterLeavingTote);
        Run("MisPick: putting the wrong item back undoes the removal", MisPickPutBackIsNotRemoved);
        Run("MisPick: tracks the correct item going in and out", MisPickTracksCorrectItem);
        Run("MisPick: other SKUs are ignored", MisPickIgnoresOtherSkus);

        Run("M1_05: items picked before the step opens complete it when it arms", ScenarioEarlyPicksCompleteWhenStepArms);
        Run("M1_07: a mis-pick removed before the step opens counts when it arms", ScenarioEarlyRemovalCountsWhenStepArms);
        Run("M1_06: decoys taken back out of the tote stop counting", ScenarioDecoysTakenOutStopCounting);

        Run("Cart: rotating a floor point follows Unity's yaw convention", CartRotationMatchesUnity);
        Run("Cart: world and cart-local floor points round-trip", CartLocalWorldRoundTrip);
        Run("Cart: one hand drags the cart by the point it holds", CartOneHandDrags);
        Run("Cart: one hand turns the cart with the head-to-hand direction", CartOneHandTurnsWithReach);
        Run("Cart: one hand keeps its heading while the hand is close to the head", CartOneHandKeepsHeadingWhenReachShort);
        Run("Cart: two hands turn the cart like a handlebar", CartTwoHandsTurnLikeHandlebar);
        Run("Cart: two hands moving together slide the cart", CartTwoHandsTranslate);
        Run("Cart: hands too close together keep the heading", CartTwoHandsTooCloseKeepHeading);
        Run("Cart: re-gripping at the current pose does not move the cart", CartRegripDoesNotJump);
        Run("Cart: a step never moves further than the cap", CartStepCapsDistance);
        Run("Cart: a step turns the short way round and wraps", CartStepTurnsShortWay);
        Run("Cart: blocked moves fall back to no turn, then X only, then Z only", CartBlockedFallbackOrder);
        Run("Cart: one hand pauses steering while the reach is short, then carries on from there", CartOneHandRebaselinesAfterShortReach);
        Run("Cart: poses with NaN or infinity are not finite", CartPoseFiniteness);
        Run("Angles: turns wrap into (-180, 180] across whole turns", AnglesWrapBoundaries);

        Run("Settle: an item locks after resting for the settle time", SettleLocksAfterRest);
        Run("Settle: holding or moving the item restarts the wait", SettleRestartsWhenHeldOrMoving);
        Run("Settle: NaN motion never counts as resting", SettleNaNIsNotRest);
        Run("Settle: a custom settle time is honoured", SettleCustomTime);

        Run("DropOff: points inside the rectangle count, points outside do not", DropOffContainsInside);
        Run("DropOff: a turned rectangle uses its own axes", DropOffTurnedRectangle);
        Run("DropOff: points exactly on the edge count as inside", DropOffEdgeIsInside);

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
            Console.WriteLine("FAIL  " + name);
            Console.WriteLine("      " + e.Message);
        }
    }

    private static void Expect(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    /// <summary>Stands in for ChallengeTutorialTrigger: completions only count while armed.</summary>
    private sealed class FakeChallenge
    {
        public bool Armed;
        public int Completions;

        public bool TryComplete()
        {
            if (!Armed) return false;
            Completions++;
            return true;
        }
    }

    private static ToteTally Tote(string sku, int count) => new ToteTally { RequiredSku = sku, RequiredCount = count };

    private static MisPickTracker MisPick() => new MisPickTracker { WrongSku = "SKU-5099", CorrectSku = "SKU-5005" };

    // ---- ArmedLatch --------------------------------------------------------------------------

    private static void LatchRejectedOfferDoesNotLatch()
    {
        var latch = new ArmedLatch();
        var challenge = new FakeChallenge { Armed = false };

        Expect(!latch.Offer(true, challenge.TryComplete), "Offer reported success while the challenge was not armed");
        Expect(!latch.IsLatched, "latched even though the challenge rejected the completion");
    }

    private static void LatchAcceptedOfferCompletesOnce()
    {
        var latch = new ArmedLatch();
        var challenge = new FakeChallenge { Armed = true };

        Expect(latch.Offer(true, challenge.TryComplete), "the first accepted offer should latch");
        Expect(!latch.Offer(true, challenge.TryComplete), "a second offer should not complete again");
        Expect(challenge.Completions == 1, $"expected 1 completion, got {challenge.Completions}");
    }

    private static void LatchUnmetConditionDoesNotComplete()
    {
        var latch = new ArmedLatch();
        var challenge = new FakeChallenge { Armed = true };

        Expect(!latch.Offer(false, challenge.TryComplete), "an unmet condition should not latch");
        Expect(challenge.Completions == 0, "the challenge must not be completed while the condition is unmet");
    }

    private static void LatchWithoutChallengeLatches()
    {
        var latch = new ArmedLatch();

        Expect(latch.Offer(true, null), "with no challenge wired the first met offer should latch");
        Expect(latch.IsLatched, "IsLatched should be true");
    }

    private static void LatchReentrantOfferCompletesOnce()
    {
        var latch = new ArmedLatch();
        int completions = 0;
        bool nestedResult = true;

        // Accepting a completion can re-enter the bridge (the framework arms the next challenge, whose
        // Armed handler offers again).
        bool TryComplete()
        {
            completions++;
            nestedResult = latch.Offer(true, () => { completions++; return true; });
            return true;
        }

        Expect(latch.Offer(true, TryComplete), "the outer offer should latch");
        Expect(!nestedResult, "the re-entrant offer should be ignored");
        Expect(completions == 1, $"expected 1 completion, got {completions}");
    }

    private static void LatchResetAllowsCompletingAgain()
    {
        var latch = new ArmedLatch();
        latch.Offer(true, null);

        latch.Reset();

        Expect(!latch.IsLatched, "reset should clear the latch");
        Expect(latch.Offer(true, null), "after a reset an offer should latch again");
    }

    // ---- ToteTally ---------------------------------------------------------------------------

    private static void ToteCountsOnlyRequiredSku()
    {
        var tote = Tote("SKU-1001", 3);
        tote.Enter(1, "SKU-1001");
        tote.Enter(2, "SKU-2002");

        Expect(tote.MatchingCount == 1, $"expected 1 matching item, got {tote.MatchingCount}");
    }

    private static void ToteCountsAnItemOnce()
    {
        var tote = Tote("SKU-1001", 3);
        tote.Enter(1, "SKU-1001");
        tote.Enter(1, "SKU-1001");

        Expect(tote.MatchingCount == 1, $"expected 1 matching item, got {tote.MatchingCount}");
        Expect(tote.Contains(1), "the item should be inside");
    }

    private static void ToteExitRemovesItem()
    {
        var tote = Tote("SKU-1001", 3);
        tote.Enter(1, "SKU-1001");

        Expect(tote.Exit(1, out string sku) && sku == "SKU-1001", "exit should report the item's SKU");
        Expect(tote.MatchingCount == 0, $"expected 0 matching items, got {tote.MatchingCount}");
        Expect(!tote.Exit(1, out _), "exiting an item that is not inside should report false");
    }

    private static void ToteTargetMetAtRequiredCount()
    {
        var tote = Tote("SKU-1001", 3);
        tote.Enter(1, "SKU-1001");
        tote.Enter(2, "SKU-1001");
        Expect(!tote.IsTargetMet, "2 of 3 should not meet the target");

        tote.Enter(3, "SKU-1001");
        Expect(tote.IsTargetMet, "3 of 3 should meet the target");
    }

    private static void ToteDecoyBlocksExclusiveTarget()
    {
        var tote = Tote("SKU-1001", 3);
        tote.RequireNoOtherItems = true;
        tote.Enter(1, "SKU-1001");
        tote.Enter(2, "SKU-1001");
        tote.Enter(3, "SKU-1001");
        tote.Enter(4, "SKU-2002");
        Expect(!tote.IsTargetMet, "a decoy in the tote should block an exclusive target");

        tote.Exit(4, out _);
        Expect(tote.IsTargetMet, "removing the decoy should meet the target");
    }

    private static void ToteIgnoredSkusNeverBlock()
    {
        var tote = Tote("SKU-1001", 3);
        tote.RequireNoOtherItems = true;
        tote.SetIgnoredSkus(new[] { "SKU-5099", "SKU-5005" });
        tote.Enter(9, "SKU-5099");
        tote.Enter(1, "SKU-1001");
        tote.Enter(2, "SKU-1001");
        tote.Enter(3, "SKU-1001");

        Expect(tote.IsTargetMet, "an ignored SKU (the M1_07 mis-pick) must not block M1_05");
        Expect(tote.IsIgnored("SKU-5099") && !tote.IsIgnored("SKU-2002"), "IsIgnored should reflect the ignore list");
    }

    private static void ToteEmptyRequiredSkuAcceptsAny()
    {
        var tote = Tote("", 2);
        tote.Enter(1, "A");
        tote.Enter(2, "B");

        Expect(tote.IsTargetMet, "an empty required SKU should accept any item");
    }

    // ---- MultiSourceGate ---------------------------------------------------------------------

    private static void GateTracksLiveSourceState()
    {
        var gate = new MultiSourceGate();
        object lineA = new object();
        object lineB = new object();
        gate.SetSourceMet(lineA, true);
        gate.SetSourceMet(lineB, true);
        Expect(gate.Count == 2, $"expected 2 met sources, got {gate.Count}");

        gate.SetSourceMet(lineA, false);
        Expect(gate.Count == 1, $"a source that is no longer met should stop counting, got {gate.Count}");
    }

    private static void GateReportsOnlyChanges()
    {
        var gate = new MultiSourceGate();
        object line = new object();

        Expect(gate.SetSourceMet(line, true), "the first report is a change");
        Expect(!gate.SetSourceMet(line, true), "repeating the same state is not a change");
        Expect(!gate.SetSourceMet(new object(), false), "an unknown source reported unmet is not a change");
    }

    private static void GateNotificationsCountUntilReset()
    {
        var gate = new MultiSourceGate();
        gate.Notify();
        gate.Notify();
        Expect(gate.Count == 2, $"expected 2, got {gate.Count}");

        gate.Reset();
        Expect(gate.Count == 0, $"reset should clear the count, got {gate.Count}");
    }

    // ---- MisPickTracker ----------------------------------------------------------------------

    private static void MisPickRemovedAfterLeavingTote()
    {
        var tracker = MisPick();
        Expect(!tracker.WrongItemRemoved, "not removed before it was ever inside");

        tracker.Enter(1, "SKU-5099");
        Expect(!tracker.WrongItemRemoved, "not removed while it is inside");

        tracker.Exit(1);
        Expect(tracker.WrongItemRemoved, "removed once it has left the tote");
    }

    private static void MisPickPutBackIsNotRemoved()
    {
        var tracker = MisPick();
        tracker.Enter(1, "SKU-5099");
        tracker.Exit(1);
        tracker.Enter(1, "SKU-5099");

        Expect(!tracker.WrongItemRemoved, "putting the wrong item back into the tote undoes the removal");
    }

    private static void MisPickTracksCorrectItem()
    {
        var tracker = MisPick();
        tracker.Enter(2, "SKU-5005");
        Expect(tracker.CorrectItemInside, "the correct item is inside");

        tracker.Exit(2);
        Expect(!tracker.CorrectItemInside, "the correct item has left");
    }

    private static void MisPickIgnoresOtherSkus()
    {
        var tracker = MisPick();
        tracker.Enter(3, "SKU-1001");
        tracker.Exit(3);

        Expect(!tracker.WrongSeen && !tracker.WrongItemRemoved && !tracker.CorrectItemInside, "other SKUs should be ignored");
    }

    // ---- Step scenarios (the review's soft-lock regressions) ---------------------------------

    private static void ScenarioEarlyPicksCompleteWhenStepArms()
    {
        var tote = Tote("SKU-1001", 3);
        tote.RequireNoOtherItems = true;
        tote.SetIgnoredSkus(new[] { "SKU-5099", "SKU-5005" });
        var latch = new ArmedLatch();
        var pickQuantity = new FakeChallenge { Armed = false };

        // The trainee drops the three items in during M1_04; the bridge offers on every change.
        for (int id = 1; id <= 3; id++)
        {
            tote.Enter(id, "SKU-1001");
            latch.Offer(tote.IsTargetMet, pickQuantity.TryComplete);
        }
        Expect(pickQuantity.Completions == 0 && !latch.IsLatched, "nothing should complete before M1_05 is active");

        // M1_05 opens: the challenge arms and the bridge offers again.
        pickQuantity.Armed = true;
        latch.Offer(tote.IsTargetMet, pickQuantity.TryComplete);
        Expect(pickQuantity.Completions == 1, $"M1_05 should complete when it arms, completions = {pickQuantity.Completions}");
    }

    private static void ScenarioEarlyRemovalCountsWhenStepArms()
    {
        var tracker = MisPick();
        var removed = new ArmedLatch();
        var removeChallenge = new FakeChallenge { Armed = false };

        tracker.Enter(1, "SKU-5099"); // pre-placed in the tote
        tracker.Exit(1);              // pulled out during M1_05
        removed.Offer(tracker.WrongItemRemoved, removeChallenge.TryComplete);
        Expect(!removed.IsLatched, "an early removal must not latch while M1_07 is inactive");

        removeChallenge.Armed = true;
        removed.Offer(tracker.WrongItemRemoved, removeChallenge.TryComplete);
        Expect(removeChallenge.Completions == 1, "the removal should count when M1_07 arms");
    }

    private static void ScenarioDecoysTakenOutStopCounting()
    {
        var line2002 = Tote("SKU-2002", 2);
        var gate = new MultiSourceGate();

        line2002.Enter(11, "SKU-2002"); // both decoys dropped in during M1_05
        line2002.Enter(12, "SKU-2002");
        gate.SetSourceMet(line2002, line2002.IsTargetMet);
        Expect(gate.Count == 1, "the SKU-2002 line is met while the decoys are inside");

        line2002.Exit(11, out _);
        line2002.Exit(12, out _);
        gate.SetSourceMet(line2002, line2002.IsTargetMet);
        Expect(gate.Count == 0, $"the line should stop counting once the decoys are removed, count = {gate.Count}");
    }

    // ---- Pushable cart (CartLogic) -----------------------------------------------------------

    private static void Near(float actual, float expected, string what, float tolerance = 1e-3f) =>
        Expect(Math.Abs(actual - expected) <= tolerance, $"{what}: expected {expected}, got {actual}");

    private static void NearPose(CartPose pose, float x, float z, float yaw, string what)
    {
        Near(pose.Position.X, x, what + " x");
        Near(pose.Position.Z, z, what + " z");
        Near(Angles.Delta(yaw, pose.Yaw), 0f, what + " yaw", 0.05f);
    }

    private static void CartRotationMatchesUnity()
    {
        // Quaternion.Euler(0, 90, 0) turns +Z into +X and +X into -Z.
        Planar forward = new Planar(0f, 1f).Rotated(90f);
        Near(forward.X, 1f, "+Z turned 90 x");
        Near(forward.Z, 0f, "+Z turned 90 z");
        Planar right = new Planar(1f, 0f).Rotated(90f);
        Near(right.X, 0f, "+X turned 90 x");
        Near(right.Z, -1f, "+X turned 90 z");
        Near(new Planar(1f, 0f).Yaw, 90f, "yaw of +X");
        Near(new Planar(0f, -1f).Yaw, 180f, "yaw of -Z");
    }

    private static void CartLocalWorldRoundTrip()
    {
        var cart = new CartPose(new Planar(2f, -1f), 37f);
        var local = new Planar(0.4f, -0.3f);
        Planar back = cart.ToLocal(cart.ToWorld(local));
        Near(back.X, local.X, "round-trip x");
        Near(back.Z, local.Z, "round-trip z");
    }

    // Cart at the origin facing +Z; the trainee holds the rear handle 0.5 m behind the centre, head 0.5 m further back.
    private static CartSteering OneHandGrip()
    {
        var steering = new CartSteering();
        steering.GripOne(new CartPose(new Planar(0f, 0f), 0f), new Planar(0f, -0.5f), new Planar(0f, -1f));
        return steering;
    }

    private static void CartOneHandDrags()
    {
        CartSteering steering = OneHandGrip();
        CartPose target = steering.TargetOne(new Planar(0f, 0.5f), new Planar(0f, 0f));
        NearPose(target, 0f, 1f, 0f, "walked 1 m forward");
    }

    private static void CartOneHandTurnsWithReach()
    {
        CartSteering steering = OneHandGrip();
        // A 90 degree turn about the head swings the hand from 0.5 m in front of the head (+Z) to 0.5 m to its right (+X).
        CartPose target = steering.TargetOne(new Planar(0.5f, -1f), new Planar(0f, -1f));
        NearPose(target, 1f, -1f, 90f, "turned 90 degrees");
    }

    private static void CartOneHandKeepsHeadingWhenReachShort()
    {
        CartSteering steering = OneHandGrip();
        // Hand pulled in to 7 cm from the head, off to the side: too close to read a direction from.
        CartPose target = steering.TargetOne(new Planar(0.05f, -0.95f), new Planar(0f, -1f));
        NearPose(target, 0.05f, -0.45f, 0f, "hand close to the head");
    }

    // Cart at the origin facing +Z; both hands on the rear handlebar, 0.6 m apart.
    private static CartSteering TwoHandGrip()
    {
        var steering = new CartSteering();
        steering.GripTwo(new CartPose(new Planar(0f, 0f), 0f), new Planar(-0.3f, -0.5f), new Planar(0.3f, -0.5f));
        return steering;
    }

    private static void CartTwoHandsTurnLikeHandlebar()
    {
        CartSteering steering = TwoHandGrip();
        // Handlebar turned 90 degrees about its middle (0, -0.5).
        CartPose target = steering.TargetTwo(new Planar(0f, -0.2f), new Planar(0f, -0.8f));
        NearPose(target, 0.5f, -0.5f, 90f, "handlebar turned 90 degrees");
    }

    private static void CartTwoHandsTranslate()
    {
        CartSteering steering = TwoHandGrip();
        CartPose target = steering.TargetTwo(new Planar(0.7f, 1.5f), new Planar(1.3f, 1.5f));
        NearPose(target, 1f, 2f, 0f, "hands moved by (1, 2)");
    }

    private static void CartTwoHandsTooCloseKeepHeading()
    {
        CartSteering steering = TwoHandGrip();
        CartPose target = steering.TargetTwo(new Planar(0f, -0.5f), new Planar(0.02f, -0.5f));
        Near(Angles.Delta(0f, target.Yaw), 0f, "heading", 0.05f);
    }

    private static void CartRegripDoesNotJump()
    {
        CartSteering steering = OneHandGrip();
        CartPose moved = steering.TargetOne(new Planar(0.5f, -1f), new Planar(0f, -1f));   // turned 90

        // A second hand joins at the current pose: nothing moves until the hands do.
        var handA = new Planar(0.5f, -1f);
        Planar handB = moved.ToWorld(new Planar(0.3f, -0.5f));
        steering.GripTwo(moved, handA, handB);
        NearPose(steering.TargetTwo(handA, handB), moved.Position.X, moved.Position.Z, moved.Yaw, "after the second hand joins");

        // The first hand lets go: the remaining hand takes a fresh one-hand grip.
        var head = new Planar(handB.X - 0.5f, handB.Z);
        steering.GripOne(moved, handB, head);
        NearPose(steering.TargetOne(handB, head), moved.Position.X, moved.Position.Z, moved.Yaw, "after the first hand lets go");
    }

    private static void CartStepCapsDistance()
    {
        var current = new CartPose(new Planar(0f, 0f), 0f);
        NearPose(CartSteering.Step(current, new CartPose(new Planar(3f, 4f), 0f), 0.05f, 180f), 0.03f, 0.04f, 0f, "capped step");
        NearPose(CartSteering.Step(current, new CartPose(new Planar(0.01f, 0f), 0f), 0.05f, 180f), 0.01f, 0f, 0f, "short step");
    }

    private static void CartStepTurnsShortWay()
    {
        var origin = new Planar(0f, 0f);
        CartPose step = CartSteering.Step(new CartPose(origin, 170f), new CartPose(origin, -170f), 1f, 5f);
        Near(step.Yaw, 175f, "turns +5 toward -170 through 180");
        CartPose wrapped = CartSteering.Step(new CartPose(origin, 178f), new CartPose(origin, -178f), 1f, 10f);
        Near(wrapped.Yaw, -178f, "wraps past 180");
    }

    private static void CartBlockedFallbackOrder()
    {
        var current = new CartPose(new Planar(1f, 2f), 10f);
        var next = new CartPose(new Planar(1.2f, 2.3f), 20f);
        var tries = new CartPose[4];
        CartSteering.BlockedFallbacks(current, next, tries);
        NearPose(tries[0], 1.2f, 2.3f, 20f, "full move");
        NearPose(tries[1], 1.2f, 2.3f, 10f, "move without turning");
        NearPose(tries[2], 1.2f, 2f, 10f, "slide along X");
        NearPose(tries[3], 1f, 2.3f, 10f, "slide along Z");
    }

    private static void SettleLocksAfterRest()
    {
        var timer = new SettleTimer();   // 0.3 s
        for (int i = 0; i < 14; i++)
            Expect(!timer.Tick(false, 0.001f, 0.2f, 0.02f), $"locked too early at tick {i + 1}");
        Expect(timer.Tick(false, 0.001f, 0.2f, 0.02f), "should lock after 15 ticks of 0.02 s");
    }

    private static void SettleRestartsWhenHeldOrMoving()
    {
        var timer = new SettleTimer();
        for (int i = 0; i < 10; i++) timer.Tick(false, 0f, 0f, 0.02f);
        Expect(!timer.Tick(true, 0f, 0f, 0.02f), "a held item never locks");
        for (int i = 0; i < 14; i++)
            Expect(!timer.Tick(false, 0f, 0f, 0.02f), "the wait should restart after being held");
        Expect(!timer.Tick(false, 0.01f, 0f, 0.02f), "moving 1 cm in a tick is not resting");
        Expect(!timer.Tick(false, 0f, 5f, 0.02f), "turning 5 degrees in a tick is not resting");
        for (int i = 0; i < 14; i++) timer.Tick(false, 0f, 0f, 0.02f);
        Expect(timer.Tick(false, 0f, 0f, 0.02f), "15 quiet ticks after moving should lock");
    }

    private static void DropOffContainsInside()
    {
        var centre = new Planar(1.89f, 3.21f);
        var half = new Planar(0.8f, 0.65f);
        Expect(DropOffArea.Contains(centre, 0f, half, centre), "the centre is inside");
        Expect(DropOffArea.Contains(centre, 0f, half, new Planar(2.6f, 3.8f)), "a point near a corner is inside");
        Expect(!DropOffArea.Contains(centre, 0f, half, new Planar(2.8f, 3.21f)), "a point beyond the X edge is outside");
        Expect(!DropOffArea.Contains(centre, 0f, half, new Planar(1.89f, 3.9f)), "a point beyond the Z edge is outside");
    }

    private static void DropOffTurnedRectangle()
    {
        var centre = new Planar(0f, 0f);
        var half = new Planar(1f, 0.2f);   // long along its own X
        Expect(!DropOffArea.Contains(centre, 0f, half, new Planar(0f, 0.8f)), "unturned: 0.8 along Z is outside");
        Expect(DropOffArea.Contains(centre, 90f, half, new Planar(0f, 0.8f)), "turned 90: its long side now runs along world Z");
    }

    private static void CartOneHandRebaselinesAfterShortReach()
    {
        CartSteering steering = OneHandGrip();   // reach points +Z (yaw 0)
        var head = new Planar(0f, -1f);

        // Hand pulled in to the head: steering pauses, heading stays 0.
        CartPose close = steering.TargetOne(new Planar(0.05f, -0.97f), head);
        Near(Angles.Delta(0f, close.Yaw), 0f, "heading while the reach is short", 0.05f);

        // The hand comes back out on the other side of the head (reach now points -Z): without re-baselining this
        // would read as a 180 degree turn; it must carry on from the current heading instead.
        CartPose back = steering.TargetOne(new Planar(0f, -1.5f), head);
        Near(Angles.Delta(0f, back.Yaw), 0f, "heading after the reach comes back", 0.05f);

        // From there, steering works relative to the new reach: a quarter turn of the reach turns the cart a quarter.
        CartPose turned = steering.TargetOne(new Planar(-0.5f, -1f), head);   // reach -Z -> -X is +90 degrees
        Near(Angles.Delta(90f, turned.Yaw), 0f, "steers relative to the new reach", 0.05f);
    }

    private static void CartPoseFiniteness()
    {
        Expect(new CartPose(new Planar(1f, 2f), 30f).IsFinite, "an ordinary pose is finite");
        Expect(!new CartPose(new Planar(float.NaN, 2f), 30f).IsFinite, "NaN X is not finite");
        Expect(!new CartPose(new Planar(1f, float.PositiveInfinity), 30f).IsFinite, "infinite Z is not finite");
        Expect(!new CartPose(new Planar(1f, 2f), float.NaN).IsFinite, "NaN yaw is not finite");
    }

    private static void AnglesWrapBoundaries()
    {
        Near(Angles.Delta(0f, 180f), 180f, "Delta(0, 180)");
        Near(Angles.Delta(0f, -180f), 180f, "Delta(0, -180) is +180 (the range is (-180, 180])");
        Near(Angles.Delta(10f, -530f), 180f, "Delta(10, -530)");
        Near(Angles.Delta(359f, 1f), 2f, "Delta(359, 1) wraps forward");
        Near(Angles.Delta(1f, 359f), -2f, "Delta(1, 359) wraps backward");
        Near(Angles.Wrap(540f), 180f, "Wrap(540)");
        Near(Angles.Wrap(-190f), 170f, "Wrap(-190)");
    }

    private static void SettleNaNIsNotRest()
    {
        var timer = new SettleTimer();
        for (int i = 0; i < 30; i++)
            Expect(!timer.Tick(false, float.NaN, 0f, 0.02f), "NaN movement must never lock");
        for (int i = 0; i < 30; i++)
            Expect(!timer.Tick(false, 0f, float.NaN, 0.02f), "NaN rotation must never lock");
    }

    private static void SettleCustomTime()
    {
        var timer = new SettleTimer { SettleTime = 0.1f };
        for (int i = 0; i < 4; i++)
            Expect(!timer.Tick(false, 0f, 0f, 0.02f), $"locked too early at tick {i + 1}");
        Expect(timer.Tick(false, 0f, 0f, 0.02f), "should lock after 5 ticks of 0.02 s");
        timer.Reset();
        Expect(!timer.Tick(false, 0f, 0f, 0.02f), "Reset should restart the wait");
    }

    private static void DropOffEdgeIsInside()
    {
        var centre = new Planar(0f, 0f);
        var half = new Planar(0.8f, 0.65f);
        Expect(DropOffArea.Contains(centre, 0f, half, new Planar(0.8f, 0f)), "on the X edge");
        Expect(DropOffArea.Contains(centre, 0f, half, new Planar(0f, -0.65f)), "on the Z edge");
        Expect(!DropOffArea.Contains(centre, 0f, half, new Planar(0.8001f, 0f)), "just past the X edge");
    }
}
