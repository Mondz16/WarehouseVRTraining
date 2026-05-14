# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a Unity 3D warehouse operations training application using the Universal Render Pipeline (URP). It supports both PC (mouse-based) and XR/VR (Meta Quest, OpenXR) delivery.

The main training scene is `Scenes/urp_scene_with_training.unity`.

## Architecture

### Event System

All cross-system communication flows through `EventManager` (a Singleton). Events are typed via the `EventTypes` enum in `EventManager.cs`. All events carry a `GameObject` argument.

```csharp
EventManager.StartListening(EventTypes.STATE_CHANGED, myHandler);
EventManager.TriggerEvent(EventTypes.TRAINING_STATE_CRITERIA_REACHED, gameObject);
EventManager.StopListening(EventTypes.STATE_CHANGED, myHandler);
```

Always call `StopListening` in `OnDestroy` or `DeactivateState` to avoid stale listeners.

### State Machine

Training progresses through a list of `StateData` MonoBehaviours managed by `StateManager_Training`.

**Inheritance chain:**
```
StateData
  └─ StateActivateAndDeactivateObjects   (activates/deactivates tagged scene objects)
       ├─ StateTrainingWithPass           (interactive step: part dragging, ghost, timer)
       └─ StateActivateWithShowcasePosition  (moves an object to a showcase position)
```

- Each state lives as a child GameObject in the scene; `StateManager_Training._stateData` is a serialized list.
- `ActivatedByState` is a tag component — any GameObject with it will be toggled by state transitions. Objects listed in a state's `_objectsToActivate` are shown; all others are hidden. Missing `ActivatedByState` generates a console warning.
- `HideInsteadOfDeactivate` makes a state object toggle its renderers/colliders rather than `SetActive`.
- `IUseTimer` interface marks states that have a time limit; `StateData.ActivateState` fires `START_TIMER` automatically when it's present.

### Training Modes

`TrainingTypeManager` controls two modes:

| Mode | Key | Behavior |
|------|-----|----------|
| Training | `T` | Step-by-step, ghost parts shown, countdown timer, only current part movable |
| Independent/Free | `F` | All parts freely draggable, no countdown |
| Toggle | `B` | Flip between modes |
| Reset | `R` | Restart from step 0 |
| Attract | `A` | Auto-demo: auto-completes each step on a timer, resets when any input is received |

### Part Interaction

`TrainingPartMover` (added/removed dynamically at runtime) handles mouse drag on the active training part:

1. Mouse down → raycast → lock part
2. Mouse drag → project onto a `Plane` (either a `_referencePlane` Transform or a drag-axis plane)
3. Mouse up → fire `TRAINING_STATE_COMPLETION_CHECK`
4. `StateTrainingWithPass.CheckForStateCompletion` compares distance to `_originalPartToMoveAssembledTransform`; if within `_distanceToConsiderCorrect`, fires `TRAINING_STATE_CRITERIA_REACHED`

### Singleton Pattern

Managers inherit from `Singleton<T>`. Access via `MyManager.Instance`. Do not rely on `Instance` before `Awake` completes; prefer event-based communication when initialization order is uncertain.

### Time Logging

`TimeManager` tracks per-step and total time and writes JSON via `JsonFileUtility`:
- In Editor: written to `Application.dataPath`
- In Build: written to `Application.persistentDataPath`
- On `TRAINING_COMPLETE`: file is saved with a datestamp suffix

## Namespaces

| Namespace | Location | Purpose |
|-----------|----------|---------|
| `ImmersiveTraining.Management` | `Scripts/ImmersiveTraining/Management/` | Core singletons and managers |
| `ImmersiveTraining.StateHandling` | `Scripts/ImmersiveTraining/StateHandling/` | State machine classes |
| `ImmersiveTraining.TrainingInteractions` | `Scripts/ImmersiveTraining/TrainingInteractions/` | Interaction components |
| `DistributionCenter` | `Scripts/DistributionCenter/` | Scene-specific utility scripts |

## Key Packages

- **Input System** (`com.unity.inputsystem` 1.18.0) — all input uses the new Input System (`Mouse.current`, `Keyboard.current`), not legacy `Input`
- **URP** (`com.unity.render-pipelines.universal` 17.3.0)
- **TextMeshPro** (via `com.unity.ugui`) — all UI text uses TMP
- **Cinemachine** (`com.unity.cinemachine` 3.1.3) — use the v3 API
- **XR Interaction Toolkit** (`com.unity.xr.interaction.toolkit` 3.3.1) + Meta XR SDK (`com.meta.xr.sdk.all` 83.0.0)
- **DOTween Pro** (in `Plugins/Demigiant/`)
- **AutoHand** (in `Plugins/AutoHand/`) — VR physics-based hand interaction

## Adding a New Training Step

1. Create a child GameObject under the `StateManager_Training` object.
2. Add `StateTrainingWithPass` (or another `StateData` subclass) component.
3. Assign `_partToMove`, `_originalPartToMoveAssembledTransform`, `_referencePlane`, `_ghostPrefab`, and instruction assets.
4. Add the step's visible objects to `_objectsToActivate` and ensure they each have `ActivatedByState` component.
5. Drag the new state GameObject into `StateManager_Training._stateData` list at the desired index.

## Patterns to Follow

- New managers should extend `Singleton<T>`.
- Scene-to-scene communication uses events via `EventManager`; direct references stay within the same system.
- State-driven visibility uses `ActivatedByState` + the state's `_objectsToActivate` list — do not directly call `SetActive` on tagged objects outside of state transitions.
- `TrainingPartMover` is added/removed at runtime; do not assume it persists between state activations.
