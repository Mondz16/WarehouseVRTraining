## Valari Foundry Training (UPM package)

This package contains the training runtime scripts (Managers, training UI/modals, TrainerController, and the in-project utility/service code they depend on) extracted from the Foundry Training project.

### Install
- Add the folder `Packages/com.valari.foundrytraining.training` (package name: `com.valari.training.template`) to source control, or publish it to a Git URL and add it via Unity Package Manager.

### External dependencies (not included)
This package intentionally does **not** include third-party packages such as Meta XR SDK, AutoHand, etc. Install your required dependencies in the target project first.

From the current code references, you will likely need (depending on which features you use):
- **Meta XR SDK** (e.g. `com.meta.xr.sdk.all`) for `Meta.XR.*` usages.
- **Odin Inspector** (`Sirenix.*`) for `[Button]`, `[ShowIf]`, `[ReadOnly]` usages.
- **DOTween** (`DG.Tweening`) for modal animations.
- **JsonFx** (`Pathfinding.Serialization.JsonFx`) for JSON serialization usage.
- **NaughtyAttributes** for `[ShowIf]` in some ScriptableObjects (if you keep those types).
- **AutoHand** (`Autohand`) for grab/challenge trigger scripts (e.g. `ChallengeTutorialTrigger`).
- **Outline** component dependency (whatever package provides `Outline` in your project) for challenge highlighting.

If you install the above first, the package should compile without modification (strict dependency mode).

### Samples
After installing, open **Package Manager → Valari Foundry Training → Samples** and import **Import Test** to validate the package import.

