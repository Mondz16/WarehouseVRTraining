# Module 2 — Packing and Dispatch — Design

**Date:** 2026-10-06 · **Module:** M2 Packing and Dispatch (`TrainingID.PackingAndDispatch`) · **Scene:**
`Assets/Scenes/BasicScene.unity` · **Canonical flow:** `Assets/ReferenceData/training_flow_module2.json` (M2_01–M2_11)

Also in scope: restore the missing **M1_09 Assessment** panel (Order Picking regression found while surveying).

## Goal

Every M2 step is playable and gated like M1: each challenge completes from a physical action at a packing
station, the module ends with the 12-question assessment (≥80 % to pass), and the whole flow can be driven
end-to-end in Play mode without a headset.

## Decisions (approved 2026-10-06)

| Question | Decision |
|---|---|
| Where | A new packing station on the open floor in front of the loading-dock door (east side, x≈15–20, z≈9–16). Trainee stands at x≈15.2 facing +x (the dock). |
| Box selection | Real-size cartons on a rack; the trainee puts one on the packing spot. Only the correct size locks; a wrong one shows why and returns to the rack. |
| Void fill | Grab air pillows from a dispenser and drop them into the carton; each is absorbed into a rising fill level (no pile of physics pillows). Base 2 → item(s) → top 3. |
| Sealing | Tape gun swept along 3 seams (H pattern). Flaps close automatically when the box is full. |
| Label | Scan the printed label (readout: address / weight class / carrier ✓), then press it onto the top face. It only sticks after a verified scan and once the box is sealed. |
| Conveyor | Released in the load zone with the label face up (≤35° tilt) → it rides off toward the dock. Otherwise "Label must face up". |
| Multi-item (M2_08) | Second order of 3 items; correct = one Large carton, all 3 items in, void fill full. No second seal/label/conveyor pass. |
| Error identification (M2_09) | Pre-packed QC box (sealed, labelled, under-filled). Open it by cutting the centre seam with the box cutter ("inspect tool"), press **Void fill** on the QC panel; then fill to full and re-tape the centre seam. |
| Assessment (M2_10) | Reuse the M1_09 pattern (`AssessmentLauncher` + `AssessmentChallengeGate` + `AssessmentFailPanel`); quiz shown at the station. |
| Not doing | Fragile-placement rule (quiz covers it), shake test, real product models (printed retail-box placeholders), new voice-over. |
| Git | Nothing is committed unless the user asks (the tree holds ~150 uncommitted files from earlier work). |

## Existing state (before this change)

- `TutorialData` assets `Assets/SO/TutorialData/M2_01…M2_11` with voice-over (`Assets/GeneratedAudio/PackingAndDispatch-25…35.wav`)
  and challenge names; `M2_10_Assessment` has **no** challenge yet (M1_09 had the same gap).
- 12 assessment questions under `AssessmentDataCollectionSO` ID 3 (`PackingAndDispatch`).
- 11 M2 panels already exist under `Canvas/SafetyPreparation` (inactive, all at the spawn, world (−0.05, 1.29, 25.0)),
  each with `_tutorialData`, `_hasChallenges`, `_challengeHolder`, `_challengeToggle` set and an **empty**
  `_challengeTutorialTriggerList`. They are already in `TutorialManager._tutorialFlowList[PackingAndDispatch]`.
- `TutorialManager` (`-----Manager-----/TutorialManager`): `_autoStartTraining = OrderPicking`, start position (0, 0, 26.22).
  No panel teleports; the trainer stays at the spawn; its voice AudioSource is 2D (heard anywhere).
- Panels whose task is far from the modal use `_enableAutoNext` (M1_03, M1_06).
- The quiz UI (`Canvas/AssesmentModalUI`) is fixed at the spawn (−0.05, 1.29, 25.0).
- **M1_09 is gone from the saved scene** (not in `Canvas/OrderPicking`, not in the OrderPicking flow list; the M1
  `AssessmentChallengeGate` on `AssessmentManager` has `_challengeTrigger` / `_showOnPass` = null). M1 currently goes
  M1_08 → M1_10 with no quiz.
- The east floor in front of the dock door has no colliders and no props in x 8–21, z 8–17 (the SVG `Packing`
  placeholders are elsewhere and are left alone). The floor tile has a collider.

## Station layout (world coordinates, y up; trainee faces +x)

```
            z 16  ┌──────────────┐  M2 modal (M2_03–08, 10, 11) at ≈(16.3, 1.45, 16.4), angled to the trainee
                  │ CARTON RACK  │  top y 0.75 — Small z14.45 · Medium z14.8 · Large z15.45 (length along x)
            z 14.3└──────────────┘
                  ┌──────────────┐  BENCH top y 0.90, x 15.6–16.4, z 11.8–14.2
                  │ scanner      │  order panel above the back edge (16.45, 1.45, 13.0)
                  │ PACKING SPOT │  spot centre (15.85, 0.90, 13.2), item display strip x≈16.25
                  │ tape gun     │
                  │ pillows/printer│  dispenser (15.85, 0.90, 12.15) · label printer (16.2, 0.90, 12.05)
            z 11.8└──────────────┘
   CONVEYOR  x 15.4 → 20.4, z 11.0–11.6, belt top y 0.85 — load zone = first metre — runs toward the dock
                  ┌──────────────┐  QC TABLE top y 0.90, x 15.6–16.4, z 9.4–10.6; QC box (15.95, 0.90, 10.0);
            z 9.4 └──────────────┘  QC panel (16.45, 1.35, 10.0); box cutter front-right; M2_09 modal south of it
   signs (gaze targets, y≈1.95): CARTONS over the rack · WORK AREA over the order panel · LABELS & CONVEYOR over z 11.7
```

Positions are drafts to tune in-headset. Everything is built from primitives + URP Lit materials by a re-runnable
editor builder (see Build).

## Step behaviour

| Step | Modal | Trainee does | Completes when (trigger) |
|---|---|---|---|
| M2_01 | spawn | — | no challenge |
| M2_02 | spawn, **auto-next** | walks to the station, looks at the 3 zone signs | each sign is found after 0.5 s of gaze within 12° and 4.5 m; 3 found → `TRIGGER_M2_StationLayoutConfirmed` (MultiCounterGate, required 3) |
| M2_03 | station | puts a carton on the packing spot | correct size for order 1 locks → `TRIGGER_M2_CorrectBoxSelected` |
| M2_04 | station | pillows → item → pillows | fill ≥ 2 before the item → `…VoidFillBase`; item in + 3 more → full → `…VoidFillTop` (in order) |
| M2_05 | station | tape gun along centre + both end seams | all 3 seams taped on the closed box → `TRIGGER_M2_BoxSealed` |
| M2_06 | station | scan label, press it on the top | verified scan → `…LabelVerified`; label applied → `…LabelApplied` (in order) |
| M2_07 | station | carries the box to the conveyor | accepted in the load zone label-up → `TRIGGER_M2_BoxOnConveyor`; the box rides off and is dispatched, which starts order 2 |
| M2_08 | station | packs order 2 | Large carton locked + 3 items + fill full → `TRIGGER_M2_MultiItemPacked` |
| M2_09 | QC table | cuts centre seam, answers, fills, re-tapes | correct answer on the open box → `…ErrorIdentified`; fill full + all seams taped → `…ErrorCorrected` (in order) |
| M2_10 | station | quiz (shown at the station) | ≥ 80 % (10/12) → `TRIGGER_M2_AssessmentComplete`; fail → fail/retry screen |
| M2_11 | station | — | no challenge; Next ends the module (framework teleports to start) |

Every bridge is arm-aware (`ArmedLatch` + `ChallengeTutorialTrigger.Armed`), like M1: an action done before its
step opens counts when the step opens.

## Orders and the fit rule

Carton inner sizes (cm): Small 30×20×15, Medium 45×35×25, Large 60×50×40. **Fit rule:** the smallest carton whose
usable size (inner − 2 × 5 cm clearance on every axis) holds the order's packed size, both sorted descending
(rotation allowed).

| Order | Items | Packed size | Correct |
|---|---|---|---|
| ORD-2001 | Wireless Speaker 24×14×10 cm, 1.2 kg | 24×14×10 | **Medium** — Small usable 20×10×5 is too small although the speaker physically fits Small |
| ORD-2002 | Desk Lamp 30×12×12 · Hardcover Book Set 24×17×8 · Cable Kit 15×10×6, 2.9 kg | 32×30×14 | **Large** — Medium usable 35×25×15 is too narrow |
| QC ORD-1874 | Ceramic Mug Set 20×20×12 cm, 1.6 kg — packed in a Medium | — | error = void fill (1 of 5) |

Wrong-carton messages explain the reason (too small / oversized). Addresses, carriers and names are fictional.

## Components

Plain C# (no UnityEngine), unit-tested — `PackingLogic.cs`:
- `CartonSize` enum + `CartonFit` (usable size, `Fits`, `SmallestFit`, reason text inputs).
- `VoidFillState` (base / items / top / full phases; `TryAddFill`, `TryAddItem`, `Phase`, `IsBaseMet`, `IsFull`).
- `SeamCoverage` (bins along a segment; `Mark(t)`, `Fraction`, `IsComplete`, `Reset`).
- `SegmentMath` (closest-point parameter + distance for a point near a segment, in carton-local metres).
- `GazeDwell` (angle/distance gate + dwell timer that resets when the gaze leaves).
- `Orientation` (`IsFaceUp(upY, maxTiltDeg)`).
- `BoxPose` containment (`ContainsLocal` for an interior box with tolerance).

MonoBehaviours (`Assets/Scripts/ImmersiveTraining/TrainingInteractions/Packing/`), all wired with direct refs:
- `PackingCarton` — the stateful box: size, fill state, packed items, flaps open/closed, 3 seams, label, lock/unlock,
  `Cuttable` (QC). Polls registered pillows/products for release inside the interior, applies the rules, snaps
  accepted items, absorbs pillows, sweeps seams from registered `SeamTool`s, raises C# events on every change.
- `PackingStation` — the bench: orders, packing spot, carton supply (respawns a carton when one is used), order
  panel text, feedback line, step bridges for M2_03/04/05/06/08, order switch on dispatch.
- `VoidFillDispenser` + `VoidFillPiece` — pooled grabbable air pillows; absorbed/rejected pillows return to the pool.
- `PackableItem` — product identity + home pose (rejected items go home).
- `SeamTool` — tape gun / box cutter head (registry).
- `ShippingLabel` — label card; `IScanListener` (verified by the scanner); sticks to a sealed carton's top face.
- `IScanListener` + a 3-line addition to `ScannerTool.Scan` (notify listeners on the scanned item). M1 unaffected.
- `ConveyorLoadZone` — load-zone check, belt transport, dispatch; bridge for M2_07.
- `GazeDwellTarget` — sign that turns green when found; reports to a `MultiCounterGate`.
- `QcReview` — QC panel buttons (bound in code), answer check, bridges for M2_09.
- `PackingFeedback` — one TMP line for station messages (green/red, fades).
- `AssessmentLauncher` gains optional `_quizRoot` + `_quizAnchor`: moves the quiz UI to the anchor while its step
  runs and puts it back when the panel closes (other modules keep the spawn position).

## Build

`Assets/Editor/PackingStationBuilder.cs` (kept, menu **Tools ▸ Packing Dispatch**):
1. **Build Prefabs** — `Assets/Prefabs/PackingDispatch/` cartons ×3, air pillow, products ×5, label, tape gun,
   box cutter; materials in the same folder.
2. **Build Station** — (re)creates the `M2_PackingStation` root (deletes the previous one first) with bench, rack,
   conveyor, QC table, signs, order/QC panels, scanner, tools, items, QC carton; adds the M2 trigger objects under
   the M2 panels, fills each panel's trigger list, moves the panels, sets M2_02 auto-next, wires every direct ref,
   updates `M2_10_Assessment.asset` (one challenge) and adds the M2 assessment gate.
3. **Restore M1_09** — duplicates the M1_08 panel as `M1_09_Assessment`, wires it like before (launcher, gate,
   trigger, hide-during-quiz) and inserts it before M1_10 in the OrderPicking flow list.

Builder steps support Undo and log a `[PackingBuilder]` summary. Rebuilding the station discards manual tweaks to it.

## Testing

- `dotnet run --project Tests/PackingDispatchLogic` — unit tests for `PackingLogic.cs` (+ `ArmedLatch` scenarios).
- Play-mode walkthrough (editor driver `Assets/Editor/PackingDispatchPlaytest.cs`, kept as a tool): starts `PackingAndDispatch`, performs every step by moving
  objects / calling the same entry points the hands use, presses Next, and asserts each trigger completes in order,
  including the wrong-carton, item-before-fill, label-before-scan, label-down and wrong-QC-answer paths.
  Also runs the M1 flow far enough to confirm M1_09 launches its quiz.
- In-headset checklist added to CLAUDE.md (grab feel, pillow drops, tape sweep, label press, carrying the box).

## Error handling

- Missing refs log one warning (`#Component#` prefix) and disable that bridge instead of throwing.
- Items or pillows lost on the floor stay grabbable; rejected ones return to their home / pool.
- The carton rack restocks a size only when its carton is used (locked on the spot); a refused carton is moved back
  to its rack slot after 1.5 s, so wrong picks never pile up cartons.
- `PackingStation` ignores the carton spot until the M2 module has started (`OnTrainingStartedEvent`).
