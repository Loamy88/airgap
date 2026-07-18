# AIRGAP — Scripting & Data Conventions

Phase 0 Part B deliverable. Everything Claude Code writes into this repo follows these rules.

## Namespaces

| Namespace | Folder | Contents |
|---|---|---|
| `AIRGAP.Infiltrator` | `Assets/Infiltrator` | Player One: controller, stance/noise, gadgets, blueprint-knowledge UI |
| `AIRGAP.Warden` | `Assets/Warden` | Player Two: dashboard screens, order UI, sensor/camera views |
| `AIRGAP.Facility` | `Assets/Facility` | The building: blueprints, rooms/anchors/doors, guards, technicians, sensors, LUMEN |
| `AIRGAP.Shared` | `Assets/Shared` | Cross-cutting: data loading, sound-event bus, netcode bootstrap, match flow |
| `AIRGAP.CI` | `Assets/Shared/Editor/CI` | Editor-only, `-batchmode`-runnable: validators, builders, scene/package bootstrap |

Rules:
- One top-level folder per namespace; sub-namespaces mirror subfolders (e.g. `AIRGAP.Facility.Guards` ↔ `Assets/Facility/Guards`).
- Editor-only code lives under an `Editor/` subfolder (Unity's special-folder rule) — never in runtime folders.
- No `Assembly-CSharp` splitting via asmdefs for the prototype; a single assembly keeps cross-system references friction-free. Revisit if compile times hurt.
- The Warden must never gain compile-time access to Infiltrator ground truth via "convenience" references — anything crossing the Infiltrator/Warden boundary goes through `AIRGAP.Shared` events/state, which is also what keeps the Phase 16 network split honest.

## Code layout

- One public type per file; file name matches the type. **Exception:** a JSON data-contract module may bundle its schema classes in one file named for the contract (`BlueprintData.cs` holds the blueprint/assignment/dressing POCOs; `BlueprintRaster.cs` carries its `RasterCell` enum) — schema classes are read together, edited together, and versioned together.
- MonoBehaviours are thin: input polling, rendering, Unity lifecycle only. Game rules live in plain C# classes that a `-batchmode` test can instantiate without a scene.
- State machines (guard alertness, consciousness, power room) are explicit enum + transition-table types, not `bool` soup — the DEVELOPMENT.md validators assert on transitions.
- Determinism where the design demands it: `EvaluateOrder`, sensor triggers, and badge checks take no `Random` and no wall-clock — randomness only enters via the round seed (Phase 12) and the hearing roll (Phase 2), each behind an injectable RNG.

## Serialization & data

- All design data is JSON under `Assets/Shared/Data/`, loaded via Newtonsoft (`com.unity.nuget.newtonsoft-json`). Unity's `JsonUtility` is not used for design data (no dictionary support, silent field drops).
- Every data file carries `schemaVersion` (int) and `description` (string) at the top level.
- IDs are kebab-case strings (`"pry-bar"`, `"map-fragment"`); display names are separate fields. Guard display IDs follow `G-{00}`.
- Numbers that are Phase 17 tuning surfaces stay in JSON — never inline constants in C#. If a script needs a magic number, it belongs in a data file with a name.
- Structures the design forbids are unrepresentable, not discouraged: e.g. `LumenAlert` has no position field at all (Phase 9), the Infiltrator's map UI renders only the `InfiltratorKnowledge` store (Phase 12). Validators assert these properties.

## Input

- New Input System (the Unity 6 template default), action maps authored as data in `Assets/Settings/AirgapControls.inputactions`. Active input handling is set to *Both* so legacy `Input.GetAxis` remains available for quick prototyping.
- Binding reference: [docs/CONTROLS.md](CONTROLS.md).

## CI / batchmode entry points

All CI methods are `public static void` on classes in `AIRGAP.CI`, invoked as:

```
Unity -batchmode -nographics -projectPath . -executeMethod AIRGAP.CI.<Class>.<Method> -logFile Logs/<name>.log
```

- Validators print `[AIRGAP.CI] ...` lines and exit non-zero on failure (`EditorApplication.Exit(1)`).
- Current entry points:
  - `AIRGAP.CI.PackageBootstrap.InstallNetcode` — one-shot NGO install
  - `AIRGAP.CI.ValidatePhase0.Run` — data-schema integrity checks
  - `AIRGAP.CI.SceneBootstrap.CreateBootstrapScene` — (re)generate the network bootstrap scene + player prefab
  - `AIRGAP.CI.Build.WindowsPlayer` — headless Windows 64-bit player build to `Builds/Windows/`
  - `AIRGAP.CI.GreyboxScene.Create` — (re)generate the Phase 1/2 grey-box test scene (`Assets/Scenes/Greybox.unity`)
  - `AIRGAP.CI.ValidatePhase1.Run` — scripted movement sequence: stances, speeds, footsteps, vent traversal
  - `AIRGAP.CI.ValidatePhase2.Run` — light sampling/occlusion, flashlight self-light, vision categories, hearing curve
  - `AIRGAP.CI.ValidatePhase3.Run` — blueprint authoring validator: schema sanity, connectivity, patrols, anchors, peek/vent rules, assignment coherence
  - `AIRGAP.CI.BlueprintScene.Create` — build `Assets/Scenes/Blueprint01.unity` from blueprint JSON + role assignment (the Phase 12 seam)
  - `AIRGAP.CI.ValidatePhase4.Run` — guard behavior + order intake: predicate matrix, gate, source blindness, TTL, patrol/chase/capture simulation
- Headless simulation pattern: gameplay components expose `EnsureInitialized()` + `Tick(dt)`; validators set `Physics2D.simulationMode = Script`, drive ticks, and step `Physics2D.Simulate` themselves. World state queries are poll-based (no trigger callbacks) so play mode and batchmode behave identically.
- Machine-local paths (editor path, project path) live in `.env` (gitignored); see `.env` on this machine.

## Git

- Commit directly to `main`, push after each coherent unit of work.
- Unity-standard `.gitignore`; `Assets/**/*.meta` files are always committed.
- A round is reproducible from `(blueprintId, seed)` — bug reports should carry both (Phase 12).
