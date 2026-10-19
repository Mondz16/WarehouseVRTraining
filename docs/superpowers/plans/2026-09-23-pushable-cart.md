# Pushable Utility Cart Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the `Utility_cart` (which carries `M1_Tote`) pushable by the trainee's hands, with products riding along, and require the cart back at a drop-off spot to finish M1_06.

**Architecture:** A kinematic cart root moved by `PushCart` from the AutoHand hands holding a separate `Handles` Grabbable body, with overlap checks against static geometry. `CartCargo` (on the tote) FixedJoints settled products to the cart body. `CartDropOffZone` reports "cart parked" to the existing `M1_MultiLineGate`. All steering, clamping, settling and zone math lives in plain C# (`CartLogic.cs`), unit-tested with `dotnet`.

**Tech Stack:** Unity 6000.3.12f1 (URP, PhysX, 50 Hz fixed step), AutoHand (`Autohand` namespace), C# 9, .NET 10 console test runner (`Tests/OrderPickingLogic`), MCP for Unity (`mcpforunity` server) for all scene edits.

**Spec:** `docs/superpowers/specs/2026-09-23-pushable-cart-design.md`

> **Executed 2026-09-23 (inline).** Deviations from the steps below, all reflected in the spec and CLAUDE.md:
> `M1_MisPick_Wrong` was un-parented with a temporary `[MenuItem]` script (the empty-string `parent` didn't survive
> the MCP client); a stray default 1 m BoxCollider that a bridge hiccup added to `LowerWall_Front` was removed;
> `PushCart._floorClearance` raised 0.05 → 0.08 (the 6 cm B pick-area floor plate); `CartCargo` also treats
> `Grabbable.BeingGrabbed()` as held. Play-mode checks ran with the test drive armed *before* Play (MCP preflight
> tools recompile mid-play); blocking was verified against the A-02-03 bin lid plate because the racks/walls have
> no colliders. The editor crashed once on leaving Play mode (Meta XR Simulator) and was relaunched; nothing was lost.

## Global Constraints

- Scripts go in `Assets/Scripts/ImmersiveTraining/TrainingInteractions/`, namespace `ImmersiveTraining.TrainingInteractions`, C# 9 (no file-scoped namespaces, no global usings). `CartLogic.cs` must not reference `UnityEngine`.
- MCP: route every call to `WarehouseVRTraining@238c2776` (call `set_active_instance` first; a second editor, `social-universe`, is also connected). Instance IDs change after a domain reload: re-find objects by name/path right before using an ID. `create` positions are **local to the parent**; `modify` with `parent` keeps the world pose; `parent: ""` moves to the scene root. `execute_code` does not work here (no Roslyn; CodeDom hits a path-length error).
- The live editor runs on the `main` checkout; write files directly in the project and call `refresh_unity` (scope all, compile request) so Unity imports them.
- **No git commits.** `main` holds the owner's uncommitted work (including in `BasicScene.unity`); each task ends with a verification checkpoint instead of a commit.
- Values: cart root (1.890, 0, 3.210); max speed 2.5 m/s; max turn 180°/s; floor clearance 0.05 m; settle 0.3 s, 3 mm/tick, 1°/tick; one-hand min reach 0.2 m; two-hand min span 0.1 m; drop-off 1.6 × 1.3 m.
- M1_06 challenge title becomes exactly: `Complete All 3 Picks and Return the Cart to the Drop-Off`.
- Save the scene (`manage_scene save`) after every scene task and before entering Play mode.

## File Structure

| File | Responsibility |
|---|---|
| `Assets/Scripts/ImmersiveTraining/TrainingInteractions/CartLogic.cs` (new) | Plain C#: `Planar`, `Angles`, `CartPose`, `CartSteering`, `SettleTimer`, `DropOffArea` |
| `Assets/Scripts/ImmersiveTraining/TrainingInteractions/PushCart.cs` (new) | Hands → target pose → caps → obstacle check → move cart + handles bodies |
| `Assets/Scripts/ImmersiveTraining/TrainingInteractions/CartCargo.cs` (new) | Joint settled products to the cart; unjoint on grab |
| `Assets/Scripts/ImmersiveTraining/TrainingInteractions/CartDropOffZone.cs` (new) | Report "cart centre inside" to a `MultiCounterGate`; draw the outline |
| `Tests/OrderPickingLogic/OrderPickingLogic.Tests.csproj` | Link `CartLogic.cs` |
| `Tests/OrderPickingLogic/Program.cs` | 16 new cart tests |
| `Assets/Scenes/BasicScene.unity` | Cart hierarchy, handles, walls, components, drop-off, gate count, route waypoints |
| `Assets/SO/TutorialData/M1_06_Multi_Line_Order.asset`, `Assets/ReferenceData/training_flow_module1.json` | M1_06 challenge title |
| `CLAUDE.md` | Cart section, M1_06 chain, stale "kinematic pickables" note, checklist |

---

### Task 1: Cart logic (plain C#) with unit tests

**Files:**
- Create: `Assets/Scripts/ImmersiveTraining/TrainingInteractions/CartLogic.cs`
- Modify: `Tests/OrderPickingLogic/OrderPickingLogic.Tests.csproj`
- Modify: `Tests/OrderPickingLogic/Program.cs`

**Interfaces:**
- Produces: `Planar(float x, float z)` with `X`, `Z`, `Length`, `Yaw`, `Rotated(float yaw)`, `Midpoint`, `+ - *`; `Angles.Delta(from, to)`, `Angles.Wrap(angle)`; `CartPose(Planar position, float yaw)` with `Position`, `Yaw`, `ToWorld`, `ToLocal`; `CartSteering` with `GripOne(CartPose, Planar hand, Planar head)`, `GripTwo(CartPose, Planar handA, Planar handB)`, `TargetOne(Planar hand, Planar head)`, `TargetTwo(Planar handA, Planar handB)`, static `Step(CartPose current, CartPose target, float maxMove, float maxTurn)`, static `BlockedFallbacks(CartPose current, CartPose next, CartPose[] into)`; `SettleTimer` with `SettleTime`, `MoveTolerance`, `TurnTolerance`, `Tick(bool held, float moved, float turned, float deltaTime)`, `Reset()`; `DropOffArea.Contains(Planar centre, float yaw, Planar halfSize, Planar point)`.

- [x] **Step 1: Link the new file into the test project**

In `Tests/OrderPickingLogic/OrderPickingLogic.Tests.csproj`, add after the `ChallengeBridgeLogic.cs` compile item:

```xml
    <Compile Include="..\..\Assets\Scripts\ImmersiveTraining\TrainingInteractions\CartLogic.cs" Link="CartLogic.cs" />
```

- [x] **Step 2: Write the failing tests**

In `Tests/OrderPickingLogic/Program.cs`, add these `Run(...)` lines just before the blank `Console.WriteLine();` that precedes the summary:

```csharp
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

        Run("Settle: an item locks after resting for the settle time", SettleLocksAfterRest);
        Run("Settle: holding or moving the item restarts the wait", SettleRestartsWhenHeldOrMoving);

        Run("DropOff: points inside the rectangle count, points outside do not", DropOffContainsInside);
        Run("DropOff: a turned rectangle uses its own axes", DropOffTurnedRectangle);
```

Add these members at the end of the `Program` class (before its closing brace):

```csharp
    // ---- Pushable cart (CartLogic.cs) ----

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
```

- [x] **Step 3: Run the tests to verify they fail**

Run: `dotnet run --project Tests/OrderPickingLogic`
Expected: build FAILS — the linked `CartLogic.cs` does not exist yet / `CartPose`, `CartSteering`, `Planar`, `SettleTimer`, `DropOffArea`, `Angles` not found.

- [x] **Step 4: Write the implementation**

Create `Assets/Scripts/ImmersiveTraining/TrainingInteractions/CartLogic.cs`:

```csharp
using System;

namespace ImmersiveTraining.TrainingInteractions
{
    // Plain C# (no UnityEngine) steering and decision logic behind the pushable cart (PushCart,
    // CartCargo, CartDropOffZone), kept separate so it can be unit-tested outside Unity:
    // dotnet run --project Tests/OrderPickingLogic
    //
    // Everything lives on the floor plane: X/Z are world metres, yaw is degrees about +Y in Unity's
    // convention (yaw 0 faces +Z, yaw 90 faces +X).

    /// <summary>A point or direction on the floor plane.</summary>
    public readonly struct Planar
    {
        public readonly float X;
        public readonly float Z;

        public Planar(float x, float z)
        {
            X = x;
            Z = z;
        }

        public float Length => (float)Math.Sqrt((double)X * X + (double)Z * Z);

        /// <summary>Heading of this direction in degrees: +Z is 0, +X is 90.</summary>
        public float Yaw => (float)(Math.Atan2(X, Z) * 180.0 / Math.PI);

        /// <summary>This vector turned by <paramref name="yaw"/> degrees about +Y (like Quaternion.Euler(0, yaw, 0)).</summary>
        public Planar Rotated(float yaw)
        {
            double radians = yaw * Math.PI / 180.0;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            return new Planar((float)(X * cos + Z * sin), (float)(-X * sin + Z * cos));
        }

        public static Planar Midpoint(Planar a, Planar b) => new Planar((a.X + b.X) * 0.5f, (a.Z + b.Z) * 0.5f);

        public static Planar operator +(Planar a, Planar b) => new Planar(a.X + b.X, a.Z + b.Z);
        public static Planar operator -(Planar a, Planar b) => new Planar(a.X - b.X, a.Z - b.Z);
        public static Planar operator *(Planar a, float scale) => new Planar(a.X * scale, a.Z * scale);

        public override string ToString() => $"({X:F3}, {Z:F3})";
    }

    /// <summary>Angle helpers, in degrees.</summary>
    public static class Angles
    {
        /// <summary>Signed turn from <paramref name="from"/> to <paramref name="to"/> the short way round, in (-180, 180].</summary>
        public static float Delta(float from, float to)
        {
            float delta = (to - from) % 360f;
            if (delta > 180f) delta -= 360f;
            else if (delta <= -180f) delta += 360f;
            return delta;
        }

        /// <summary><paramref name="angle"/> wrapped into (-180, 180].</summary>
        public static float Wrap(float angle) => Delta(0f, angle);
    }

    /// <summary>Where the cart stands on the floor and which way it faces.</summary>
    public readonly struct CartPose
    {
        public readonly Planar Position;
        public readonly float Yaw;

        public CartPose(Planar position, float yaw)
        {
            Position = position;
            Yaw = yaw;
        }

        /// <summary>World floor point of a point given in the cart's own floor coordinates.</summary>
        public Planar ToWorld(Planar local) => Position + local.Rotated(Yaw);

        /// <summary>The cart's own floor coordinates of a world floor point.</summary>
        public Planar ToLocal(Planar world) => (world - Position).Rotated(-Yaw);

        public override string ToString() => $"{Position} yaw {Yaw:F1}";
    }

    /// <summary>
    /// Works out where the cart should be from the hands holding it. The grip is taken again from the
    /// cart's current pose every time the set of holding hands changes, so grabbing or letting go never
    /// makes the cart jump.
    ///
    /// One hand: the held point follows the hand, and the cart turns as much as the direction from the
    /// head to the hand turns (turning your body or swinging the hand sideways steers; looking around
    /// does not). Two hands: the cart follows the hands' midpoint and turns with the line between them,
    /// like a handlebar.
    /// </summary>
    public sealed class CartSteering
    {
        /// <summary>Head-to-hand distance (m) below which the one-hand heading is held.</summary>
        public const float MinReach = 0.2f;

        /// <summary>Hand spacing (m) below which two hands stop turning the cart.</summary>
        public const float MinSpan = 0.1f;

        private Planar _anchorA;
        private Planar _anchorB;
        private float _gripYaw;
        private float _reachYaw;
        private bool _hasReach;
        private float _heading;

        /// <summary>Takes a one-hand grip at the cart's current pose.</summary>
        public void GripOne(CartPose cart, Planar hand, Planar head)
        {
            _anchorA = cart.ToLocal(hand);
            _gripYaw = cart.Yaw;
            _heading = cart.Yaw;

            Planar reach = hand - head;
            _hasReach = reach.Length >= MinReach;
            if (_hasReach) _reachYaw = reach.Yaw;
        }

        /// <summary>Takes a two-hand grip at the cart's current pose.</summary>
        public void GripTwo(CartPose cart, Planar handA, Planar handB)
        {
            _anchorA = cart.ToLocal(handA);
            _anchorB = cart.ToLocal(handB);
            _heading = cart.Yaw;
        }

        /// <summary>Pose the cart should take for a one-hand grip.</summary>
        public CartPose TargetOne(Planar hand, Planar head)
        {
            Planar reach = hand - head;
            if (reach.Length >= MinReach)
            {
                if (!_hasReach)
                {
                    // The reach only became usable now: steer relative to this moment.
                    _reachYaw = reach.Yaw;
                    _gripYaw = _heading;
                    _hasReach = true;
                }

                _heading = Angles.Wrap(_gripYaw + Angles.Delta(_reachYaw, reach.Yaw));
            }

            return new CartPose(hand - _anchorA.Rotated(_heading), _heading);
        }

        /// <summary>Pose the cart should take for a two-hand grip.</summary>
        public CartPose TargetTwo(Planar handA, Planar handB)
        {
            Planar span = handB - handA;
            Planar anchorSpan = _anchorB - _anchorA;
            if (span.Length >= MinSpan && anchorSpan.Length >= MinSpan)
                _heading = Angles.Wrap(span.Yaw - anchorSpan.Yaw);

            Planar anchorMid = Planar.Midpoint(_anchorA, _anchorB);
            return new CartPose(Planar.Midpoint(handA, handB) - anchorMid.Rotated(_heading), _heading);
        }

        /// <summary>
        /// Moves <paramref name="current"/> toward <paramref name="target"/> by at most
        /// <paramref name="maxMove"/> metres and <paramref name="maxTurn"/> degrees (the short way round).
        /// </summary>
        public static CartPose Step(CartPose current, CartPose target, float maxMove, float maxTurn)
        {
            Planar move = target.Position - current.Position;
            float distance = move.Length;
            if (distance > maxMove && distance > 0f) move = move * (maxMove / distance);

            float turn = Angles.Delta(current.Yaw, target.Yaw);
            if (turn > maxTurn) turn = maxTurn;
            else if (turn < -maxTurn) turn = -maxTurn;

            return new CartPose(current.Position + move, Angles.Wrap(current.Yaw + turn));
        }

        /// <summary>
        /// Poses to try, in order, when the move from <paramref name="current"/> to <paramref name="next"/>
        /// may be blocked: the full move, the move without turning, then sliding along X only and Z only.
        /// <paramref name="into"/> must hold at least 4 poses.
        /// </summary>
        public static void BlockedFallbacks(CartPose current, CartPose next, CartPose[] into)
        {
            into[0] = next;
            into[1] = new CartPose(next.Position, current.Yaw);
            into[2] = new CartPose(new Planar(next.Position.X, current.Position.Z), current.Yaw);
            into[3] = new CartPose(new Planar(current.Position.X, next.Position.Z), current.Yaw);
        }
    }

    /// <summary>
    /// Decides when an item resting in the cart should lock to it: it must be free (not held) and still
    /// relative to the cart for <see cref="SettleTime"/> seconds.
    /// </summary>
    public sealed class SettleTimer
    {
        // Absorbs float error so that, e.g., 15 ticks of 0.02 s count as 0.3 s.
        private const float Epsilon = 1e-4f;

        private float _rested;

        public float SettleTime { get; set; } = 0.3f;

        /// <summary>Largest movement (m) per tick, relative to the cart, that still counts as resting.</summary>
        public float MoveTolerance { get; set; } = 0.003f;

        /// <summary>Largest rotation (degrees) per tick, relative to the cart, that still counts as resting.</summary>
        public float TurnTolerance { get; set; } = 1f;

        /// <summary>Feeds one physics tick; returns true once the item has rested long enough.</summary>
        public bool Tick(bool held, float moved, float turned, float deltaTime)
        {
            if (held || moved > MoveTolerance || turned > TurnTolerance)
            {
                _rested = 0f;
                return false;
            }

            _rested += deltaTime;
            return _rested >= SettleTime - Epsilon;
        }

        public void Reset() => _rested = 0f;
    }

    /// <summary>A rectangle on the floor (the cart drop-off).</summary>
    public static class DropOffArea
    {
        /// <summary>
        /// True if <paramref name="point"/> lies inside the rectangle centred on <paramref name="centre"/>,
        /// turned by <paramref name="yaw"/> degrees, with half-sizes <paramref name="halfSize"/> along its own X and Z.
        /// </summary>
        public static bool Contains(Planar centre, float yaw, Planar halfSize, Planar point)
        {
            Planar local = new CartPose(centre, yaw).ToLocal(point);
            return Math.Abs(local.X) <= halfSize.X && Math.Abs(local.Z) <= halfSize.Z;
        }
    }
}
```

- [x] **Step 5: Run the tests to verify they pass**

Run: `dotnet run --project Tests/OrderPickingLogic`
Expected: `39 passed, 0 failed` (23 existing + 16 new).

- [x] **Step 6: Unity compiles it**

MCP: `refresh_unity` (mode force, scope all, compile request, wait) → `read_console` types error.
Expected: no `CartLogic` compile errors (Unity also compiles C# 9 here).

- [x] **Step 7: Checkpoint** — test output shows 39 passed; console clean.

---

### Task 2: `PushCart` component

**Files:**
- Create: `Assets/Scripts/ImmersiveTraining/TrainingInteractions/PushCart.cs`

**Interfaces:**
- Consumes: `CartSteering`, `CartPose`, `Planar` (Task 1); AutoHand `Grabbable.GetHeldBy()` (`List<Hand>`), `HandBase.follow` (`Transform`).
- Produces: `PushCart` MonoBehaviour with serialized `_handles` (Grabbable), `_head` (Transform), `_footprint` (MeshFilter), `_maxSpeed`, `_maxTurnSpeed`, `_blockingMask`, `_floorClearance`, `_testDriveVelocity` (Vector2), `_testDriveTurnSpeed`; public `bool IsHeld`. Requires a Rigidbody on the same GameObject.

- [x] **Step 1: Write the component**

Create `Assets/Scripts/ImmersiveTraining/TrainingInteractions/PushCart.cs`:

```csharp
using System.Collections.Generic;
using Autohand;
using UnityEngine;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// Makes the order-picking cart pushable. While hands hold its handles (an AutoHand
    /// <see cref="Grabbable"/> on a child kinematic body) the cart follows them on the floor: the held
    /// point stays with the controller, one hand steers with the head-to-hand direction and two hands
    /// steer like a handlebar (<see cref="CartSteering"/>). Speed and turn rate are capped, and every move
    /// is checked against static geometry (racks, walls, pallets): a blocked cart slides along the
    /// obstacle or stops, and the stretched hand lets go as with a real snag. Loose physics objects are
    /// pushed aside.
    ///
    /// The cart body is kinematic (MovePosition/MoveRotation), so it never tips, drifts or jitters and
    /// hands can't shove it while it is parked. Items that settle in it are held by <see cref="CartCargo"/>.
    /// Put this on an upright root at floor level; the model, handles and tote are its children.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PushCart : MonoBehaviour
    {
        [Tooltip("AutoHand Grabbable on the Handles child (kinematic Rigidbody + the handle colliders).")]
        [SerializeField] private Grabbable _handles;

        [Tooltip("The player's head camera. One hand steers with the direction from the head to the hand.")]
        [SerializeField] private Transform _head;

        [Tooltip("The cart model's MeshFilter. Its bounds size the box used to check for obstacles.")]
        [SerializeField] private MeshFilter _footprint;

        [Header("Movement")]
        [Tooltip("Fastest the cart moves, in m/s (the player walks at up to 2.3 m/s).")]
        [SerializeField] private float _maxSpeed = 2.5f;

        [Tooltip("Fastest the cart turns, in degrees per second.")]
        [SerializeField] private float _maxTurnSpeed = 180f;

        [Header("Obstacles")]
        [Tooltip("Layers checked for obstacles. Only static or kinematic colliders outside the cart and the hands block it.")]
        [SerializeField] private LayerMask _blockingMask = ~0;

        [Tooltip("Height above the cart's base where the obstacle check starts, so the floor never blocks it.")]
        [SerializeField] private float _floorClearance = 0.05f;

        [Header("Editor testing")]
        [Tooltip("While no hand holds the cart, drives it at this world X/Z velocity (m/s) through the same caps and obstacle checks as a push. Leave at zero.")]
        [SerializeField] private Vector2 _testDriveVelocity;

        [Tooltip("While no hand holds the cart, turns it at this rate (degrees per second) through the same checks. Leave at zero.")]
        [SerializeField] private float _testDriveTurnSpeed;

        private const float Skin = 0.01f;

        private readonly List<Hand> _hands = new List<Hand>(2);
        private readonly List<Hand> _holding = new List<Hand>(2);
        private readonly CartSteering _steering = new CartSteering();
        private readonly HashSet<Collider> _ignored = new HashSet<Collider>();
        private readonly Collider[] _overlaps = new Collider[64];
        private readonly CartPose[] _tries = new CartPose[4];

        private Rigidbody _body;
        private Rigidbody _handlesBody;
        private Vector3 _handlesLocalPosition;
        private Quaternion _handlesLocalRotation = Quaternion.identity;
        private Vector3 _boxCenter;
        private Vector3 _boxHalfExtents;
        private bool _wasTestDriving;

        /// <summary>True while at least one hand holds the handles.</summary>
        public bool IsHeld => _hands.Count > 0;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _body.isKinematic = true;
            _body.useGravity = false;
            _body.interpolation = RigidbodyInterpolation.Interpolate;

            if (_handles != null)
            {
                _handlesBody = _handles.GetComponent<Rigidbody>();
                _handlesLocalPosition = transform.InverseTransformPoint(_handles.transform.position);
                _handlesLocalRotation = Quaternion.Inverse(transform.rotation) * _handles.transform.rotation;
            }

            CacheObstacleBox();
        }

        private void FixedUpdate()
        {
            SyncHands();

            bool testDriving = _hands.Count == 0 && (_testDriveVelocity != Vector2.zero || _testDriveTurnSpeed != 0f);
            if (testDriving && !_wasTestDriving) CaptureIgnored();
            _wasTestDriving = testDriving;

            float dt = Time.fixedDeltaTime;
            CartPose current = CurrentPose();
            CartPose target;
            if (_hands.Count == 2)
                target = _steering.TargetTwo(HandPoint(_hands[0]), HandPoint(_hands[1]));
            else if (_hands.Count == 1)
                target = _steering.TargetOne(HandPoint(_hands[0]), HeadPoint(_hands[0]));
            else if (testDriving)
                target = new CartPose(current.Position + new Planar(_testDriveVelocity.x, _testDriveVelocity.y) * dt,
                                      current.Yaw + _testDriveTurnSpeed * dt);
            else
                return;

            CartPose next = CartSteering.Step(current, target, _maxSpeed * dt, _maxTurnSpeed * dt);
            CartSteering.BlockedFallbacks(current, next, _tries);
            foreach (CartPose pose in _tries)
            {
                if (IsBlocked(pose)) continue;
                MoveTo(pose);
                return;
            }
        }

        // Mirrors the (at most two) hands holding the handles and re-grips whenever that set changes,
        // so grabbing or letting go never moves the cart.
        private void SyncHands()
        {
            _holding.Clear();
            if (_handles != null)
                foreach (Hand hand in _handles.GetHeldBy())
                    if (hand != null && hand.follow != null && _holding.Count < 2)
                        _holding.Add(hand);

            if (SameHands()) return;

            bool pushStarted = _hands.Count == 0;
            _hands.Clear();
            _hands.AddRange(_holding);
            if (_hands.Count == 0) return;

            CartPose cart = CurrentPose();
            if (_hands.Count == 1) _steering.GripOne(cart, HandPoint(_hands[0]), HeadPoint(_hands[0]));
            else _steering.GripTwo(cart, HandPoint(_hands[0]), HandPoint(_hands[1]));

            if (pushStarted) CaptureIgnored();
        }

        private bool SameHands()
        {
            if (_holding.Count != _hands.Count) return false;
            for (int i = 0; i < _hands.Count; i++)
                if (_holding[i] != _hands[i]) return false;
            return true;
        }

        private CartPose CurrentPose()
        {
            Vector3 position = _body.position;
            return new CartPose(new Planar(position.x, position.z), _body.rotation.eulerAngles.y);
        }

        private static Planar HandPoint(Hand hand) => Flat(hand.follow.position);

        // Without a head reference the hand itself is used, which leaves the heading unchanged (no steering).
        private Planar HeadPoint(Hand hand) => _head != null ? Flat(_head.position) : HandPoint(hand);

        private static Planar Flat(Vector3 point) => new Planar(point.x, point.z);

        private void MoveTo(CartPose pose)
        {
            var position = new Vector3(pose.Position.X, _body.position.y, pose.Position.Z);
            Quaternion rotation = Quaternion.Euler(0f, pose.Yaw, 0f);
            _body.MovePosition(position);
            _body.MoveRotation(rotation);

            // Move the handles in the same physics step so the hand holding them never lags the cart.
            if (_handlesBody != null)
            {
                _handlesBody.MovePosition(position + rotation * _handlesLocalPosition);
                _handlesBody.MoveRotation(rotation * _handlesLocalRotation);
            }
        }

        private bool IsBlocked(CartPose pose)
        {
            int count = OverlapAt(pose);
            for (int i = 0; i < count; i++)
                if (Blocks(_overlaps[i]) && !_ignored.Contains(_overlaps[i])) return true;
            return false;
        }

        // Static and kinematic colliders outside the cart block it. Dynamic bodies (loose items, the
        // player's body, hands) are pushed aside, and anything on an AutoHand hand (e.g. the wrist device,
        // which may be kinematic) never blocks.
        private bool Blocks(Collider other)
        {
            if (other.transform.IsChildOf(transform)) return false;
            Rigidbody body = other.attachedRigidbody;
            if (body != null && !body.isKinematic) return false;
            return other.GetComponentInParent<Hand>() == null;
        }

        // Anything the cart already overlaps when a push starts is ignored for that push, so it never gets stuck.
        private void CaptureIgnored()
        {
            _ignored.Clear();
            int count = OverlapAt(CurrentPose());
            for (int i = 0; i < count; i++)
                if (Blocks(_overlaps[i])) _ignored.Add(_overlaps[i]);
        }

        private int OverlapAt(CartPose pose)
        {
            if (_boxHalfExtents == Vector3.zero) return 0;

            Quaternion rotation = Quaternion.Euler(0f, pose.Yaw, 0f);
            Vector3 center = new Vector3(pose.Position.X, _body.position.y, pose.Position.Z) + rotation * _boxCenter;
            return Physics.OverlapBoxNonAlloc(center, _boxHalfExtents, _overlaps, rotation, _blockingMask,
                                              QueryTriggerInteraction.Ignore);
        }

        // Obstacle box = the model's mesh bounds in cart space, shrunk by a skin and lifted off the floor.
        private void CacheObstacleBox()
        {
            _boxHalfExtents = Vector3.zero;
            if (_footprint == null || _footprint.sharedMesh == null) return;

            Bounds bounds = _footprint.sharedMesh.bounds;
            Vector3 min = Vector3.positiveInfinity;
            Vector3 max = Vector3.negativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                var sign = new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f);
                Vector3 corner = _footprint.transform.TransformPoint(bounds.center + Vector3.Scale(bounds.extents, sign));
                Vector3 local = transform.InverseTransformPoint(corner);
                min = Vector3.Min(min, local);
                max = Vector3.Max(max, local);
            }

            min = new Vector3(min.x + Skin, Mathf.Max(min.y, _floorClearance), min.z + Skin);
            max -= new Vector3(Skin, Skin, Skin);
            _boxCenter = (min + max) * 0.5f;
            _boxHalfExtents = Vector3.Max(Vector3.zero, (max - min) * 0.5f);
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) CacheObstacleBox();
            if (_boxHalfExtents == Vector3.zero) return;

            Gizmos.color = new Color(1f, 0.6f, 0f, 0.8f);
            Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.Euler(0f, transform.eulerAngles.y, 0f), Vector3.one);
            Gizmos.DrawWireCube(_boxCenter, _boxHalfExtents * 2f);
        }
    }
}
```

- [x] **Step 2: Compile in Unity**

MCP: `refresh_unity` (force, all, compile request, wait) → `read_console` types error, count 20.
Expected: no errors mentioning `PushCart`.

- [x] **Step 3: Checkpoint** — console clean. (Runtime behaviour is verified in Task 7 via the test-drive fields.)

---

### Task 3: `CartCargo` component

**Files:**
- Create: `Assets/Scripts/ImmersiveTraining/TrainingInteractions/CartCargo.cs`

**Interfaces:**
- Consumes: `SettleTimer` (Task 1); `SkuItem`; AutoHand `Grabbable.OnBeforeGrabEvent` (`HandGrabEvent(Hand, Grabbable)`), `Grabbable.IsHeld()`.
- Produces: `CartCargo` MonoBehaviour with serialized `_cart` (Rigidbody), `_settleTime`; public `int LockedCount`. Requires a Collider (the tote trigger) on the same GameObject.

- [x] **Step 1: Write the component**

Create `Assets/Scripts/ImmersiveTraining/TrainingInteractions/CartCargo.cs`:

```csharp
using System.Collections.Generic;
using Autohand;
using UnityEngine;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// Holds products in the pushable cart so they ride along with it. Attach to the tote: it shares the
    /// tote's trigger Collider with the <see cref="ToteCounter"/>s and <see cref="MisPickRecovery"/>.
    ///
    /// A <see cref="SkuItem"/> that is inside the trigger, not held and still relative to the cart for the
    /// settle time is locked: a FixedJoint ties it to the cart's kinematic body, so it moves with the cart
    /// in the same physics step and the tote counts never see it leave. The item stays a dynamic body and
    /// the joint keeps collision on, so nothing about its physics pairs (including its contact with the
    /// tote trigger) changes. Grabbing it removes the joint before the hand closes; dropped back in, it
    /// locks again once it settles.
    ///
    /// Products must not be children of the cart: a dynamic body under a moving parent gets dragged by the
    /// hierarchy as well as by physics.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CartCargo : MonoBehaviour
    {
        [Tooltip("The cart's kinematic Rigidbody (on the cart root). Settled items are jointed to it.")]
        [SerializeField] private Rigidbody _cart;

        [Tooltip("Seconds an item must rest in the cart before it locks.")]
        [SerializeField] private float _settleTime = 0.3f;

        private sealed class Item
        {
            public Grabbable Grabbable;
            public Rigidbody Body;
            public int Contacts;                  // colliders of this item currently inside the trigger
            public FixedJoint Joint;
            public RigidbodyInterpolation Interpolation;
            public Vector3 LastLocalPosition;
            public Quaternion LastLocalRotation = Quaternion.identity;
            public readonly SettleTimer Timer = new SettleTimer();

            public bool Locked => Joint != null;
        }

        private readonly Dictionary<Grabbable, Item> _items = new Dictionary<Grabbable, Item>();

        /// <summary>Number of items currently locked in the cart.</summary>
        public int LockedCount
        {
            get
            {
                int count = 0;
                foreach (Item item in _items.Values)
                    if (item.Locked) count++;
                return count;
            }
        }

        private void OnDestroy()
        {
            foreach (Item item in _items.Values)
                if (item.Grabbable != null) item.Grabbable.OnBeforeGrabEvent -= HandleBeforeGrab;
        }

        private void OnTriggerEnter(Collider other)
        {
            Item item = FindItem(other, true);
            if (item != null) item.Contacts++;
        }

        private void OnTriggerExit(Collider other)
        {
            Item item = FindItem(other, false);
            if (item == null || item.Contacts == 0) return;

            item.Contacts--;
            if (item.Contacts == 0 && item.Locked)
                Debug.LogWarning($"#{GetType().Name}# {item.Body.name} left the tote trigger while locked to the cart.", this);
        }

        private void FixedUpdate()
        {
            if (_cart == null) return;

            Quaternion toCart = Quaternion.Inverse(_cart.rotation);
            foreach (Item item in _items.Values)
            {
                if (item.Locked || item.Contacts == 0 || item.Body == null || item.Grabbable == null) continue;

                // Motion relative to the cart, so items dropped into a moving cart still settle.
                Vector3 localPosition = toCart * (item.Body.position - _cart.position);
                Quaternion localRotation = toCart * item.Body.rotation;
                float moved = Vector3.Distance(localPosition, item.LastLocalPosition);
                float turned = Quaternion.Angle(localRotation, item.LastLocalRotation);
                item.LastLocalPosition = localPosition;
                item.LastLocalRotation = localRotation;

                if (item.Timer.Tick(item.Grabbable.IsHeld(), moved, turned, Time.fixedDeltaTime))
                    Lock(item);
            }
        }

        private Item FindItem(Collider other, bool create)
        {
            SkuItem sku = other.GetComponentInParent<SkuItem>();
            if (sku == null) return null;
            Grabbable grabbable = sku.GetComponent<Grabbable>();
            if (grabbable == null) return null;

            if (_items.TryGetValue(grabbable, out Item item)) return item;
            if (!create) return null;

            Rigidbody body = sku.GetComponent<Rigidbody>();
            if (body == null) return null;

            item = new Item { Grabbable = grabbable, Body = body };
            item.Timer.SettleTime = _settleTime;
            _items.Add(grabbable, item);
            grabbable.OnBeforeGrabEvent += HandleBeforeGrab;
            return item;
        }

        private void Lock(Item item)
        {
            item.Interpolation = item.Body.interpolation;
            item.Body.interpolation = RigidbodyInterpolation.Interpolate;   // render as smoothly as the cart

            item.Joint = item.Body.gameObject.AddComponent<FixedJoint>();
            item.Joint.connectedBody = _cart;
            item.Joint.enableCollision = true;   // keep every physics pair exactly as it was
        }

        // Fires before the hand moves in or joints the item, so the hand never fights the cart joint.
        private void HandleBeforeGrab(Hand hand, Grabbable grabbable)
        {
            if (!_items.TryGetValue(grabbable, out Item item) || !item.Locked) return;

            Destroy(item.Joint);
            item.Joint = null;
            item.Body.interpolation = item.Interpolation;
            item.Timer.Reset();
        }
    }
}
```

- [x] **Step 2: Compile in Unity**

MCP: `refresh_unity` (force, all, compile request, wait) → `read_console` types error.
Expected: no errors mentioning `CartCargo`.

- [x] **Step 3: Checkpoint** — console clean.

---

### Task 4: `CartDropOffZone` component

**Files:**
- Create: `Assets/Scripts/ImmersiveTraining/TrainingInteractions/CartDropOffZone.cs`

**Interfaces:**
- Consumes: `DropOffArea`, `Planar` (Task 1); `MultiCounterGate.SetSourceMet(Object source, bool met)`.
- Produces: `CartDropOffZone` MonoBehaviour with serialized `_cart` (Transform), `_gate` (MultiCounterGate), `_size` (Vector2, default 1.6 × 1.3), `_outlineMaterial` (Material), `_outlineColor` (default yellow), `_outlineWidth` (0.05), `_outlineHeight` (0.02); public `bool CartInside`. Requires a LineRenderer.

- [x] **Step 1: Write the component**

Create `Assets/Scripts/ImmersiveTraining/TrainingInteractions/CartDropOffZone.cs`:

```csharp
using UnityEngine;
using UnityEngine.Rendering;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// The cart drop-off spot (M1_06). Tells a <see cref="MultiCounterGate"/>, live, whether the cart's
    /// centre is inside this rectangle on the floor, so the multi-line order only completes once the cart
    /// is parked back here (a trainee who never moved the cart is already parked). Draws its own flat
    /// outline with the LineRenderer on this GameObject, so the floor marking always matches the zone.
    /// The rectangle is centred on this transform, turned with its yaw and sized by <see cref="_size"/>.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class CartDropOffZone : MonoBehaviour
    {
        [Tooltip("The cart root; its position is the cart's centre.")]
        [SerializeField] private Transform _cart;

        [Tooltip("Gate that needs the cart parked here (M1_MultiLineGate).")]
        [SerializeField] private MultiCounterGate _gate;

        [Tooltip("Zone size in metres along its own X and Z.")]
        [SerializeField] private Vector2 _size = new Vector2(1.6f, 1.3f);

        [Header("Outline")]
        [SerializeField] private Material _outlineMaterial;
        [SerializeField] private Color _outlineColor = new Color(1f, 0.85f, 0f, 1f);
        [SerializeField] private float _outlineWidth = 0.05f;

        [Tooltip("Height of the outline above this transform, so it doesn't z-fight with the floor.")]
        [SerializeField] private float _outlineHeight = 0.02f;

        private bool _reported;
        private bool _inside;

        /// <summary>True while the cart's centre is inside the zone.</summary>
        public bool CartInside => _inside;

        private void Awake() => DrawOutline();

        private void OnValidate() => DrawOutline();

        private void OnEnable() => _reported = false;

        private void FixedUpdate()
        {
            if (_cart == null) return;

            Vector3 centre = transform.position;
            Vector3 cart = _cart.position;
            bool inside = DropOffArea.Contains(new Planar(centre.x, centre.z), transform.eulerAngles.y,
                                               new Planar(Mathf.Abs(_size.x) * 0.5f, Mathf.Abs(_size.y) * 0.5f),
                                               new Planar(cart.x, cart.z));
            if (_reported && inside == _inside) return;

            _inside = inside;
            _reported = true;
            if (_gate != null) _gate.SetSourceMet(this, inside);
        }

        private void DrawOutline()
        {
            var line = GetComponent<LineRenderer>();
            if (line == null) return;

            float x = Mathf.Abs(_size.x) * 0.5f;
            float z = Mathf.Abs(_size.y) * 0.5f;
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 4;
            line.SetPosition(0, new Vector3(-x, _outlineHeight, -z));
            line.SetPosition(1, new Vector3(-x, _outlineHeight, z));
            line.SetPosition(2, new Vector3(x, _outlineHeight, z));
            line.SetPosition(3, new Vector3(x, _outlineHeight, -z));
            line.startWidth = _outlineWidth;
            line.endWidth = _outlineWidth;
            line.startColor = _outlineColor;
            line.endColor = _outlineColor;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            if (_outlineMaterial != null) line.sharedMaterial = _outlineMaterial;
        }
    }
}
```

- [x] **Step 2: Compile in Unity**

MCP: `refresh_unity` (force, all, compile request, wait) → `read_console` types error.
Expected: no errors mentioning `CartDropOffZone`.

- [x] **Step 3: Checkpoint** — console clean.

---

### Task 5: Scene — restructure the cart and wire pushing + cargo

**Files:**
- Modify: `Assets/Scenes/BasicScene.unity` (via MCP)

**Interfaces:**
- Consumes: `PushCart`, `CartCargo` (Tasks 2–3).
- Produces (scene objects later tasks use by name/path): `Utility_cart` (root; Rigidbody + PushCart), `Utility_cart/Model`, `Utility_cart/Handles` (Rigidbody + Grabbable) with `Handle_A`/`Handle_B`, `Utility_cart/LowerWall_Front`/`LowerWall_Back`, `Utility_cart/M1_Tote` (+ CartCargo), `M1_MisPick_Wrong` at the scene root.

- [x] **Step 1: Record the "before" world poses**

MCP: `find_gameobjects` for `Utility_cart`, `M1_Tote`, `M1_MisPick_Wrong`; read `mcpforunity://scene/gameobject/{id}` for each.
Expected (from the spec): `Utility_cart` world (1.889, 0.47, 3.232) rot (270, 0, 0) scale 55.72; `M1_Tote` world (1.889, ~0, 3.232) rot (0, 270, 0) lossy scale 1; `M1_MisPick_Wrong` world ≈ (2.221, 0.351, 3.406). Keep these to compare after re-parenting.

- [x] **Step 2: Rename the mesh object and create the upright root**

MCP:
1. `manage_gameobject` modify, target = the `Utility_cart` id, `search_method: by_id`, `new_name: "Model"`.
2. `manage_gameobject` create, `name: "Utility_cart"`, `position: [1.89, 0, 3.21]` (scene root, identity rotation/scale).
3. `manage_gameobject` modify, target = the `Model` id, `parent: "Utility_cart"`.
4. `manage_gameobject` modify, target = `M1_Tote` (by name), `parent: "Utility_cart"`.
5. `manage_gameobject` modify, target = `M1_MisPick_Wrong` (by name), `parent: ""` (scene root).

Verify: read the three objects again — world positions/rotations unchanged from Step 1; `M1_Tote` local scale ≈ (1, 1, 1) and parent = `Utility_cart`; `M1_MisPick_Wrong` parent null; `Model` parent = `Utility_cart`.

- [x] **Step 3: Cart body + PushCart on the root**

MCP on `Utility_cart`:
1. `manage_components` add `Rigidbody`; set_property `{ "isKinematic": true, "useGravity": false, "interpolation": 1, "collisionDetectionMode": 3 }`.
2. `manage_components` add `PushCart`.

- [x] **Step 4: Handles body and the two handle bars**

MCP:
1. `manage_gameobject` create `name: "Handles"`, `parent: "Utility_cart"`, `position: [0, 0, 0]`.
2. On `Handles`: add `Rigidbody`, set `{ "isKinematic": true, "useGravity": false }`; add `Grabbable` (AutoHand), set `{ "parentOnGrab": false, "heldNoFriction": false, "singleHandOnly": false, "maintainGrabOffset": true, "throwPower": 0, "makeChildrenGrabbable": true, "instantGrab": false }` and `hightlightMaterial` = `Assets/Plugins/AutoHand/Examples/Materials/Highlight/Highlight.mat` (same as the products).
3. `manage_gameobject` create `name: "Handle_A"`, `primitive_type: "Cylinder"`, `parent: "Handles"`, `position: [-0.508, 0.941, -0.008]`, `rotation: [90, 0, 0]`, `scale: [0.035, 0.3, 0.035]`.
4. Same for `Handle_B` with `position: [0.509, 0.941, -0.008]`.
5. On each handle: `manage_components` set_property `MeshRenderer` `enabled: false` (invisible; AutoHand still copies the mesh for its highlight). Keep the primitive's CapsuleCollider (radius 0.5 × 0.035 = 1.75 cm, 0.6 m long along world Z).

- [x] **Step 5: Lower-tier side walls**

MCP:
1. create `name: "LowerWall_Front"`, `parent: "Utility_cart"`, `position: [0.001, 0.358, -0.304]`; add `BoxCollider`, set `size: [0.946, 0.244, 0.02]`.
2. create `name: "LowerWall_Back"`, `parent: "Utility_cart"`, `position: [0.001, 0.358, 0.318]`; add `BoxCollider`, set `size: [0.946, 0.244, 0.02]`.

- [x] **Step 6: Wire PushCart and add CartCargo**

MCP:
1. `PushCart` on `Utility_cart`: `_handles` → the `Handles` GameObject id (its Grabbable); `_head` → `Camera (head)` GameObject id; `_footprint` → the `Model` GameObject id (its MeshFilter).
2. On `M1_Tote`: add `CartCargo`; set `_cart` → the `Utility_cart` GameObject id (its Rigidbody).

Verify: read `mcpforunity://scene/gameobject/{Utility_cart id}/component/PushCart` and `{M1_Tote id}/component/CartCargo` — all four references non-null with the expected names.

- [x] **Step 7: Save and look**

MCP: `manage_scene save`; `read_console` errors (expect none new). Screenshot `manage_camera` screenshot, `view_target: "Utility_cart"`, `batch: "orbit"`, `orbit_angles: 4`, `orbit_elevations: [25]`, `orbit_distance: 2.2`, `include_image: true`.
Expected: the cart looks exactly as before (handles invisible), red bottle still in its bottom bin.

- [x] **Step 8: Checkpoint** — poses unchanged, references wired, scene saved.

---

### Task 6: Scene & data — drop-off, M1_06 gate, route overlay, challenge title

**Files:**
- Modify: `Assets/Scenes/BasicScene.unity` (via MCP)
- Modify: `Assets/SO/TutorialData/M1_06_Multi_Line_Order.asset:27`
- Modify: `Assets/ReferenceData/training_flow_module1.json:86`

**Interfaces:**
- Consumes: `CartDropOffZone` (Task 4); `Utility_cart`, `M1_MultiLineGate` (scene).

- [x] **Step 1: Drop-off zone**

MCP:
1. create `name: "Cart_DropOff"`, `position: [1.89, 0, 3.21]` (scene root).
2. add `CartDropOffZone` (its RequireComponent adds the LineRenderer).
3. set `_cart` → `Utility_cart` id; `_gate` → `M1_MultiLineGate` id; `_outlineMaterial` → `Assets/Materials/RouteLineMat.mat`.

Verify: read the component — `_cart`, `_gate`, `_outlineMaterial` set; LineRenderer has 4 positions, loop on.

- [x] **Step 2: M1_06 needs 4 sources**

MCP: `M1_MultiLineGate` → `MultiCounterGate` set_property `_requiredCount: 4`. Verify by reading it back.

- [x] **Step 3: Optimal route without the mid-route tote leg**

MCP:
1. `RouteOverlay` → `RouteOverlayController` set `_optimalWaypoints` to `[WP0_Start, WP1_A0203, WP3_B0102, WP4_B0204, WP5_C0301, WP6_ToteReturn]` (by instance id, each `{ "instanceID": id }` of the GameObject/Transform).
2. `manage_gameobject` modify `WP6_ToteReturn`: `position: [1.89, 0.03, 3.21]` (parent `OptimalRouteWaypoints` is at the origin, so local = world), `new_name: "WP6_DropOff"`.
3. `manage_gameobject` delete `WP2_Tote`.

Verify: read `RouteOverlayController` — 6 waypoints, last is `WP6_DropOff`, no null entries.

- [x] **Step 4: M1_06 challenge title**

Edit `Assets/SO/TutorialData/M1_06_Multi_Line_Order.asset` line 27:

```yaml
  - <ChallengeTitle>k__BackingField: Complete All 3 Picks and Return the Cart to the Drop-Off
```

Edit `Assets/ReferenceData/training_flow_module1.json` line 86:

```json
        "ChallengeTitle": "Complete All 3 Picks and Return the Cart to the Drop-Off",
```

MCP: `refresh_unity` (force, assets). Verify with `grep -n "ChallengeTitle" Assets/SO/TutorialData/M1_06_Multi_Line_Order.asset`.

- [x] **Step 5: Save and look**

MCP: `manage_scene save`; `read_console` errors (none). Screenshot from `view_position: [1.9, 2.4, 5.6]`, `view_rotation: [40, 180, 0]`, `include_image: true`.
Expected: the yellow outline is visible on the floor around the cart. (It is also drawn in edit mode via `OnValidate`.)

- [x] **Step 6: Checkpoint** — gate 4, route 6 waypoints, title updated, scene saved.

---

### Task 7: Play-mode verification, cleanup and docs

**Files:**
- Delete: `Assets/Editor/CartProbeTemp.cs` (+ `.meta`), this session's `Assets/Screenshots/screenshot-20260923-*.png` (+ `.meta`)
- Modify: `CLAUDE.md`
- Modify: memory `C:\Users\EJ-Shadow-mode\.claude\projects\C--Users-chris-UnityProjects-WarehouseVRTraining\memory\` (MCP workflow notes)

- [x] **Step 1: Enter Play mode**

Scene is saved (Task 6). MCP: `manage_editor play`; poll `mcpforunity://editor/state` until `is_playing` and ready. Wait ~2 s of play (`read_console` in between is fine).

- [x] **Step 2: Cargo and drop-off at rest**

MCP reads:
- `M1_MisPick_Wrong` components → a `FixedJoint` is present (connected body `Utility_cart`).
- `M1_Tote` → `CartCargo.LockedCount` = 1.
- `M1_MultiLineGate` → `MultiCounterGate.CurrentCount` = 1 (the drop-off source).
- `read_console` warnings/errors → no `#CartCargo#` warning, no exceptions.

- [x] **Step 3: Test drive out of the drop-off (items ride along)**

MCP: `PushCart` on `Utility_cart` set `_testDriveVelocity: {"x": 0, "y": 1}`. After ~1.5 s set it back to `{"x": 0, "y": 0}`.
Expected: `Utility_cart` z increased by up to ~1.5 m (or stopped at an obstacle); `M1_MisPick_Wrong` moved by the same offset and still has its joint; `CartCargo.LockedCount` = 1; `MultiCounterGate.CurrentCount` = 0 once the centre is > 0.65 m from z 3.21; no `#CartCargo#` warning. Screenshot the cart.

- [x] **Step 4: Test drive into an obstacle (blocking)**

MCP: set `_testDriveVelocity` toward the nearest static obstacle seen in the Step 3 screenshot (e.g. `{"x": 0, "y": 1}` again if the grey cage lies ahead, otherwise `{"x": 0, "y": -1}` back toward the black crate stack behind the start spot) for ~3 s, then zero it.
Expected: the cart stops at the obstacle (position stops changing while the drive is on) instead of passing through; screenshot shows no interpenetration; red item still locked.

- [x] **Step 5: Test turn**

MCP: `_testDriveTurnSpeed: 45` for ~2 s, then 0.
Expected: the cart yaw changed (or stayed if blocked) without errors; red item still in its bin (screenshot).

- [x] **Step 6: Stop Play mode**

MCP: `manage_editor stop`; confirm `is_playing` false and the scene reverted (cart back at (1.89, 0, 3.21)).

- [x] **Step 7: Remove temporary files**

Delete `Assets/Editor/CartProbeTemp.cs` and `Assets/Editor/CartProbeTemp.cs.meta`; delete only this session's screenshots — list `Assets/Screenshots/screenshot-20260923-*` first and delete those files and their `.meta`s. MCP `refresh_unity` (force, all, compile request); `read_console` errors → none.

- [x] **Step 8: Final test run**

Run: `dotnet run --project Tests/OrderPickingLogic`
Expected: `39 passed, 0 failed`.

- [x] **Step 9: Update CLAUDE.md**

- §3 table: add `PushCart`, `CartCargo`, `CartDropOffZone`, `CartLogic` rows; test count 23 → 39.
- §4: replace "Pickables are kinematic with gravity off and stay where released" with "Pickables are dynamic Rigidbodies with gravity"; add a **Pushable cart** subsection (hierarchy, handles, steering, blocking, cargo joints, test-drive fields, obstacle-box gizmo, tunables); update `M1_Tote` (now under `Utility_cart`, no crate model — the cart's four bins; trigger covers them) and `M1_MisPick_Wrong` (scene root) / `M1_MisPick_Correct` (floor near the drop-off) descriptions.
- §5: M1_06 chain gets the 4th source (`Cart_DropOff` → `CartDropOffZone`), `_requiredCount` 4, new challenge title; M1_08 optimal route waypoints (6, ends at `WP6_DropOff`).
- §6: in-headset checklist items — one/two-hand pushing, snap turn while holding, blocking and sliding at racks, grip breaking when snagged, items riding and unlocking on grab, M1_06 completing only once parked in the outline, handle highlight; tuning items — speed/turn caps, handle collider fit, drop-off size, settle time.
- §7: `execute_code` fails here (no Roslyn; CodeDom path too long) → use a temporary `Assets/Editor` menu script + `execute_menu_item`; two editors are connected, call `set_active_instance WarehouseVRTraining@238c2776` first.

- [x] **Step 10: Checkpoint** — Play-mode results recorded, temp files gone, docs updated, tests 39/39, console clean.
