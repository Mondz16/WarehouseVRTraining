# WarehouseVRTraining — Project State & Task Tracker

> Living doc for tracking build progress on the VR warehouse-training app.
> Last updated: 2026-10-06. Keep the **Task Tracker** and **Wiring Status** tables current as work lands.

---

## 1. Project Overview

Unity VR warehouse-worker training app. Modules are guided by the **Valari tutorial
framework** — each step shows a modal panel; some steps have *challenges* that the
trainee completes by physically interacting with the scene, which advances the flow.

- **Engine:** Unity `6000.3.12f1`, URP.
- **XR:** Meta Quest / OpenXR, **AutoHand** for VR grabbing, new Input System, TextMeshPro.
- **Main scene:** `Assets/Scenes/BasicScene.unity`.
- **Modules built:** Module 1 — **Order Picking (M1)**; Module 2 — **Packing and Dispatch (M2)** (2026-10-06, §5b).
- **Canonical flows:** `Assets/ReferenceData/training_flow_module1.json` (10 steps, M1_01–M1_10),
  `training_flow_module2.json` (11 steps, M2_01–M2_11).

---

## 2. How the Tutorial / Challenge Framework Works

Framework lives in `Assets/__ValariPackageExportStaging/` (namespaces `Valari.*`).

- `TutorialData` (ScriptableObject) — one per step; holds title, description, VO, challenge list.
- `TutorialModalUI` — per-panel MonoBehaviour. Key fields: `_challengeTutorialTriggerList`
  (`List<ChallengeTutorialTrigger>`), `_hasChallenges`, `_challengeHolder`,
  `_challengeToggle`, `_tutorialData`.
- `ChallengeTutorialTrigger` (global namespace MonoBehaviour) — fields `_triggerName`,
  `_tutorialType`, `_enabled`; public method `OnTriggerComplete()`.

**Trigger flow:**
```
scene interaction → ChallengeTutorialTrigger.OnTriggerComplete()
                  → TutorialModalUI.OnUpdateChallengesState(name)   (matches by trigger name)
                  → marks that challenge complete → advances step
```

See memory: `valari-challenge-framework.md`.

---

## 3. Interaction Scripts

Location: `Assets/Scripts/ImmersiveTraining/TrainingInteractions/` (all compile clean).

| Script | Purpose |
|--------|---------|
| `SkuItem` | Tags a pickable with `_sku` + `_description` (props `Sku`, `Description`). |
| `ToteCounter` | Trigger volume; counts matching-SKU items inside. Optional exclusive mode (`_requireNoOtherItems` + `_ignoredSkus`, used by M1_05). Reports its live met/unmet state to a `MultiCounterGate`. |
| `MultiCounterGate` | Aggregates N sources that are met right now (`SetSourceMet`, from ToteCounters) or `NotifyComplete()` calls (multi-line orders, M1_06). |
| `LocationZoneTrigger` | Trigger volume; fires once when a `TriggerIdentity` (default `"Player"`) enters (navigation, M1_03). |
| `TriggerIdentity` | Tags an object with an `Id` string (player rig carries `Id="Player"`). |
| `ScannerActivator` | Bridges AutoHand `Grabbable.OnSqueezeEvent` → `ScannerTool.Scan()` (squeeze held scanner to scan, M1_04). |
| `GrabChallengeTrigger` | Bridges AutoHand `Grabbable.OnGrabEvent` → `ChallengeTutorialTrigger.OnTriggerComplete()` (grab-to-open, M1_02; reusable). |
| `MisPickRecovery` | On the tote; fires a remove-trigger when the wrong-SKU item exits, then a replace-trigger when the correct-SKU item enters (ordered, M1_07). |
| `ScannerTool` | Right-hand scanner; `Scan()` verifies SKU. |
| `TriggerEventByID` / `TriggerIdentity` | ID-based trigger helpers. |
| `ChallengeBridgeLogic` | Plain C# (no UnityEngine) decision logic behind the bridges: `ArmedLatch`, `ToteTally`, `MultiSourceGate`, `MisPickTracker`. Unit tests outside Unity: `dotnet run --project Tests/OrderPickingLogic` (45 tests, incl. `CartLogic`). |
| `PushCart` | On the `Utility_cart` root (kinematic Rigidbody). Follows the hands holding the `Handles` Grabbable (one hand: steers with the head→hand direction; two hands: handlebar), capped 2.5 m/s / 180°/s, `OverlapBox`-checked against static/kinematic colliders (stop or slide on one axis). Test-drive fields for headset-less Play mode (§4 Pushable cart). |
| `CartCargo` | On `M1_Tote` (shares its trigger). Products that settle in the cart (0.3 s still relative to it) get a `FixedJoint` to the cart body so they ride along; the joint is removed on AutoHand `OnBeforeGrabEvent`. `LockedCount` for inspection. |
| `CartDropOffZone` | On `Cart_DropOff`. Reports "cart centre inside the rectangle" live to `M1_MultiLineGate` (4th source) and draws the yellow floor outline. |
| `CartLogic` | Plain C# behind the cart: `Planar`, `Angles`, `CartPose`, `CartSteering` (grip/target/step/fallbacks), `SettleTimer`, `DropOffArea`. Unit-tested with the runner above. |

**Module 2 scripts** (`TrainingInteractions/Packing/`, all compile clean; details in §5b):

| Script | Purpose |
|--------|---------|
| `PackingLogic` | Plain C#: `CartonFit` (minimum-fit rule, 5 cm clearance), `VoidFillState` (base → items → top → full), `SeamCoverage`, `SegmentMath`, `GazeDwell`, `Orientation.IsFaceUp`, `BoxRegion`. Unit tests: `dotnet run --project Tests/PackingDispatchLogic` (31 tests). |
| `PackingCarton` | The stateful carton: polls released pillows/products inside it (only while locked on the spot / QC table and open), auto-closes the flaps when full, H-pattern seams swept by a `SeamTool` (tape / QC cutter), label on the top face, `LockAt` / `Unlock` / `PutOnConveyor`, `Changed` event. Pre-packed mode for the QC box. |
| `PackingStation` | On `M2_PackingStation/Bench`: orders, carton rack (restocks a size when its carton is used), packing spot (judges with `CartonFit`, locks the right carton, returns a wrong one to the rack after 1.5 s), order panel text, M2_03/04/05/06/08 bridges, starts order 2 when the conveyor dispatches order 1, resets everything M2 on module (re)start. |
| `PackableItem` | Product identity + home pose; packed products become inert children of the carton. |
| `VoidFillDispenser` / `VoidFillPiece` | Pool of 12 air pillows, 3 waiting in slots; absorbed or refused pillows go back to the pool. |
| `SeamTool` | Tape gun / box cutter head; works only while held (`ForceActive` for tests). |
| `ShippingLabel` | Scanned (via `IScanListener`) → verified readout + M2_06 verify bridge; let go on a sealed carton's top face → applied. |
| `IScanListener` | `ScannerTool.Scan` now notifies listeners on the scanned item (additive; M1 unaffected). |
| `ConveyorLoadZone` | Load zone + belt (0.5 m/s, 4.2 m, then dispatched); label-up check (≤35°); M2_07 bridge. |
| `GazeDwellTarget` | Zone sign found by 0.5 s of gaze within 12° / 4.5 m (`HeadOverride` for tests); reports to a `MultiCounterGate` (M2_02). |
| `QcReview` | QC panel buttons (bound in code), answer check (void fill only once the box is opened), M2_09 bridges; wrong answers raise `OnUpdateMistakeCountEvent`. |
| `PackingFeedback` | One coloured message line (order panel, QC panel). |
| `AssessmentLauncher` (edited) | Optional `_quizRoot` + `_quizAnchor`: moves the quiz UI to the step's modal while it runs, puts it back when the panel closes. Used by M2_10 and the restored M1_09. |

**Design pattern — why direct object-refs exist:** MCP `set_property` **cannot wire
UnityEvents** (`Unsupported SerializedPropertyType: Generic`), but object references
(single + `List`) **are** settable. So each interaction script exposes *both* a
`UnityEvent` **and** an optional direct `[SerializeField]` ref that it invokes:
- `ToteCounter._challengeTrigger` → `ChallengeTutorialTrigger.OnTriggerComplete()`
- `ToteCounter._gate` → `MultiCounterGate.NotifyComplete()` (added for M1_06)
- `ScannerTool._challengeTrigger` → likewise
- `MultiCounterGate._challengeTrigger` → fires when all counts in.
- `LocationZoneTrigger._challengeTrigger` → fires on player arrival (M1_03).

Use the direct refs for all MCP-driven wiring.

**Arm-aware completion (2026-09-19):** a `ChallengeTutorialTrigger` ignores completions while its step
is not active and never replays them, so bridges that latched on their first attempt soft-locked the
step when the trainee acted early. The framework trigger now has `IsArmed`, an `Armed` event and
`bool TryComplete()`; every bridge (ToteCounter, MultiCounterGate, MisPickRecovery, GrabChallengeTrigger,
LocationZoneTrigger, RouteReviewPresenter) latches only when `TryComplete()` returns true and re-checks
its condition on `Armed`. Acting before a step opens now counts when the step opens (items already in
the tote, player already in the zone, wrist device still held, mis-pick already removed).

---

## 4. Prefabs & Scene Objects

**Prefabs** (`Assets/Prefabs/OrderPicking/`):
- `Tote_OrderPicking` — BoxCollider trigger + ToteCounter (SKU-1001 ×3, exclusive: no other items except the
  M1_07 SKUs 5099/5005). The trigger covers the crate interior: size (0.76, 0.62, 1.42) at y 0.31 — the
  plastic_container model is 0.81 × 0.62 × 1.47 m. (It was 0.5 × 0.4 × 0.35 until 2026-09-19, so items placed
  anywhere but the middle of the crate were never counted.) In `BasicScene` the tote no longer uses this crate —
  `M1_Tote` sits on the pushable cart and its trigger covers the cart's four bins (see **Pushable cart** below).
  **Pickables are dynamic Rigidbodies with gravity** (prefabs and scene instances; they were kinematic until
  ~2026-09-22), so a released item falls and rests where it lands.
- `PickItem_OrderPicking` — AutoHand Grabbable + SkuItem + TriggerIdentity. **Base for the products.**
- `Scanner_OrderPicking` — scanner FBX (scale 0.13), Grabbable + ScannerTool + Muzzle child.

**Per-product pickable prefabs** (`Assets/Prefabs/OrderPicking/Products/`, cloned from the base — each
has the full grab stack + SkuItem/TriggerIdentity set, and a **cube placeholder mesh to swap for a real
model**): `PickItem_BlueWidget500` (SKU-1001), `PickItem_BlueWidget750` (SKU-2002), `PickItem_RubberGasket40`
(SKU-2007), `PickItem_SteelBracket90` (SKU-3003), `PickItem_RedWidget500` (SKU-5099), `PickItem_GreenWidget500`
(SKU-5005). **To add a model:** swap the MeshFilter mesh + material (or nest the FBX and drop the cube),
refit the BoxCollider, re-record the GrabbablePose if the shape changed a lot.

**Widget bottle model applied (the 4 widgets share ONE mesh):** `Assets/Models/cylindrical/cylindrical.fbx`
(Meshy bottle) is nested as a `BottleMesh` child in each of the 4 widget prefabs (local rot X270 to
stand upright, scale 70 = ~0.2 m; 750 uses 80 = slightly larger); the base cube's MeshFilter/MeshRenderer
were removed and the BoxCollider refit (~0.6×1.33×0.6). Colors = 3 tinted URP-Lit materials in `Products/`
(`Mat_WidgetBlue/Red/Green`, duplicated from the fbx's `Material.001`, `_BaseColor` tinted) — Blue serves
500 & 750. **Single material caveat:** the bottle texture is one white-body/red-cap map, so `_BaseColor`
tints the whole bottle — the red cap goes dark on blue/green (fix later via a grey-cap texture or a 2nd
material). GrabbablePose re-record + final size/tint tuning are in-headset.

**Gasket + bracket models applied too:** `PickItem_RubberGasket40` nests `40_mm_industrial/...ru...fbx`
(`GasketMesh`, rotation identity so the ring lies flat, scale 18 ≈ 4-5 cm) and `PickItem_SteelBracket90`
nests `90_mm_L_shaped_stee...fbx` (`BracketMesh`, rotation X270 upright, scale 32 ≈ 9 cm). Each keeps its
own textured `Material.001` (no tint needed — real gasket/bracket look), cube renderers removed, BoxCollider
refit (gasket 0.35×0.12×0.35, bracket 0.58×0.62×0.22). Verified via a floating-probe screenshot (L-bracket
with bolt holes; orange rubber ring). **→ ALL pickable products now use real models, and the M1_08 confirm
marker has been RETIRED — no placeholder cubes remain in the scene.** GrabbablePose re-record + size tuning in-headset.

**Scene pickables are now instances of these product prefabs** (re-pointed from the old placeholder cubes):
`lower/M1_Prod_A1/A2/A3`→BlueWidget500, `lower/M1_Decoy_1/2`→BlueWidget750, `PickArea_B/M1_Prod_B1/B2`→BlueWidget750,
`B3/B4/B5/B6`→RubberGasket40, `PickArea_C/M1_Prod_C1`→SteelBracket90, `M1_MisPick_Wrong`→RedWidget500 (scene
root, resting in a bottom bin of the cart), `M1_MisPick_Correct`→GreenWidget500 (scene root, on the floor by the drop-off)
— each kept its exact position/rotation/scale (0.15); SKU-based tote/scan/mis-pick logic unaffected. **Duplication fixed:** the A-aisle previously had a duplicate `Bin/` set
(y0.571) alongside `lower/` (y0.182) — the `Bin/` copies were deleted so counts are correct (3× SKU-1001, etc.).
Instantiate a product prefab via `manage_gameobject create` with `prefab_path`+`save_as_prefab:false`.

**Scene** (player start ≈ -10,0,24; picking area to west / -X):
- Shelves: `WarehouseLayout_FromSVG/Picking/Aisle_{A,B,C}_Shelf_SVG (2)`
  (A z0.81, B z7.53, C z14.09) + `PickPos_A/B/C` markers.
- A-aisle (under root `lower`): `M1_Prod_A1/A2/A3` (SKU-1001),
  `M1_Decoy_1/2` (SKU-2002), `Bin_A0203`.
- B/C-aisle: `PickArea_B` → `Bin_B0102`, `Bin_B0204`, `M1_Prod_B1/B2` (SKU-2002),
  `M1_Prod_B3/B4/B5/B6` (SKU-2007); `PickArea_C` → `Bin_C0301`, `M1_Prod_C1` (SKU-3003).
- `Utility_cart/M1_Tote` — trigger over the cart's four bins, 4 ToteCounters (see M1_06 wiring), MisPickRecovery,
  CartCargo; `M1_Scanner`.

**Pushable cart (2026-09-23)** — spec `docs/superpowers/specs/2026-09-23-pushable-cart-design.md`, plan
`docs/superpowers/plans/2026-09-23-pushable-cart.md`. Hierarchy (world pose of the original cart kept):
```
Utility_cart        upright root at (1.89, 0, 3.21): kinematic Rigidbody (Interpolate) + PushCart
├─ Model            the Meshy cart mesh (FBX instance, rot X270, scale 55.72) + its 7 box colliders
├─ LowerWall_Front  box collider on the lower tier's long side (the mesh colliders had none there)
├─ LowerWall_Back   same, other side
├─ Handles          kinematic Rigidbody + AutoHand Grabbable (parentOnGrab off, heldNoFriction off,
│  │                two hands, maintainGrabOffset on, throwPower 0)
│  ├─ Handle_A      Cylinder on the end-frame top bar at x≈1.382, y 0.941 (CapsuleCollider; MeshRenderer
│  └─ Handle_B      DISABLED so it's invisible but AutoHand still has a mesh to highlight), x≈2.399
└─ M1_Tote          (see above) + CartCargo
Cart_DropOff        (1.89, 0, 3.21): CartDropOffZone + LineRenderer — yellow 1.6 × 1.3 m floor outline
```
- **Pushing:** grab a handle bar; the cart follows the controller (`Hand.follow`), yaw-only on the floor. One hand
  steers with the neck→hand direction (`_neckOffset` 0.1 m behind the head camera, so snap/smooth turns and sideways
  hand moves steer but looking around doesn't; a hand pulled in closer than 0.2 m pauses steering); two hands steer
  like a handlebar. Grip is re-taken from the current pose whenever the set of hands changes (no jumps).
- **Player:** while held, the cart ignores the player's body capsule + head collider (AutoHandPlayer drags its
  tracking space with its body, so a cart shoving the player would drag the controllers and cart after it — a
  sliding loop). Collisions come back after release once they no longer overlap; a parked cart is solid.
- **Obstacles:** each tick an `OverlapBox` (model mesh bounds, from `_floorClearance` 0.08 m up) tests the next pose;
  static/kinematic colliders outside the cart and the hands block it; fallbacks: no turn → X only → Z only → stay.
  `_blockingMask` excludes UI/Grabbing/Grabbable/Hand/HandPlayer (the hands' ~60 finger colliders would fill the
  64-slot buffer; a full buffer counts as blocked + warns once). Colliders already overlapped when a push starts are
  ignored until the cart is clear of them. **Caveat: the level has almost no
  colliders** — racks, pallets, crate stacks and the building walls are visual only (the player can walk through them
  too), so in practice the cart stops only at the few that exist (e.g. the A-02-03 bin lid plate). The 0.08 clearance
  keeps the 6 cm `PickArea_B/GameObject` floor plate from blocking the cart at the B pick spot.
- **Cargo:** a product inside the tote trigger, not held/being grabbed, and still relative to the cart for 0.3 s gets a
  `FixedJoint` to the cart body (collision left **on**, interpolation set to Interpolate); the joint is destroyed on
  AutoHand `OnBeforeGrabEvent`. Joint (not kinematic + parented) so the item's trigger pairs never change — Unity's
  docs disagree on whether kinematic–kinematic trigger events are reported under Default Contact Pairs. **No product
  may be parented under the cart** (hierarchy + physics would move it twice) — that's why `M1_MisPick_Wrong` lives at
  the scene root.
- **Headset-less testing:** set `PushCart._testDriveVelocity` (world X/Z m/s) / `_testDriveTurnSpeed` (°/s) before
  entering Play mode — the unheld cart then drives itself through the same caps and checks. **Keep both at 0 in the
  saved scene.** Selecting the cart draws its obstacle box as a gizmo.
- Verified in Play mode 2026-09-23: red item locks at start and rides exactly over 40+ m and >1000° of turning; gate
  count 1 at the drop-off → 0 when the cart leaves; the cart stops at the bin lid plate and slides along it; no
  `#CartCargo#` warnings. Re-run after the code-review fixes (layer mask, player ignore, pruned ignore list,
  physics-pose joint anchor, containment check): same results. Hand-driven pushing is in-headset only (§6 checklist).

**Tutorial modal placement (world-space `Canvas/OrderPicking`):** the 10 M1 panels are world-space UI and
were all stacked off to the side at ≈(9.5,1.29,4.25), far from the tasks. Repositioned so each modal sits
where its task happens, at eye height (y≈1.2), facing +z (identity rotation) — the trainee always faces −z
(spawn `Pos1` = world (0,0,26.2) rot y180, walks the corridor toward the aisles). Since only one modal
shows at a time, same-area steps share a spot: **M1_01/02/03 → start** at world (0,1.2,23.5) (in front of
spawn); **M1_04/05 + M1_07–M1_10 → A-aisle/tote** at world (1.0,1.2,3.3); **M1_06 → B/C aisle** at world
(0.1,1.2,12.25) — midway between B (z9) and C (z15.5) — with rotation **y180 (faces −z)** — B/C is the one place the trainee approaches from the
opposite side (walking +z up from the tote), so its modal faces −z, unlike the +z-facing A-aisle/start
modals. Local x = world x + 4 (OrderPicking is at world −4,0,0). Verified via spawn-view, A-aisle-view, and
B-aisle-approach screenshots. Fine-tune heights/spots in-headset.
The trainee walks freely between steps. **Correction (2026-10-06):** M1_01 *does* have `_teleportPlayer = 1`
(saved override, `_teleportPosition` = the spawn), as do S05/S10/S13 — so starting M1 teleports the player to the spawn
and walks the trainer there (see the trainer caveat in §7).

**Wrist pick list:** `Handheld UI` under `RobotHand (L)` — world-space Canvas, title
"Picking List", table under `Text Holder` (VerticalLayoutGroup) with a `Header` row
(SKU / ID / Description / Location / Quantity) + `Product` rows.
- Row 1 = first pick (SKU-1001 | 1001 | Blue Widget 500ml | A-02-03 | 3), active.
- Rows 2–4 = M1_06 multi-line order (drafted, **deactivated** until M1_06 view is shown):
  - SKU-2002 | Blue Widget 750ml | B-01-02 | 2
  - SKU-3003 | Steel Bracket 90mm | C-03-01 | 1
  - SKU-2007 | Rubber Gasket 40mm | B-02-04 | 4
  - Order deliberately zig-zags (B/C/B) to teach route planning.

---

## 5. Wiring Status — Module 1

| Step | Title | Trigger name | Status |
|------|-------|--------------|--------|
| M1_01 | Welcome | *(none)* | ✅ no challenge |
| M1_02 | Pick List Intro | `TRIGGER_M1_OpenPickList` | ✅ wired (chain below) |
| M1_03 | Reading Pick Location | `TRIGGER_M1_ReachLocation_A0203` | ✅ wired (chain below) |
| M1_04 | SKU Verification | `TRIGGER_M1_ScanSKU_Verified` | ✅ wired (chain below) |
| M1_05 | Quantity Pick (×3) | `TRIGGER_M1_PickQuantity_3` | ✅ fully wired & working |
| M1_06 | Multi-Line Order | `TRIGGER_M1_MultiLine_AllPicked` | ✅ wired — 3 lines in the cart **and** cart parked at the drop-off (chain below) |
| M1_07 | Error Recovery | `TRIGGER_M1_RemoveMisPick`, `TRIGGER_M1_ReplaceCorrectItem` | ✅ wired (chain below) |
| M1_08 | Path Efficiency Review | `TRIGGER_M1_PathReviewConfirmed` | ✅ wired — real route overlay + auto-complete ~1.5s after it shows (grab marker retired; chain below) |
| M1_09 | Assessment | `TRIGGER_M1_AssessmentComplete` | ✅ wired — ≥80% pass gate + modal hidden during quiz + fail/retry screen (chain below) |
| M1_10 | Complete | *(none)* | ✅ no challenge |

**M1_06 chain (verified; drop-off added 2026-09-23):**
```
3 lines dropped in M1_Tote (the cart's bins)
  → each line's ToteCounter reports _gate.SetSourceMet(this, met)          (3 live sources)
cart centre inside the Cart_DropOff outline
  → CartDropOffZone reports _gate.SetSourceMet(this, inside)               (4th live source)
  → M1_MultiLineGate (_requiredCount=4) fires _challengeTrigger once all 4 are met while M1_06 is armed
  → Trigger_M1_MultiLine (name TRIGGER_M1_MultiLine_AllPicked)
  → M1_06 panel challenge complete
```
Challenge title (asset + `training_flow_module1.json`): "Complete All 3 Picks and Return the Cart to the Drop-Off".
The cart starts parked, so a trainee who carries items back by hand instead of pushing still completes M1_06.
The narrated M1_05/M1_06 descriptions were left untouched to match the voice-over (M1_05's "virtual tote on your
left side" is stale — re-record if wanted).
`M1_Tote` ToteCounters: idx0 SKU-1001×3 (M1_05, `_gate` null) · idx1 SKU-2002×2 ·
idx2 SKU-2007×4 · idx3 SKU-3003×1 (idx1–3 `_gate` → `M1_MultiLineGate`).
All counters share the tote's one trigger BoxCollider; each ignores non-matching SKUs.

**M1_03 chain (verified):**
```
player walks into Zone_A0203 (trigger box, world 0.3/1/2.8, size 3.5×2.5×3)
  → AutoHandPlayer's TriggerIdentity(Id="Player") matches LocationZoneTrigger._requiredId
  → LocationZoneTrigger fires _challengeTrigger
  → Trigger_M1_ReachLocation (name TRIGGER_M1_ReachLocation_A0203, child of M1_03 panel)
  → M1_03 panel challenge completes
```
`Zone_A0203` is a root-level GameObject placed on the floor in front of the real bin
A-02-03 (world XZ ≈ 0.3 / 2.3; note PickPos_A at z18.2 is a stale marker, not used).
Player rig already carries a trigger SphereCollider + Rigidbody + `TriggerIdentity("Player")`
— no player changes needed. Runtime (walk-in) confirmation is pending in-headset.

**M1_04 chain (verified):**
```
squeeze the held M1_Scanner (AutoHand Grabbable.OnSqueezeEvent)
  → ScannerActivator.HandleSqueeze → ScannerTool.Scan()
  → raycast from Muzzle (maxDist 3, mask 1015) hits a SkuItem
  → Sku == _expectedSku ("SKU-1001") → fires _challengeTrigger
  → Trigger_M1_ScanSKU (name TRIGGER_M1_ScanSKU_Verified) → M1_04 panel challenge completes
```
`M1_Scanner` components: Rigidbody + BoxCollider + `Autohand.Grabbable` + `ScannerTool`
(`_challengeTrigger`→Trigger_M1_ScanSKU, `_muzzle`→Muzzle child) + `ScannerActivator`
(`_grabbable`→Grabbable, `_scanner`→ScannerTool). "Squeeze" = whatever the AutoHand Hand
binds as its squeeze/activate input (unchanged). Mismatch fires `_onScanMismatch` (red).
Runtime (squeeze-to-scan) confirmation pending in-headset.

**M1_02 chain (verified):**
```
grab the Handheld UI wrist device (AutoHand Grabbable.OnGrabEvent)
  → GrabChallengeTrigger.HandleGrab → Trigger_M1_OpenPickList.OnTriggerComplete()
  → (once M1_02 is the active step so ChallengeTutorialTrigger._enabled=true)
  → M1_02 panel challenge completes
```
`Handheld UI` (#75920, child of RobotHand (L)) is an `Autohand.Grabbable` device (always
active). Added `GrabChallengeTrigger` (`_grabbable`→its Grabbable, `_challengeTrigger`→
Trigger_M1_OpenPickList #75796, `_fireOnce`=true). `Trigger_M1_OpenPickList` already existed
and was already in the M1_02 panel list. Runtime (grab-to-open) confirmation pending in-headset.

**Framework note:** `ChallengeTutorialTrigger` only completes a challenge when its `_enabled`
is true — the framework arms it (`OnUpdateTutorialTriggerState(true)`) when that step becomes
active. Its `_grabbable`/`_isGrabTrigger` fields do NOT auto-fire on grab (the built-in
`OnGrabEvent` sub only toggles the outline); completion always needs an external
`OnTriggerComplete()` call — which is why every step uses a code-bridge/collider hook. Bridges now call
`TryComplete()` and re-offer on `Armed` (see §3, arm-aware completion).

**M1_07 chain (verified) — two ordered challenges:**
```
remove: pull SKU-5099 item out of M1_Tote (Collider exit)
  → MisPickRecovery.OnTriggerExit → Trigger_M1_RemoveMisPick (TRIGGER_M1_RemoveMisPick)
then replace: drop SKU-5005 item into M1_Tote (Collider enter, only after removal)
  → MisPickRecovery.OnTriggerEnter → Trigger_M1_ReplaceCorrectItem (TRIGGER_M1_ReplaceCorrectItem)
  → both in M1_07 panel list → step completes
```
`MisPickRecovery` on M1_Tote (shares its trigger BoxCollider), `_wrongSku`=SKU-5099,
`_correctSku`=SKU-5005, `_requireRemoveBeforeReplace`=true. Dedicated pickables (dynamic, gravity on), both at
the **scene root** since 2026-09-23 (nothing may be parented under the moving cart): `M1_MisPick_Wrong` (SKU-5099
"Red Widget 500ml") at ≈ world (2.22, 0.34, 3.41), resting in a bottom bin of the cart inside the tote trigger —
`CartCargo` joints it to the cart ~0.3 s after start, so it rides along until grabbed; `M1_MisPick_Correct`
(SKU-5005 "Green Widget 500ml") on the floor at ≈ world (1.13, 0.09, 2.38), just outside the drop-off outline.
M1_06 requires the cart back at the drop-off, so the green item is next to the cart when M1_07 opens.
SKU-5099/5005 are ignored by the M1_05 counter and watched by no other ToteCounter → no interference with M1_05/06.
MisPickRecovery logs a warning when M1_07 opens if the red item was never inside the trigger. **Headset check:**
confirm the green item's floor spot is clear and reachable, and that both items are grabbable.
Runtime remove-then-replace test pending in-headset.

**M1_08 chain (verified) — auto-complete after review (grab marker RETIRED):**
```
M1_08 opens → RouteReviewPresenter.OnEnable → StopRecording + ShowOverlay
  → after _autoCompleteDelay (1.5s) → _completeTrigger.OnTriggerComplete()
  → Trigger_M1_PathReview (TRIGGER_M1_PathReviewConfirmed) → M1_08 completes → "Next"
```
The old `M1_PathReview_Confirm` grab-cube (and its `GrabChallengeTrigger`) was **deleted** — the last
placeholder cube in the scene. Completion is now driven by `RouteReviewPresenter._completeTrigger`→
`Trigger_M1_PathReview` (still in the M1_08 panel list): the presenter shows the overlay, then a ~1.5s
coroutine fires the trigger (the delay lets the trainee see the lines AND lets the framework arm the
trigger — arming happens a frame or two after the panel enables). `_autoCompleteDelay` is tunable.

**Route overlay NOW BUILT (was a placeholder):** the real optimal-vs-actual route comparison. Three new
scripts (TrainingInteractions):
- **`RouteRecorder`** on `RouteTracker` (root): on `TutorialManager.OnTrainingStartedEvent(OrderPicking)`
  it clears + records the player's floor position (`_player`→`AutoHandPlayer`) every `_minSampleDistance`
  (0.3 m), flattened to `_floorY` 0.03. Exposes `Points` / `StartRecording` / `StopRecording`.
- **`RouteOverlayController`** on `RouteOverlay` (root): `ShowOverlay()` draws `OptimalLine` (green) through
  the 7 `OptimalRouteWaypoints` markers and `ActualLine` (amber) through `RouteRecorder.Points`; both are
  `LineRenderer`s (width 0.05, world-space, shared `Assets/Materials/RouteLineMat.mat` = Sprites/Default so
  vertex colours show). `Awake` hides them.
- **`RouteReviewPresenter`** on the M1_08 panel: gated on `OnTrainingStartedEvent(OrderPicking)` (panels save
  active, like the pick-list swapper); on the real M1_08 show it calls `recorder.StopRecording()` +
  `overlay.ShowOverlay()`.

Optimal waypoints (world XZ, floor markers, tunable): Start (−10.045, 23.954) → A-02-03 (0.3, 2.8) → B-01-02
(−0.3, 9.0) → B-02-04 (0.5, 9.0) → C-03-01 (0.157, 15.562) → drop-off `WP6_DropOff` (1.89, 3.21) — the "do all of
one aisle before the next" optimal with the cart travelling along (the mid-route "back to the tote" leg `WP2_Tote`
was deleted on 2026-09-23 and `WP6_ToteReturn` became `WP6_DropOff`). The amber actual line is still the player's path. **Headset tuning:** waypoint positions, line Y/width/colours are drafts.
**Still not built (intentional, YAGNI):** per-segment "you backtracked here" callouts, distance/% stats, route
scoring. Runtime record-then-review test pending in-headset.

**M1_09 chain (verified) — reuses the EXISTING Valari assessment framework, not a new quiz:**
```
M1_09 modal opens → AssessmentLauncher.OnEnable → AssessmentManager.LaunchAssessment()
  → existing AssessmentUI shows the 13 OrderPicking questions (already authored in
    AssessmentDataCollectionSO.asset, TrainingID.OrderPicking) with 4-choice buttons
learner answers all 13 → result screen (score/total) → Close → OnAssessmentFinishedEvent(score)
  → AssessmentChallengeGate: passed = score*100 >= 80*total  (i.e. ≥11/13)
     PASS → Trigger_M1_AssessmentComplete.OnTriggerComplete() → M1_09 challenge completes → "Next"
     FAIL → AssessmentFailPanel.Show(score,total,80%) → "Not passed — Retake?" → Retake button
            → AssessmentManager.RetakeAssessment() → quiz relaunches from Q1
```
**Why this differs from M1_02–M1_08:** the 13-Q assessment framework already existed and was
already wired (`AssessmentManager` #−83972 → `AssessmentDataCollectionSO.asset` + `AssesmentModalUI`;
`PerformanceEvaluationManager` present). It is **event-driven, not trigger-driven**, and had **no
pass/fail gate** (only a %) — the framework normally auto-launches the quiz *after the last modal*
(`TutorialManager.OnTrainingCompleteEvent`, i.e. post-M1_10). To make M1_09 gate like the other
steps, three things were added:
- **`AssessmentLauncher`** (new, on the M1_09 panel `#−64492`): `OnEnable` → `AssessmentManager.LaunchAssessment()`,
  so the quiz launches *while M1_09 is the active/armed step* (a `ChallengeTutorialTrigger` only
  completes its step while armed — post-M1_10 it would no-op). `LaunchAssessment()` returns false
  until the assessment is initialized, so an early enable at scene-load is ignored (no false latch).
- **`AssessmentChallengeGate`** (new, on `AssessmentManager` `#−83972`): subscribes to the static
  `AssessmentManager.OnAssessmentFinishedEvent`; applies the **≥80% pass rule** (`_passPercent=80`);
  on pass fires `_challengeTrigger`→`Trigger_M1_AssessmentComplete`; on fail (`_retakeOnFail=true`)
  calls `RetakeAssessment()`. Since 2026-09-19 it only reacts while `_trainingId` (OrderPicking) is the
  running module and its trigger is armed — the finished event is shared by every module's quiz, and it
  used to grade (and fail/retake) the Safety quiz too.
- **`AssessmentManager.cs` edits (additive, framework)**: new public `LaunchAssessment()` /
  `RetakeAssessment()` / `TotalQuestions`, plus a `_manualLaunchConsumed` guard so the quiz launched
  at M1_09 does **not** re-appear after the module-complete screen. Modules that never launch
  manually keep the original auto-launch behaviour — no cross-module impact.

`M1_09_Assessment.asset` (TutorialData) updated: `HasChallenges=1`, `IsInOrder=1`, one challenge
(`TRIGGER_M1_AssessmentComplete`, "Complete the Order Picking Assessment"). The M1_09 panel was made
a challenge modal to match M1_02–08: `_hasChallenges=true`, `_challengeHolder`→a `ChallengesHolder`
(duplicated from M1_08's), `_challengeToggle`→`ChallengeToggle.prefab`, trigger added to the list.
Compiles/saves clean (only the benign SceneTemplate NRE).
**Modal hidden during quiz (DONE):** `AssessmentLauncher._hideDuringQuiz`→M1_09 Container. The launcher
runs a coroutine that waits until the framework's show-coroutine has activated the Container, then
`SetActive(false)` — so only the quiz is on screen (hiding at OnEnable alone wouldn't stick: the
framework re-activates the Container a frame later). `AssessmentChallengeGate._showOnPass`→Container
re-shows it on a **pass** so the "Next" button is reachable; a **fail** keeps it hidden.
**Fail/retry screen (DONE):** on a failing score `AssessmentChallengeGate._failPanel`→`AssessmentFailPanel`
shows a "Not passed — you scored X/13, 80% (11/13) required — Retake?" screen instead of silently
relaunching. `AssessmentFailPanel` (new script) was built by duplicating the quiz **result** screen
(`Container (1)`→`AssessmentFailPanel` under `AssesmentModalUI`; Animator removed so it shows statically,
CanvasGroup alpha 1, kept inactive), repurposing its `Score` TMP as the message and its `Button` as
"Retake". Its `Awake` disables the button's inherited persistent onClick (the duplicated result-screen
Close) via `SetPersistentListenerState(Off)` + binds `OnRetry` → `RetakeAssessment()` + hides. Runtime
take-the-quiz test pending in-headset (fail-screen text size/layout is a draft — tune in-headset).

**M1_09 restored (2026-10-06):** the `M1_09_Assessment` panel had disappeared from the saved scene (before the
2026-09-24 save) — not in `Canvas/OrderPicking`, not in the OrderPicking flow list, and the M1 `AssessmentChallengeGate`'s
`_challengeTrigger` / `_showOnPass` were null, so M1 went M1_08 → M1_10 with no quiz. Rebuilt by
**Tools ▸ Packing Dispatch ▸ 3. Restore M1_09 Assessment** (copy of the M1_08 panel at the A-aisle spot,
`RouteReviewPresenter` removed, new `Trigger_M1_AssessmentComplete`, `AssessmentLauncher` with `_hideDuringQuiz`, gate
re-wired, inserted before M1_10 → OrderPicking flow has 10 entries again). New: the quiz UI (fixed at the spawn) is moved
to `Canvas/OrderPicking/QuizAnchor_M1` (the modal's spot) while M1_09 runs and put back afterwards. Verified in Play mode
(fresh start, and again right after the M2 module in the same session once the trainer fix in §7 landed): M1_09 is
step 9, quiz moves + launches, a pass completes the step and re-shows the modal.

---

## 5b. Wiring Status — Module 2 (Packing and Dispatch)

Spec `docs/superpowers/specs/2026-10-06-packing-dispatch-module-design.md`, plan
`docs/superpowers/plans/2026-10-06-packing-dispatch-module.md`. Content was already authored: 11 `TutorialData`
assets + voice-over, 12 assessment questions (`AssessmentDataCollectionSO` ID 3), 11 panels in the PackingAndDispatch
flow list. Built on 2026-10-06: the station, all interactions, the wiring, and the M2_10 challenge.

| Step | Title | Trigger name(s) | Completes when | Status |
|------|-------|-----------------|----------------|--------|
| M2_01 | Welcome | *(none)* | — | ✅ |
| M2_02 | Station Layout | `TRIGGER_M2_StationLayoutConfirmed` | all 3 zone signs found by gaze (`M2_StationLayoutGate`, required 3); panel **auto-next** (modal is at the spawn) | ✅ |
| M2_03 | Box Selection | `TRIGGER_M2_CorrectBoxSelected` | correct carton (Medium) locked on the packing spot | ✅ |
| M2_04 | Void Fill | `TRIGGER_M2_VoidFillBase`, `…VoidFillTop` | base 2 pillows before the product; product + 3 pillows → full (flaps close) | ✅ |
| M2_05 | Sealing | `TRIGGER_M2_BoxSealed` | tape gun swept along centre + both end seams (H pattern) | ✅ |
| M2_06 | Label Verification | `TRIGGER_M2_LabelVerified`, `…LabelApplied` | label scanned with `M2_Scanner`; label let go on the sealed box's top face | ✅ |
| M2_07 | Conveyor Loading | `TRIGGER_M2_BoxOnConveyor` | labelled box released in the load zone label-up (≤35°) | ✅ |
| M2_08 | Multi-Item Consolidation | `TRIGGER_M2_MultiItemPacked` | order 2 in one Large carton: base 2, 3 products, top 3 → full | ✅ |
| M2_09 | Error Identification | `TRIGGER_M2_ErrorIdentified`, `…ErrorCorrected` | QC box cut open + **Void fill** answered; then filled to full + centre seam re-taped | ✅ |
| M2_10 | Assessment | `TRIGGER_M2_AssessmentComplete` | ≥80 % (10/12) — `AssessmentLauncher` + second `AssessmentChallengeGate` (`M2_PackingStation/M2_AssessmentGate`, trainingId PackingAndDispatch) + shared `AssessmentFailPanel` | ✅ |
| M2_11 | Complete | *(none)* | — | ✅ |

Every bridge is arm-aware (`ArmedLatch`, re-offers on `Armed`): doing a step's action early counts when the step opens.
Order-1 milestones are read from the order-1 carton even after it is dispatched, so nothing soft-locks.

**Station** — root `M2_PackingStation` on the open floor in front of the loading-dock door (east side). The trainee
stands at x ≈ 15.2 facing +x (the dock):
```
M2_PackingStation
├─ Bench (16,0,13)        PackingStation; top y 0.90; PackingSpot (15.85,0.90,13.2) yaw 90 + yellow outline; 3 display
│                          slots on the back strip (x 16.25); order board (OrderText, LabelReadout, Feedback) on posts
├─ CartonRack (16,0,15.05) top y 0.75; Slot_Small/Medium/Large (cartons length along +x, size mark on the -x end)
├─ VoidFillDispenser (15.85,0.90,12.15)  tray + 3 slots; LabelPrinter (16.22,0.90,12.05) + ShippingLabel_ORD-2001 on top
├─ TapeGun (15.75,0.96,12.6) · M2_Scanner (copy of M1_Scanner, expected SKU LBL-ORD-2001, no direct trigger) (15.75,0.98,13.9)
├─ Conveyor               belt x 15.55→20.55, z 11.3, top y 0.85; LoadZone (16.1,1.0,11.3) ConveyorLoadZone; OUTBOUND sign
├─ QCTable (16,0,10)      QC_Carton_ORD-1874 (Medium, pre-packed: closed, H-taped, labelled, mug set inside, fill 1/5,
│                          cuttable) + BoxCutter
├─ Sign_Cartons / Sign_WorkArea / Sign_LabelsConveyor (y 2.0)  GazeDwellTarget → M2_StationLayoutGate
├─ QuizAnchor_M2 · M2_AssessmentGate
Canvas/M2_QCPanel         QcReview: QC order text, LABEL / SEAL / VOID FILL buttons (laser-pointer UI), result + feedback
Canvas/PackingAndDispatch the 11 M2 panels (moved from Canvas/SafetyPreparation): M2_01/02 at the spawn (M1_01's pose),
                          M2_03–08/10/11 at (16.35,1.45,14.75) yaw 35 (above the rack, angled to the bench), M2_09 at
                          (16.3,1.45,8.95) yaw 134 (by the QC table)
```
**Orders:** ORD-2001 Wireless Speaker 24×10×14 cm → **Medium** (fits Small physically but not with 5 cm clearance);
ORD-2002 Desk Lamp + Book Set + Cable Kit, packed 32×30×14 cm → **Large**; QC ORD-1874 Ceramic Mug Set in a Medium,
fault = void fill. Addresses/carriers are fictional. Products are coloured retail-box placeholders (no real models yet).
**M2_10 panel was replaced:** it was a plain modal (no challenge checklist), so the builder swapped in a copy of the M2_09
challenge modal pointing at `M2_10_Assessment.asset` (which now has `HasChallenges=1` + the one challenge).

**Builder** — `Assets/Editor/PackingStationBuilder.cs`, menu **Tools ▸ Packing Dispatch**:
`1. Build Prefabs` (`Assets/Prefabs/PackingDispatch/`: Carton_Small/Medium/Large, AirPillow, Product_Speaker/DeskLamp/
BookSet/CableKit, ShippingLabel_ORD-2001, TapeGun, BoxCutter + 22 URP Lit materials), `2. Build Station + Wire M2`
(deletes and rebuilds `M2_PackingStation` + `Canvas/M2_QCPanel`, recreates the M2 trigger objects, places the panels,
wires everything — **manual tweaks to the station are lost on rebuild**; positions are constants at the top of the file),
`3. Restore M1_09 Assessment` (no-op if it exists). All steps support Undo and log `[PackingBuilder]`.

**Headset-less test** — `Assets/Editor/PackingDispatchPlaytest.cs`, **Tools ▸ Packing Dispatch ▸ Test: M2 Walkthrough
(Play mode)**: with auto-start = PackingAndDispatch, enter Play mode and run it; it plays all 11 steps (88 checks incl.
wrong carton, product before fill, extra pillow, single tape strip, unscanned label, label-down box, wrong QC answer,
failed quiz + retake, quiz moved to the station and back) and writes `Temp/m2_playtest.txt`. **Result 2026-10-06:
88 passed, 0 failed** (three times, the last with the trainer fix).

**Polish 2026-10-09:**
- **Packed products rode away without the box** (left behind when the box was carried / on the conveyor) although they
  *were* parented under the carton's `Contents`: their Rigidbody is set to Interpolate (AutoHand restores it on release),
  and an interpolated Rigidbody writes its old physics pose to the transform every frame, overriding the parent's
  movement — measured 13.6 cm off after 0.4 m of belt. `PackableItem.PackInto` now also sets interpolation None, and
  `PackingCarton.PutOnConveyor` does the same for the box (the belt moves it by transform; it had been dragged back to
  about half the belt speed). Now 0.0 cm off, and the box covers 0.75 m in the same time.
  **Rule: never move an interpolated Rigidbody (or its parent) through its transform** — make it kinematic +
  interpolation None first, or use `MovePosition`.
- **QC box label and tape hidden while open:** `LabelVisual` and the tape strips sit on the closed top, so
  `RefreshVisuals`/`RefreshTape` now show them only while the box is closed (they floated above the open box); both come
  back when the box is filled and closes.
- Walkthrough: 7 new checks (speaker parented, carried and conveyed with the box; QC label + end tape hidden while open,
  shown again when closed); tool sweeps now run with interpolation off (like a held tool) and one frame per step.
  Result: everything up to M2_09 passes (82 checks). **M2_10 fails because `M2_10_Assessment` was removed from the
  TutorialManager's PackingAndDispatch flow list** (scene saved 2026-10-09 11:26: list size 10, M2_09 → M2_11; the panel
  itself is intact) — not changed here; to bring the quiz back, re-add it in the Inspector (TutorialManager ▸ Tutorial
  Flow List ▸ PackingAndDispatch, between M2_09 and M2_11).
- **Builder warning:** the products, `AirPillow` and `TapeGun` prefabs now carry hand-made real models (2026-10-08/09).
  *1. Build Prefabs* would overwrite them with the primitive versions — don't run it again (or remove those prefabs from
  it first). *2. Build Station* rebuilds the station and loses manual scene tweaks to it.
  `TutorialManager._llmHandler` is also null now, so pressing "Check" on an unfinished step throws in `SendPrompt`. `Test: M1_09 Smoke` runs Order Picking straight
after the M2 walkthrough (or on a fresh session with auto-start = None) — see §5 M1_09 note.

---

## 6. Task Tracker

**Wiring — ✅ ALL COMPLETE (M1_02–M1_09; M1_01/M1_10 have no challenge):**
- [x] **M1_03** — navigation trigger DONE (Zone_A0203 → TRIGGER_M1_ReachLocation_A0203).
      Runtime walk-in test pending in-headset.
- [x] **M1_07** — error recovery DONE (MisPickRecovery on M1_Tote; remove SKU-5099 then
      add SKU-5005). Runtime remove-then-replace test + item-height tuning pending in-headset.
- [x] **M1_08** — completes automatically ~1.5 s after the route overlay shows (grab marker retired) AND the real
      optimal-vs-actual **route overlay is now built** (`RouteRecorder` + `RouteOverlayController` +
      `RouteReviewPresenter`; green optimal line through 7 waypoints, amber recorded actual line). See
      M1_08 chain in §5. Runtime record-then-review test + waypoint/line tuning pending in-headset.
- [x] **M1_09** — assessment DONE. Reused the existing Valari assessment framework (13 OrderPicking
      questions already authored) + added `AssessmentLauncher`, `AssessmentChallengeGate`, and a ≥80%
      pass gate that fires `TRIGGER_M1_AssessmentComplete`. See M1_09 chain in §5. UX polish DONE
      (M1_09 modal hidden during quiz; `AssessmentFailPanel` fail/retry screen). Runtime quiz test pending in-headset.
      **→ All M1_02–M1_09 challenge triggers are now wired. M1_01/M1_10 have no challenge.**

**Done this pass:**
- [x] **M1_02** DONE — `GrabChallengeTrigger` on Handheld UI subscribes to
      `Grabbable.OnGrabEvent` and fires `TRIGGER_M1_OpenPickList`. Grab-to-open test pending in-headset.
- [x] **M1_04** DONE — `ScannerActivator` subscribes to `Grabbable.OnSqueezeEvent`
      and calls `ScannerTool.Scan()`. Runtime squeeze-to-scan test pending in-headset.

**UI / flow:**
- [x] Wrist pick-list 1-line→3-line swap DONE — `PickListMultiLineSwapper` on the M1_06 panel
      (`#−75862`) hides row 1 (`Product` `#74038`, the SKU-1001 pick) and shows rows 2–4
      (`Product (1..3)` `#72512/72540/75780`, the B/C/B order) when M1_06 is shown. Gated on the
      OrderPicking module having started (`TutorialManager.OnTrainingStartedEvent`) because the
      tutorial panels are saved **active** — a naive `OnEnable` would swap at scene load; the same
      event resets to the 1-line view so a module restart shows the correct initial state. **Design
      choice:** it treats M1_06 as a fresh 3-line order and *hides* row 1 — if you'd rather keep row 1
      visible (4-line view), just clear the swapper's `_singleLineRows` list. Runtime test pending in-headset.

**Build readiness — ✅ DONE (so it can be built & tested):**
- [x] `Assets/Scenes/BasicScene.unity` added to **Build Settings as scene 0** — it was MISSING (only
      `SampleScene` was listed, so a build would have loaded the wrong scene). `SampleScene` kept at index 1.
- [x] Build target is already **Android (Quest)**; project **compiles clean** (0 errors / 0 warnings).
- **To build:** *File ▸ Build* (or *Build And Run* with the Quest attached) → loads BasicScene. If you
      intended SampleScene to load first, just reorder the two in Build Settings.

**Review fixes (2026-09-19) — done, compile clean, logic tests 23/23:**
- [x] Tote trigger resized to the crate interior (M1_07 could not complete; most placed items never counted).
- [x] Early actions no longer soft-lock M1_02/03/05/06/07 (arm-aware bridges, §3).
- [x] M1_05 needs exactly the pick-list item: a decoy (SKU-2002) in the tote blocks it until removed, so decoys
      can no longer pre-complete the M1_06 B-01-02 line; M1_06 lines count only while their items are in the tote.
- [x] M1_08 keeps offering its completion until the step is armed (no single timed shot).
- [x] M1_09 gate ignores other modules' quizzes.
- [x] Scanner mask = Default | Grabbing | Grabbable (193; was 1015, which included the hand layers).
- [x] `TutorialManager` auto-start is a serialized field (`_autoStartTraining`, default WarehouseSafety).
      **BasicScene sets it to PackingAndDispatch (since 2026-10-06, was OrderPicking) for M2 testing — set it back to
      WarehouseSafety for the product build.**
- [x] Unused `WristPickListUI` removed.

**⏳ In-headset test checklist (do while testing — none of these block the build):**
- [ ] M1_02 — grab the wrist device → pick list opens/completes.
- [ ] M1_03 — walk into Zone_A0203 → step completes.
- [ ] M1_04 — squeeze the held scanner at the SKU label → green verify → completes.
- [ ] M1_05 — place exactly the 3× SKU-1001 in the tote (a decoy in it blocks completion until removed) → completes.
- [ ] M1_06 — wrist list swaps to the 3-line B/C/B order; drop all 3 lines into the cart → completes only once the
      cart is back inside the yellow drop-off outline (or never left it).
- [ ] M1_07 — pull the SKU-5099 mis-pick out, then drop the SKU-5005 correct item in → completes.
- [ ] Cart — grab either handle bar (does it highlight? does the hand pose look right?); push with one hand while
      walking/thumbsticking; snap-turn while holding (cart swings round with you); two hands on one bar steer like
      a handlebar; letting go leaves it parked and solid.
- [ ] Cart — items dropped in (also while it's moving) settle and ride along; grabbing one takes it straight out;
      pushing the cart into the A-02-03 bin lid stops it / slides along it and the stretched grip lets go.
- [ ] Cart — pull it back / hold it close to your body: no involuntary sliding; look around while holding with one
      hand: the cart shouldn't steer; watch the gripping hand at speed for jitter along the bar; on a snag, a
      harmless "Setting linear velocity of a kinematic body is not supported" warning may appear (AutoHand writes
      velocity to the kinematic Handles body when it force-releases).
- [ ] M1_08 — green optimal vs amber actual route lines draw on the floor; the step completes by itself ~1.5 s later.
- [ ] M1_09 — take the 13-Q quiz; ≥11/13 → "Next"; <11/13 → fail/retry screen → Retake relaunches.
- [ ] Early actions — grab the wrist device during M1_01, or fill the tote during M1_04: the later step
      should complete as soon as it opens instead of getting stuck.

**Module 2 — ✅ built and Play-mode verified (2026-10-06), in-headset checks pending:**
- [x] Station, prefabs, 12 challenge triggers, M2_10 challenge + assessment gate, quiz placement — see §5b.
- [x] M1_09 Assessment panel restored (was missing from the scene) — see §5.
- [ ] M2_02 — walk to the bench, look at CARTONS / WORK AREA / LABELS & CONVEYOR: each turns green ("FOUND"); is 12° / 0.5 s
      comfortable? The step auto-advances (its modal is at the spawn).
- [ ] M2_03 — pick a carton off the rack (two hands work; does the grab feel right on a thin wall?), set it inside the
      yellow PACK HERE outline: Small/Large are refused with a reason and slide back to the rack; Medium locks.
- [ ] M2_04 — grab pillows from the tray and drop them in (absorbed; the fill level rises); the product is refused until
      2 pillows are in; is the fill visual readable? Flaps close at full.
- [ ] M2_05 — sweep the tape gun along each seam (yellow guides); tape grows as you go; head radius 7 cm, 11 of 12 bins.
- [ ] M2_06 — squeeze the bench scanner at the label on the printer (readout on the order board), then press the label onto
      the box top. Label text is small (4–9 mm) — readable up close?
- [ ] M2_07 — carry the sealed box (two hands) to the conveyor start; label-down/sideways is refused; label-up rides away.
- [ ] M2_08 — order 2 appears after dispatch; Large carton, 3 products, pillows.
- [ ] M2_09 — cut the QC box's centre seam with the box cutter, press VOID FILL (laser pointer), fill, re-tape.
- [ ] M2_10 — quiz appears at the station (not the spawn); fail → retake; pass → Next.
- [ ] Modal placement (above the rack, yaw 35; QC modal yaw 134) and the order-board text size.

**⏳ In-headset tuning (needs the visual/VR — can't be done from MCP):**
- [ ] Raise `Bin_B0204` to shelf level 2, `Bin_C0301` to shelf level 3 (drafted low); nudge B/C onto shelf faces.
- [ ] Scanner grab-pose / muzzle direction.
- [ ] M1_07 items — red item in a bottom bin of the cart, green item on the floor by the drop-off; confirm both are grabbable.
- [ ] Drafts to tune: M1_08 route waypoint positions + line Y/width/colours; M1_09 fail-screen text size/layout.
- [ ] Cart drafts: `PushCart` `_maxSpeed` 2.5 / `_maxTurnSpeed` 180 / `_floorClearance` 0.08; handle collider fit
      (radius 1.75 cm); `CartCargo._settleTime` 0.3; drop-off `_size` 1.6 × 1.3.
- [ ] Level: racks, pallets, crate stacks and walls have no colliders (the cart and the player pass through them).
      Adding simple box colliders to rack uprights/walls would make the cart stop at them — decide if wanted.

**Quest performance (2026-09-21/22 — measured in the editor, not yet profiled on device):**
Quest renders with quality level "Low" → `Performance URP Config` + `Android Preset` renderer.
Result so far: active renderers 10,382 → 3,154, triangles 3.84 M → 2.22 M, UI raycasters 37 → 1, occlusion culling
baked. Draws in view from the spawn point (frustum only, before occlusion): 8,887 → 2,473.
The pass is re-runnable from **Tools ▸ Quest Performance** (`Assets/Editor/QuestPerformanceTools.cs`); every step
logs a `[QuestPerf]` summary and scene edits support Undo. `Report` prints the counts above.
- [x] Settings pass: Depth + Opaque Texture off, main-light shadows off, Volume Update Mode = Via Scripting;
      `TimeManager` Maximum Allowed Timestep 0.333 → 0.05. **Keep Depth/Opaque Texture off** — anything sampling
      Scene Color/Depth renders wrong on Quest; `bulb_outer.mat` + `wall_gate_windows.mat` (EntranceGate) are plain
      URP Lit transparent for that reason.
- [x] Fixed foveated rendering: `Assets/Scripts/QuestFoveation.cs` sets High + dynamic (Android player only).
- [x] **Pallet stacks merged** (step 1): the 88 `pallet_boxes_prefab` / `pallet_box_pack_prefab` instances (incl.
      nested) went from 9,464 box/tape/sticker renderers to 2,236 merged ones (one per material + lightmap). Originals
      are disabled, not deleted; merged meshes are in `Assets/Generated/MergedMeshes/BasicScene/` (193 MB text, ~91 MB
      mesh memory — each stack needs its own copy because its lightmap UVs differ). `MergedMeshGroup`
      (`Assets/Scripts/Performance/`) reapplies the baked lightmap at load; the merged UV1 has each part's lightmap
      scale/offset baked in, every other UV channel is copied unchanged (`shdr_basic_01` reads textures from UV0
      or UV2 — dropping UV2 turned box faces black). **After a lighting rebake or editing the pallet prefabs:**
      `1b. Unmerge`, rebake/edit, then `1. Merge` again.
- [x] Occlusion culling (steps 2–3): 3,108 static renderers are occludees, 2,467 of them occluders (anything with
      a Rigidbody, Animator, Canvas, AutoHand or ChallengeTutorialTrigger component up the hierarchy is skipped).
      Baked with smallest occluder 1 m, smallest hole 0.15 m. Re-bake after moving static environment.
- [x] UI raycasters (step 4): removed from the 36 shelf-label canvases and the display-only wrist pick list.
- [x] Mesh LOD (step 5): Unity Mesh LOD on the 9 heavy models, 25 renderers forced to a coarser level even up close
      (oil spill 361k → 13k, bottles 47.5k → 6.7k, scanner 69k → 4.9k, …). Racks' `main_structural_01` is capped at
      LOD 1 (190k → ~half): LOD 2+ twists the perforated uprights. `5b. Clear Forced Mesh LOD` undoes the forcing.
- [x] Android textures capped at 2048 (step 6): one 8K + seventeen 4K textures.
- [x] `shdr_basic_01`: Clear Coat off, Render Face Front (checked from 8 trainee viewpoints, nothing missing).
- [x] Code: trainer typing uses `maxVisibleCharacters` (no per-letter TMP rebuild); outlines are toggled via the
      `Outline` component's `enabled` instead of width 0.
- [ ] Headset: check frame time with OVR Metrics Tool, that foveation is active, the EntranceGate glass look, and
      that merged pallets look lit (MergedMeshGroup applies lightmaps at runtime).
- [ ] Not done: stickers and tape are still ~40% of the merged draws and could be baked into the box textures; 9 realtime
      ceiling point lights do nothing on Quest; merged meshes could use half-precision attributes (~30% smaller) or
      be generated at build time instead of stored in the project.
- **Editor crashes seen (Meta XR SDK, not project code):** `OVRTelemetry` in the Project Setup Tool during an asset
  refresh, and `OpenXRLoaderBase.Deinitialize` on the Meta XR Simulator when leaving Play mode.

---

## 7. Environment Gotchas (read before using MCP)

- **Live Unity editor runs on the `main` checkout** (MCP instance
  `WarehouseVRTraining` on the **`mcpforunity`** server; older sessions used a `UnityMCP` server). Scene/prefab/script
  work must land on main so Unity compiles/uses it — **cannot isolate in a git worktree**. Scripts can be written
  directly in the project, then `refresh_unity` (mode force, scope all, compile request) and check
  `read_console` for `error CS`. See memory `unity-worktree-constraint.md`.
- **`execute_code` is BROKEN** here (`mono.exe: filename or extension too long`; the Roslyn backend isn't
  installed either). Use `ReadMcpResourceTool` (`mcpforunity://scene/gameobject/{id}`) to inspect; for anything
  that needs code (mesh measurements, physics queries, Undo-safe reparenting), drop a temporary
  `Assets/Editor/*Temp.cs` with a `[MenuItem]`, run it via `execute_menu_item`, read `read_console`, then delete it.
- **Two editors may be connected** to the `mcpforunity` server (e.g. `social-universe`) — call
  `set_active_instance WarehouseVRTraining@238c2776` first or calls go to the wrong project.
- **Play mode + MCP:** `find_gameobjects`, `manage_components`, `manage_gameobject`, `manage_scene` (and asset tools)
  run a server "preflight" that does `refresh_unity(compile=request)` when Assets changed on disk — and entering Play
  mode rewrites `Assets/XR/Settings/OpenXR Package Settings.asset`, so the first such call recompiles **mid-play**
  (domain reload → AutoHand `HandBase.cs:383` NREs, non-serialized script state wiped). While playing, use only
  resource reads, `read_console`, `manage_camera` and `manage_editor`; set test values before pressing Play.
  Instance IDs survive entering Play mode, so look them up in Edit mode first.
- **Leaving Play mode can crash the editor** (Meta XR Simulator: `OpenXRLoaderBase.Deinitialize` →
  `Internal_PumpMessageLoop`, seen again 2026-09-23 on `manage_editor stop`; intermittent). Save before Play.
- **2026-10-06 incidents:** the PC went down mid-session (~07:55) during a forced `refresh_unity`; the git index was left
  zero-filled ("index file corrupt") and was rebuilt with `git read-tree HEAD` (nothing was staged; corrupt copy kept in
  the session scratchpad). Prefer `refresh_unity` mode `if_dirty`. After the restart the editor showed the
  **Windows/Mac/Linux** platform, not Android — check *Build Profiles* before building for Quest.
- **Stuck "iteration … of 9" on open / GUIDs regenerated — 2026-10-08.** `Library/SourceAssetDB` (LMDB) was corrupt
  (`MDB_CURSOR_FULL` / `MDB_BAD_VALSIZE` in Editor.log, most likely from the 10-06 power loss), so no import could be
  recorded: Unity re-imported the same assets 9 times, logged "An infinite import loop has been detected" and started
  over. One editor session (14:21) also gave **1,826 .meta files new GUIDs**, breaking every scene/prefab reference to
  them. Repaired: tracked metas got their `guid:` line back from git HEAD; the 124 untracked ones (cart/packing scripts,
  MergedMeshGroup, the 88 merged pallet meshes…) were recovered from stale pages of the corrupt DB (records
  `<lowercase path>\0` + 16-byte nibble-swapped GUID; method validated on the 1,702 tracked metas with 0 wrong) and
  cross-checked against the references in `BasicScene.unity`. Only the guid line was changed. Pre-repair metas are in
  `_MetaGuidBackup_2026-10-08/`, the corrupt Library in `Library_corrupt_2026-10-08/` — delete both once the project
  opens clean. **If the loop comes back: check Editor.log for `MDB_` errors, close Unity, and check
  `git diff -U0 -- '*.meta' | grep '^-guid'` before anything gets saved.**
- **Controllers/hands not detected — FIXED 2026-10-08.** `Assets/XR/Settings/OpenXR Package Settings.asset` had been
  regenerated with every OpenXR feature off except `MetaXRFeature` (new fileIDs, defaults; most likely the 10-06 power
  loss). With no interaction profile, OpenXR exposes no controller devices, so the AutoHand rig (legacy `TrackedPoseDriver`
  + `XRHandControllerLink` → `InputDevices.GetDevicesAtXRNode`) got no pose or buttons; the build log said "At least one
  interaction profile must be added". Re-enabled the committed set through the OpenXR API — both groups: Oculus Touch +
  Meta Quest Touch Plus profiles, HandTracking, MetaHandTrackingAim, MetaXRFeature, MetaXRFoveationFeature; Android also
  MetaQuestFeature, AndroidXRSupportFeature, MetaXRSubsampledLayout, latency = input polling, buffer discards on.
  OpenXR validation: 0 issues for Standalone and Android. **If the hands stop tracking, check Project Settings ▸ XR
  Plug-in Management ▸ OpenXR ▸ Enabled Interaction Profiles first.**
- **Trainer stuck "walking" — FIXED 2026-10-06.** `TrainerController.MoveTo` looped until
  `remainingDistance <= stoppingDistance` (2.5) and then until it faced the camera within 0.5°, while the trainer was an
  unconstrained dynamic Rigidbody that physics kept nudging. M1_01's teleport walks the trainer to the spawn; after
  another module had run in the same session the trainer had drifted, hovered outside the stopping distance and
  `IsWalking` stayed true forever — every modal waits on it, so the whole flow stalled. Fix (framework, additive):
  `TrainerController.Awake` makes its Rigidbody kinematic (the NavMeshAgent drives it); a walk now ends on arrival,
  after 2 s without getting 5 cm closer, or after `2 × distance / speed + 5 s` (`TrainerWalkMonitor`, plain C#, tests:
  `dotnet run --project Tests/TrainerLogic`, 9 tests), logs a warning if it ended without arriving, and clears the
  agent's path; turning to face the player compares yaw only and gives up after `_faceTimeout` (1.5 s); a new walk
  replaces a running one; `OnDisable` clears `IsWalking`. Tunables: `_walkStallSeconds`, `_walkExtraSeconds`,
  `_faceTimeout` on the trainer. Verified in Play mode: the M2 walkthrough (88/88) then Order Picking in the same
  session — M1_09 smoke 7/7, trainer stays put (it previously stalled).
- **2026-10-06, `mcpforunity` "connection timed out":** Unity had a modal **"Low disk space"** dialog open, which blocks
  the editor's main thread, so every bridge command timed out (the log shows `Command TCS timed out (N consecutive)`).
  Check for a modal dialog in the editor before debugging MCP itself.
- `Invalid AABB inAABB` asserts (uGUI `Graphic.cs:716`) appear during module runs — pre-existing (they occur with the M2 QC
  panel disabled too); the `LLMHandler` 401s at module completion are the AI evaluation without an API key.
- `manage_gameobject modify` with `parent: ""` should move to the scene root, but an empty string may not survive the
  client's JSON — use `Undo.SetTransformParent(t, null, …)` from a temp menu script instead. `create` positions are
  local to `parent`; `modify … parent` keeps world pose. After adding a component, re-read the object — a transient
  bridge disconnect once applied an `add` twice (a stray default 1 m BoxCollider).
- **`find_gameobjects` is EXACT full-name match** (not substring); supports
  `include_inactive`. Actual names include suffixes e.g. `Aisle_B_Shelf_SVG (2)`.
- **`set_property` cannot wire UnityEvents** (Generic type error) — use direct object-ref
  fields (see §3). Object refs settable via `{"instanceID": <id>}`.
- **`manage_gameobject modify` treats `position` as LOCAL** even with `world_space:true`
  — pass parent-relative coords.
- **`manage_scene get_hierarchy` ignores its `target` param** (always returns roots) —
  read children via the gameobject resource.
- Set TMP text via property `m_text`. Adding same-type components appends by index
  (existing=0, new=1,2,3…). Instance IDs can change across domain reloads — re-find by name.
- **Push access:** `Ej-Naranjo` can't push to origin; no `gh` CLI (memory
  `git-push-access.md`).

---

## 8. Related Memory Files

`~/.claude/projects/.../memory/`: `MEMORY.md` (index), `m1-order-picking-state.md`, `m2-packing-dispatch-state.md`,
`valari-challenge-framework.md`, `unity-worktree-constraint.md`, `git-push-access.md`.
