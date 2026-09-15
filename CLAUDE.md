# WarehouseVRTraining — Project State & Task Tracker

> Living doc for tracking build progress on the VR warehouse-training app.
> Last updated: 2026-09-07. Keep the **Task Tracker** and **Wiring Status** tables current as work lands.

---

## 1. Project Overview

Unity VR warehouse-worker training app. Modules are guided by the **Valari tutorial
framework** — each step shows a modal panel; some steps have *challenges* that the
trainee completes by physically interacting with the scene, which advances the flow.

- **Engine:** Unity `6000.3.12f1`, URP.
- **XR:** Meta Quest / OpenXR, **AutoHand** for VR grabbing, new Input System, TextMeshPro.
- **Main scene:** `Assets/Scenes/BasicScene.unity`.
- **Active module:** Module 1 — **Order Picking (M1)**.
- **Canonical M1 flow:** `Assets/ReferenceData/training_flow_module1.json` (10 steps, M1_01–M1_10).

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
| `ToteCounter` | Trigger volume; counts matching-SKU items dropped in. Fires on target count. |
| `MultiCounterGate` | Aggregates N `NotifyComplete()` calls (multi-line orders, M1_06). |
| `LocationZoneTrigger` | Trigger volume; fires once when a `TriggerIdentity` (default `"Player"`) enters (navigation, M1_03). |
| `TriggerIdentity` | Tags an object with an `Id` string (player rig carries `Id="Player"`). |
| `ScannerActivator` | Bridges AutoHand `Grabbable.OnSqueezeEvent` → `ScannerTool.Scan()` (squeeze held scanner to scan, M1_04). |
| `GrabChallengeTrigger` | Bridges AutoHand `Grabbable.OnGrabEvent` → `ChallengeTutorialTrigger.OnTriggerComplete()` (grab-to-open, M1_02; reusable). |
| `MisPickRecovery` | On the tote; fires a remove-trigger when the wrong-SKU item exits, then a replace-trigger when the correct-SKU item enters (ordered, M1_07). |
| `ScannerTool` | Right-hand scanner; `Scan()` verifies SKU. |
| `WristPickListUI` | Wrist pick-list display. |
| `TriggerEventByID` / `TriggerIdentity` | ID-based trigger helpers. |

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

---

## 4. Prefabs & Scene Objects

**Prefabs** (`Assets/Prefabs/OrderPicking/`):
- `Tote_OrderPicking` — BoxCollider trigger + ToteCounter (SKU-1001 ×3).
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
`B3/B4/B5/B6`→RubberGasket40, `PickArea_C/M1_Prod_C1`→SteelBracket90, `M1_Tote/M1_MisPick_Wrong`→RedWidget500,
`M1_MisPick_Correct`→GreenWidget500 — each kept its exact position/rotation/scale (0.15) + kinematic flag; SKU-based
tote/scan/mis-pick logic unaffected. **Duplication fixed:** the A-aisle previously had a duplicate `Bin/` set
(y0.571) alongside `lower/` (y0.182) — the `Bin/` copies were deleted so counts are correct (3× SKU-1001, etc.).
Only `M1_PathReview_Confirm` (the M1_08 confirm marker, not a product) is still a cube — swap or retire it separately.
Instantiate a product prefab via `manage_gameobject create` with `prefab_path`+`save_as_prefab:false`.

**Scene** (player start ≈ -10,0,24; picking area to west / -X):
- Shelves: `WarehouseLayout_FromSVG/Picking/Aisle_{A,B,C}_Shelf_SVG (2)`
  (A z0.81, B z7.53, C z14.09) + `PickPos_A/B/C` markers.
- A-aisle (under root `lower`): `M1_Prod_A1/A2/A3` (SKU-1001),
  `M1_Decoy_1/2` (SKU-2002), `Bin_A0203`.
- B/C-aisle: `PickArea_B` → `Bin_B0102`, `Bin_B0204`, `M1_Prod_B1/B2` (SKU-2002),
  `M1_Prod_B3/B4/B5/B6` (SKU-2007); `PickArea_C` → `Bin_C0301`, `M1_Prod_C1` (SKU-3003).
- `M1_Tote` — 4 ToteCounters (see M1_06 wiring), `M1_Scanner`.

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
No teleport is used (`_teleportPlayer=false`), so the trainee walks freely.

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
| M1_06 | Multi-Line Order | `TRIGGER_M1_MultiLine_AllPicked` | ✅ wired (chain below) |
| M1_07 | Error Recovery | `TRIGGER_M1_RemoveMisPick`, `TRIGGER_M1_ReplaceCorrectItem` | ✅ wired (chain below) |
| M1_08 | Path Efficiency Review | `TRIGGER_M1_PathReviewConfirmed` | ✅ wired — real route overlay + auto-complete ~1.5s after it shows (grab marker retired; chain below) |
| M1_09 | Assessment | `TRIGGER_M1_AssessmentComplete` | ✅ wired — ≥80% pass gate + modal hidden during quiz + fail/retry screen (chain below) |
| M1_10 | Complete | *(none)* | ✅ no challenge |

**M1_06 chain (verified):**
```
3 lines dropped in M1_Tote
  → each line's ToteCounter fires _gate.NotifyComplete()
  → M1_MultiLineGate (_requiredCount=3) fires _challengeTrigger
  → Trigger_M1_MultiLine (name TRIGGER_M1_MultiLine_AllPicked)
  → M1_06 panel challenge complete
```
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
`OnTriggerComplete()` call — which is why every step uses a code-bridge/collider hook.

**M1_07 chain (verified) — two ordered challenges:**
```
remove: pull SKU-5099 item out of M1_Tote (Collider exit)
  → MisPickRecovery.OnTriggerExit → Trigger_M1_RemoveMisPick (TRIGGER_M1_RemoveMisPick)
then replace: drop SKU-5005 item into M1_Tote (Collider enter, only after removal)
  → MisPickRecovery.OnTriggerEnter → Trigger_M1_ReplaceCorrectItem (TRIGGER_M1_ReplaceCorrectItem)
  → both in M1_07 panel list → step completes
```
`MisPickRecovery` on M1_Tote (shares its trigger BoxCollider), `_wrongSku`=SKU-5099,
`_correctSku`=SKU-5005, `_requireRemoveBeforeReplace`=true. Dedicated pickables (cloned from
`lower/M1_Prod_A1`, kinematic): `M1_MisPick_Wrong` (SKU-5099 "Red Widget 500ml") child of
M1_Tote at local (0,0.25,0) ≈ world (1.889,0.25,2.19), pre-placed inside the tote;
`M1_MisPick_Correct` (SKU-5005 "Green Widget 500ml") child of M1_Tote at local (0.36,0.95,0)
≈ world (1.889,0.95,2.55), floating in front within reach. SKU-5099/5005 are watched by no
ToteCounter → zero interference with M1_05/06. **Headset tuning TODO:** item heights are drafts
(correct item floats — add a stand/shelf spot); confirm kinematic items are grabbable.
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

Optimal waypoints (world XZ, floor markers, tunable): Start (−10.045, 23.954) → A-02-03 (0.3, 2.8) → Tote
(1.889, 2.19) → B-01-02 (−0.3, 9.0) → B-02-04 (0.5, 9.0) → C-03-01 (0.157, 15.562) → Tote — the "do all of
one aisle before the next" optimal. **Headset tuning:** waypoint positions, line Y/width/colours are drafts.
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
  calls `RetakeAssessment()`.
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

---

## 6. Task Tracker

**Wiring — ✅ ALL COMPLETE (M1_02–M1_09; M1_01/M1_10 have no challenge):**
- [x] **M1_03** — navigation trigger DONE (Zone_A0203 → TRIGGER_M1_ReachLocation_A0203).
      Runtime walk-in test pending in-headset.
- [x] **M1_07** — error recovery DONE (MisPickRecovery on M1_Tote; remove SKU-5099 then
      add SKU-5005). Runtime remove-then-replace test + item-height tuning pending in-headset.
- [x] **M1_08** — confirm trigger DONE (grab-marker → TRIGGER_M1_PathReviewConfirmed) AND the real
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

**⏳ In-headset test checklist (do while testing — none of these block the build):**
- [ ] M1_02 — grab the wrist device → pick list opens/completes.
- [ ] M1_03 — walk into Zone_A0203 → step completes.
- [ ] M1_04 — squeeze the held scanner at the SKU label → green verify → completes.
- [ ] M1_05 — drop exactly 3× SKU-1001 into the tote → completes.
- [ ] M1_06 — wrist list swaps to the 3-line B/C/B order; drop all 3 lines → completes.
- [ ] M1_07 — pull the SKU-5099 mis-pick out, then drop the SKU-5005 correct item in → completes.
- [ ] M1_08 — green optimal vs amber actual route lines draw on the floor; grab the Confirm marker → completes.
- [ ] M1_09 — take the 13-Q quiz; ≥11/13 → "Next"; <11/13 → fail/retry screen → Retake relaunches.

**⏳ In-headset tuning (needs the visual/VR — can't be done from MCP):**
- [ ] Raise `Bin_B0204` to shelf level 2, `Bin_C0301` to shelf level 3 (drafted low); nudge B/C onto shelf faces.
- [ ] Scanner grab-pose / muzzle direction.
- [ ] M1_07 mis-pick items & M1_08 confirm marker float — add stands; confirm the kinematic clones are grabbable.
- [ ] Drafts to tune: M1_08 route waypoint positions + line Y/width/colours; M1_09 fail-screen text size/layout.

---

## 7. Environment Gotchas (read before using MCP)

- **Live Unity editor runs on the `main` checkout** (MCP instance
  `WarehouseVRTraining` on the **UnityMCP** server). Scene/prefab/script work must land
  on main so Unity compiles/uses it — **cannot isolate in a git worktree**. Edit scripts
  via `mcp__UnityMCP__script_apply_edits` (writes to the main project). See memory
  `unity-worktree-constraint.md`.
- **`execute_code` is BROKEN** here (`mono.exe: filename or extension too long`). Use
  `ReadMcpResourceTool` (`mcpforunity://scene/gameobject/{id}`) to inspect instead.
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

`~/.claude/projects/.../memory/`: `MEMORY.md` (index), `m1-order-picking-state.md`,
`valari-challenge-framework.md`, `unity-worktree-constraint.md`, `git-push-access.md`.
