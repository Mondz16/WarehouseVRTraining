# Pushable Utility Cart — Design

**Date:** 2026-09-23 · **Module:** M1 Order Picking · **Scene:** `Assets/Scenes/BasicScene.unity`

## Goal

The trainee can push the `Utility_cart` (which now carries `M1_Tote`) around the pick area while
picking, so items travel with them instead of being walked back to a fixed tote.

## Decisions (agreed 2026-09-23)

| Question | Decision |
|---|---|
| How is the cart pushed? | Grab either end handle with one or both hands; the cart follows the hands, stays upright on the floor, turns with the trainee, stops at racks and walls. Script-driven. |
| Items dropped in the cart? | Lock to the cart once they settle; grabbing unlocks them. |
| End of the multi-line order (M1_06)? | The cart must be back at a drop-off spot (its start position) for M1_06 to complete. M1_07–M1_10 panels and the green item stay where they are. |
| Implementation | **Kinematic cart moved by script** (not a dynamic, velocity-driven body). |

## Current state (before this change)

- `Utility_cart` (root): Meshy mesh (`Assets/Models/Utility_Cart/…fbx`, rotation 270° X, scale 55.72) with
  four built-in blue bins, 7 box colliders, no Rigidbody. World centre ≈ (1.890, 0.47, 3.231); footprint
  ≈ 1.06 (x) × 0.79 (z) m, height ≈ 0.95 m. Handle frames at both short ends (x ≈ 1.39 and x ≈ 2.39).
- `M1_Tote` is its child: trigger (world size 0.96 × 0.64 × 0.62, y 0.24–0.88) covering all four bins,
  4 `ToteCounter`s, `MisPickRecovery`; children `M1_MultiLineGate` and `M1_MisPick_Wrong` (red, SKU-5099,
  resting in a bottom bin — its AutoHand `originalParent` is therefore `M1_Tote`).
- Physics settings: Contact Pairs Mode = Default, Auto Sync Transforms = on.
- `M1_MisPick_Correct` (green, SKU-5005) is a root object on the floor at (1.134, 0.092, 2.378).
- All pickables are **dynamic Rigidbodies with gravity** (CLAUDE.md still says kinematic — stale).
- AutoHand restores a released item's parent to `Grabbable.originalParent` (public, settable). The hand only
  treats a collider as grabbable if a `Grabbable`/`GrabbableChild` sits on that collider's own GameObject.
  A `Grabbable` recursively claims every collider below it (layer → `Grabbable`, temporarily `Grabbing`
  during a grab; physics-material swap when `heldNoFriction`).
- Physics runs at 50 Hz (`Fixed Timestep 0.02`); the player moves with smooth locomotion (max 2.3 m/s)
  and snap turn (30°).

## Scene structure

World pose of the cart is unchanged; only the hierarchy is reorganised.

```
Utility_cart          NEW upright root on the floor at the cart centre (1.890, 0, 3.210), identity rot/scale
│                     kinematic Rigidbody (Interpolate, ContinuousSpeculative, no gravity) + PushCart
├─ Model              the current mesh object, renamed; keeps its 7 box colliders (NOT grabbable)
├─ LowerWall_Front    NEW BoxCollider on the lower tier's long side (items can't roll out before they lock)
├─ LowerWall_Back     NEW BoxCollider on the other long side
├─ Handles            kinematic Rigidbody + AutoHand Grabbable (see settings below)
│  ├─ Handle_A        Cylinder primitive on one end frame's top bar: CapsuleCollider + DISABLED MeshRenderer
│  └─ Handle_B        same on the other end (the disabled mesh gives AutoHand something to highlight)
└─ M1_Tote            unchanged trigger + ToteCounters + MisPickRecovery, + NEW CartCargo
   └─ M1_MultiLineGate
Cart_DropOff          NEW root on the floor at the cart's start centre: CartDropOffZone + LineRenderer outline
M1_MisPick_Wrong      MOVED from under M1_Tote to the scene root (world pose kept)
```

Measured from the mesh (probe, 2026-09-23): handle bars are ~3 cm tubes centred at y 0.941, x 1.382 (A) and
2.399 (B), straight between z 2.885 and 3.518; the lower bins' outer walls are at z 2.896 / 3.538 and rise
from the bottom shelf (y 0.236) to y 0.48; the end frames' inner faces are at x 1.418 / 2.364.

The `Grabbable` lives on `Handles` (not the root) so AutoHand's layer / material handling only touches the
two handle colliders — never the tote trigger (a layer flip on the trigger could fire false trigger exits,
e.g. a false "mis-pick removed").

No product may be a child of the cart: a dynamic body under a moving parent gets dragged by the hierarchy as
well as by physics. That is why `M1_MisPick_Wrong` moves to the scene root — it also clears its AutoHand
`originalParent`, so a released red item is never put back under the cart.

**Handles Grabbable settings:** `parentOnGrab = false` (must not re-parent the handles to the hand),
`heldNoFriction = false`, `singleHandOnly = false`, `maintainGrabOffset = true` (the cart keeps the hand
where it gripped, so the hand should not pull back toward the controller), `throwPower = 0`, default
`jointBreakForce` (a snagged cart makes the grip break naturally), `makeChildrenGrabbable = true`
(adds `GrabbableChild` to the two handle colliders).

## Components

All new scripts in `Assets/Scripts/ImmersiveTraining/TrainingInteractions/`, namespace
`ImmersiveTraining.TrainingInteractions`, following the existing bridge style (direct serialized refs, no
UnityEvent wiring needed — MCP cannot wire UnityEvents).

### `CartLogic.cs` — plain C# (no UnityEngine), unit-tested

Linked into `Tests/OrderPickingLogic` alongside `ChallengeBridgeLogic.cs`.

- `CartPose` — planar pose `(X, Z, Yaw)`; yaw in degrees about +Y (Unity convention: yaw 0 faces +Z,
  90 faces +X; rotating local `(x, z)` by yaw θ gives `(x·cosθ + z·sinθ, −x·sinθ + z·cosθ)`).
- `CartSteering.OneHand(...)` — target pose for one holding hand (see Steering).
- `CartSteering.TwoHands(...)` — target pose for two holding hands.
- `CartSteering.Step(current, target, maxMove, maxTurnDeg)` — moves at most `maxMove` metres and turns
  at most `maxTurnDeg` (shortest way) toward the target.
- `SettleTimer` — per item: fed each tick with *held?* and the item's motion relative to the cart; reports
  when it has rested long enough to lock.
- `DropOffArea.Contains(centre, halfSize, yaw, point)` — point-in-rotated-rectangle on the floor plane.

### `PushCart` (on the `Utility_cart` root)

Refs: `_handles` (Grabbable on `Handles`), `_head` (the player's `Camera (head)`), `_footprint`
(`Model`'s MeshFilter, used once at Awake to size the blocking box). Tunables: `_maxSpeed` 2.5 m/s,
`_maxTurnSpeed` 180°/s, `_floorClearance` 0.08 m (raised from 0.05 during implementation: a 6 cm floor plate at the
B pick spot, `PickArea_B/GameObject`, would otherwise block the cart), `_blockingMask` (default everything).

- Subscribes to `_handles.OnGrabEvent` / `OnReleaseEvent`; keeps the list of holding hands and uses each
  hand's controller target (`Hand.follow`), not the physics hand.
- **Re-anchoring:** whenever the set of holding hands changes, every holding hand's anchor is recorded
  from the *current* cart pose (`anchor = inverse-rotate(controller − cartPosition)`, planar), together with
  the one-hand reference angles. This keeps the cart exactly still at every grab/release transition.
- Each `FixedUpdate` while held: target pose → `CartSteering.Step` (speed/turn caps) → blocking check →
  `MovePosition`/`MoveRotation` on the root body **and** on the `Handles` body (same pose + its captured
  local offset, so the handle the hand is jointed to never lags the cart).
- Root Y never changes; the cart only slides and turns on the floor plane.

### `CartCargo` (on `M1_Tote`, shares its trigger)

Refs: `_cart` (the root's Rigidbody). Tunables: settle time 0.3 s (move tolerance 3 mm/tick and rotation
tolerance 1°/tick are `SettleTimer` defaults).

- Tracks `SkuItem`s whose colliders are inside the trigger (per-item overlap counting, enter/exit).
- **Lock** — an item that is inside, not held or being grabbed (`IsHeld()` / `BeingGrabbed()` — so a slow reach
  can't re-lock it just before the hand joints it), and has moved < 3 mm and rotated < 1° *relative to the cart's
  physics pose* per tick for 0.3 s gets a `FixedJoint` to the cart's kinematic body, with **collision left
  enabled**, and its interpolation set to Interpolate (restored on unlock) so it renders as smoothly as the
  cart. The item stays a dynamic body and keeps its parent.
- **Unlock** — on the item's `Grabbable.OnBeforeGrabEvent` (fires before the hand moves in or joints): the
  joint is destroyed, interpolation restored, settle timer reset. Released inside the cart it settles and
  locks again; released outside it stays where it lands.
- Only `SkuItem`s lock — the scanner and the wrist device are unaffected.
- Why a joint and not "kinematic + parented": Unity's docs disagree on whether trigger events between two
  kinematic bodies are reported under the project's Default Contact Pairs mode, and a disabled joint
  collision filters the pair inside PhysX. A jointed dynamic item with collision on changes nothing about
  its physics pairs, so its contact with the tote trigger (dynamic vs. kinematic-attached trigger) is always
  reported, and the joint moves it in the same physics step as the cart — the counts never see an exit.
- If a locked item ever leaves the trigger anyway, `CartCargo` logs a warning (diagnostic only).
- *(Review follow-ups)* An item only locks if its centre of mass is really inside the trigger (Unity sends no
  trigger exit for an item deactivated inside it, so contact counts can be stale). The joint's connected anchor
  is set from the two Rigidbody poses, not auto-configured from Transforms, so a lock in a moving cart doesn't
  bake in the interpolated cart Transform's lag.

### `CartDropOffZone` (on `Cart_DropOff`)

Refs: `_cart` (the root transform), `_gate` (`M1_MultiLineGate`). Tunable: `_size` 1.6 × 1.3 m (x × z in
the zone's local space). Draws its own yellow outline (LineRenderer, loop, local space, `RouteLineMat`, default
View alignment like the route lines — TransformZ with points in local XZ would give two sides zero width) from
`_size` so the marking always matches the zone.

- Reports `_gate.SetSourceMet(this, inside)` whenever "cart centre inside the rectangle" changes, and once
  on enable. Position only — the cart doesn't have to be released or aligned. When disabled it reports "not
  inside"; a missing cart or gate reference logs a warning at Awake (it would otherwise soft-lock M1_06).

## Behaviour

### Steering

Positions are planar (x, z) controller targets; *h* is the head position.

- **One hand** (anchor *a*, grab-time cart yaw θ₀, grab-time head→hand yaw φ₀): φ = yaw of (hand − h),
  updated only while |hand − h| ≥ 0.2 m. Target yaw θ = θ₀ + Δ(φ₀, φ); target position = hand − rotate(θ, a).
  While the reach is shorter than 0.2 m steering pauses, and when it is usable again θ₀/φ₀ are re-taken from that
  moment (a hand that passes close to the head and comes out on the other side doesn't swing the cart 180°).
  *h* is a neck point `_neckOffset` (0.1 m) behind the head camera along its flattened forward: the camera sits
  ahead of the neck, so turning the head would otherwise swing it sideways and steer. Snap/smooth turns and
  sideways hand moves steer; looking around does not. No head reference → translation only.
- **Two hands** (anchors *a₁*, *a₂*): θ = yaw(hand₂ − hand₁) − yaw(a₂ − a₁); position = midpoint(hands) −
  rotate(θ, midpoint(anchors)). If either span is < 0.1 m the heading is kept (the midpoint still moves it).
- Caps: 2.5 m/s, 180°/s (tracking glitches cannot fling the cart; a 30° snap turn takes ~0.17 s).

### Blocking

- Blocking box = `Model` mesh bounds in root space, shrunk by 1 cm, bottom raised to `_floorClearance`
  above the floor.
- A pose is free if `Physics.OverlapBox` (triggers ignored) finds no *blocking* collider: not part of the
  cart hierarchy, not part of an AutoHand hand (e.g. the wrist device on the left hand, which may be
  kinematic), and either static (no Rigidbody) or kinematic. Dynamic bodies (loose items, hands) never
  block — the kinematic cart pushes them aside.
- `_blockingMask` leaves out the UI, Grabbing, Grabbable, Hand and HandPlayer layers (set by `Reset()` for new
  components; `m_Bits` 4294966303 in the scene): nothing on them can block, and the two hands' ~60 finger colliders
  would otherwise fill the 64-slot query buffer. A full buffer counts as blocked (with a one-time warning) rather
  than risk dropping a real obstacle.
- **Editor testing:** `PushCart._testDriveVelocity` (m/s, world X/Z) and `_testDriveTurnSpeed` (°/s) drive the
  unheld cart through the same caps and checks, so pushing, blocking and cargo can be exercised in Play mode
  without a headset. Both default to zero and only act in the editor (`Application.isEditor`; the fields stay
  unconditional because editor-only serialized fields break the player's serialization layout). A selected
  cart draws its obstacle box as a gizmo.
- Colliders the cart already overlaps when a push starts are ignored until the cart has moved clear of them (it
  can't get stuck, and can't later be driven through them either).
- Candidates in order: target pose → target position at current yaw → x-only move → z-only move → stay. A
  non-finite target (bad tracking sample) is skipped.
- When the cart can't follow, the hand stretches and AutoHand releases the grip (joint break or
  `maxFollowDistance`), like a real snag.
- **The player:** while the cart is held it ignores collisions with the player's body capsule and head collider.
  AutoHandPlayer moves its tracking space with its physics body, so a held cart shoving the player would drag
  the controllers — and so the cart — after the player (a sliding feedback loop). After release, collisions are
  restored once the cart and the player no longer overlap (bounds test), so there's no pop.
- A parked cart is solid: the player can't walk through it and hands can't shove it.

## Tutorial integration

- **M1_06:** `M1_MultiLineGate._requiredCount` 3 → 4; `CartDropOffZone` is the 4th live source. M1_06
  completes when all 3 lines are in the cart **and** the cart is parked in the drop-off outline. A trainee
  who never moved the cart is already parked. Arm-aware behaviour is unchanged (the gate re-checks on arm).
- **M1_06 challenge title** (on-panel checklist; not narrated): "Complete All 3 Picks in the Multi-Line
  Order" → "Complete All 3 Picks and Return the Cart to the Drop-Off", in
  `Assets/SO/TutorialData/M1_06_Multi_Line_Order.asset` and `Assets/ReferenceData/training_flow_module1.json`. Narrated descriptions are left as-is so they keep
  matching the voice-over (M1_05's "virtual tote on your left side" narration is already stale — owner's call).
- **M1_08 optimal route:** Start → A-02-03 → B-01-02 → B-02-04 → C-03-01 → drop-off. Delete `WP2_Tote`
  (and remove it from `RouteOverlayController._optimalWaypoints`); move `WP6_ToteReturn` to the drop-off
  centre and rename it `WP6_DropOff`.

## Out of scope (YAGNI)

Resetting the cart or items on module restart (items aren't reset today either); forcing the trainee to push
(carrying items back still works); wheel physics or wheel animation; locking the scanner or wrist device;
voice-over changes; route scoring or a cart-path line in the overlay.

## Testing & verification

1. **Unit tests first** (`dotnet run --project Tests/OrderPickingLogic`): one-hand steering (translation,
   turn with head→hand direction, head too close keeps yaw), two-hand steering (handlebar turn, midpoint,
   degenerate span falls back), `Step` caps (distance, shortest-way turn, wrap at ±180°), `SettleTimer`
   (locks after 0.3 s of rest, held or moving resets it), `DropOffArea` (inside/outside, rotated zone).
   The existing 23 tests must still pass.
2. **Editor (MCP):** compiles with 0 errors; in Play mode the red item locks (gets its joint) shortly after
   start, the drop-off reports "parked" (gate count 1), a test drive moves the cart with the red item riding
   along and the gate count dropping to 0 once it leaves the outline, a test drive into an obstacle stops
   the cart, and no `CartCargo` warning appears; handle colliders sit on the mesh bars (screenshots). Scene
   saved before entering Play mode (Meta XR Simulator has crashed the editor on leaving Play mode before).
3. **In-headset (added to the CLAUDE.md checklist):** one- and two-hand pushing and steering, snap turn
   while holding, blocking and sliding at racks/pallets, grip breaking when snagged, items riding along and
   unlocking when grabbed, M1_06 completing only once the cart is back in the outline.

## Files

- New: `PushCart.cs`, `CartCargo.cs`, `CartDropOffZone.cs`, `CartLogic.cs` (TrainingInteractions).
- Modified: `Tests/OrderPickingLogic/OrderPickingLogic.Tests.csproj` (link `CartLogic.cs`),
  `Tests/OrderPickingLogic/Program.cs` (new tests), `BasicScene.unity`, `M1_06_Multi_Line_Order.asset`,
  `training_flow_module1.json`, `CLAUDE.md` (cart section, M1_06 chain, stale kinematic note, checklist).
