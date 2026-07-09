# AIRGAP — Development Plan

A staged build order for the prototype. Each phase should leave the game in a playable (if incomplete) state — the guiding rule is to prove the *feel* of a system with the cheapest possible fake before investing in the real version of it. This is most important for the guard-dialogue system (Phase 7), which is the riskiest and most novel piece.

## Setup Option: Unity Installed Locally

This plan assumes **Unity 2022 LTS (or later)** is installed and available via command line. This enables:
- **Headless script execution** — Claude Code can invoke Unity's `-batchmode` to run validation/generation scripts without opening the editor
- **Automated builds** — CLI builds for quick iteration testing
- **Real-time feedback** — minimal human editor work; changes are file-driven and validated automatically

If Unity is not yet installed: `choco install unity-hub` (Windows), or download from [unity.com/download](https://unity.com/download). Then install a 2022 LTS or later editor version from Unity Hub.

## Working with Claude Code
This project is meant to be built mostly by Claude Code rather than by hand, so the plan is shaped around that:
- **Data-driven over hardcoded.** Blueprints, room anchors, patrol loops, item pool entries, and gadget stats should live as data (JSON/YAML), not baked into scenes or magic numbers — an agent can generate, extend, and tune data files far more reliably than it can edit a scene.
- **Hand-authored layout, generated contents.** Facility floorplans are drawn by hand and shipped as data; only room roles, item/guard/sensor anchor selection, and door typing are rolled per round (Phase 11). This is the single biggest complexity saving in the project — it keeps the generator's output space small, enumerable, and cheap to validate.
- **Automated validation over manual playtesting for generation.** Blueprints are validated once at authoring time (connectivity, closed patrol loops); each round's role assignment is validated per-seed in CI. Both run as headless-mode C# validators, so Claude Code can verify its own output without human editor intervention.
- **Each checklist item below is meant to be handed over as its own scoped prompt** — small enough to implement and verify in one pass, with a clear "done" condition (compiles, passes CI validation, playable in isolation).
- **Hybrid workflow:** Claude Code writes all scripts and data; you open the editor only to (a) wire scenes together from pre-made prefabs, (b) hit Play to test feel, or (c) adjust balance numbers. No hand-crafting level layouts or scripting from the editor.
- **Flag-for-human items:** feel and balance tuning (Phase 16), and any "does this read as fun/tense" judgment call. Everything else — state machines, data schemas, UI wiring, procedural algorithms, validation — is fair game to drive end-to-end with Claude Code via command line.

## Phase 0 — Foundations & Tooling

### Part A: Unity Project Setup
- [ ] **CLI validation:** verify `Unity -version` returns a 2022 LTS or later version available in PATH.
- [ ] **Create new 2D URP project:** `Unity -createProject -quit -batchmode` with 2D template and URP enabled (stores project path in `.env` for later scripting).
- [ ] **Repo structure:** create `Assets/{Infiltrator,Warden,Facility,Shared}` folders and initial `.gitignore` (Unity-standard, excludes Library/Logs/Temp/Obj/Build).
- [ ] **ProjectSettings:** configure URP asset (already default in 2D template), confirm `Light2D` support enabled in URP settings.

### Part B: Data & Scripting Infrastructure
- [ ] **Data schemas:** author JSON definitions for guards, technicians, gadgets, facility config (placed in `Assets/Shared` as reference, not runtime-loaded yet).
- [ ] **Scripting conventions:** establish namespace structure (`AIRGAP.Infiltrator`, `AIRGAP.Warden`, `AIRGAP.Facility`, `AIRGAP.Shared`), code layout, and serialization patterns.
- [ ] **Validation harness:** create a C# console validator (can run in `-batchmode`) that checks data schema integrity — validates guard/gadget definitions, facility config sanity, and reports errors to stdout for CI.
- [ ] **Build test:** ensure the empty project compiles via `Unity -buildWindows64Player` (even if no game logic yet).

### Part C: Input & Control Definitions
- [ ] **Input Manager config:** define gamepad/keyboard axes for Infiltrator (move, sprint, aim) and Warden (screen-switch, click). Store in InputManager.asset.
- [ ] **Control schema file:** create a document (markdown or JSON) listing all input bindings for future reference.

### Part D: Basic Networking Scaffold
Two devices, always — no split-screen, no shared-machine mode (see Networking, in README.md, for why). This means a minimal network layer has to exist from Phase 0, not get retrofitted after the fact.
- [ ] **Install Netcode for GameObjects (NGO)** package.
- [ ] **One universal build, role chosen at connect time** — rather than shipping separate Infiltrator/Warden executables, a single build presents a host-or-join screen and a role pick, simplifying distribution to one artifact.
- [ ] **LAN direct-connect for dev-time:** basic `NetworkManager` setup using IP-based direct connect — no Relay/Lobby yet, just enough to prove two processes can talk. This is the foundation Phase 15 upgrades to Relay/Lobby later.
- [ ] **Smoke test:** two separate processes (two Editor instances on one dev machine for early testing, or two actual devices on the same LAN) connect, each resolves into the correct role, and a trivial synced value (a ping RPC) round-trips. Every later phase builds on this connection existing.

## Phase 1 — Grey-box Movement Prototype
Single test room, no AI yet — just prove movement and stealth *feel* before adding anything reactive to it. Claude Code generates all scripts; you create one test scene in the editor and hit Play to validate feel.
- [ ] **Infiltrator controller script:** handle crouch/walk/sprint stance switching, movement-speed lookup from config, input polling from InputManager.
- [ ] **Noise-radius system:** a Circle collider (debug-rendered in green) that scales with stance — size data comes from config JSON.
- [ ] **Vent/crawlspace layer:** a "traversal" tile type marked by a collider tag; Infiltrator can enter/exit these via input or automatic detection (data-driven: marked as "layerType": "traversal" in tilemap data).
- [ ] **Test scene (manual):** you create one room in the editor with a few grey tiles, a vent corridor, and a guard placeholder collider. Wire in the Infiltrator prefab.
- [ ] **CLI validation:** a script that loads the test scene in `-batchmode`, spawns the Infiltrator, runs a movement sequence (forward, sprint, vent-enter), and confirms state transitions in the log. Reports to stdout for CI.
- [ ] **Feel playtest (human):** you open the editor, hit Play on the test scene, and confirm the movement *feels* right (acceleration curve, noise timing). Feed back movement-speed or stance-threshold numbers to Claude Code for Phase 1b tuning.

## Phase 2 — Lighting, Visibility & Sound Propagation
- [ ] Night baseline lighting with authored light sources (`Light2D`): lamps, window spill, floodlights.
- [ ] Player visibility sampling: is the Infiltrator's tile in shadow or light right now (feeds the light-level meter).
- [ ] Guard vision cone: raycast/shape-cast with distance and light-level falloff, producing a visibility category (none / silhouette / partial / clear), not just a boolean.
- [ ] Sound event system: emitters (footstep, sprint, gadget) broadcast type + loudness + location; anything in range "hears" it. This same event bus feeds guards, microphones, and the sensor systems later.

## Phase 3 — Guard Behavior: Duty, Alertness Ladder, Patrol Paths
- [ ] Guard duty data: **Guard a Point** vs. **Patrol a Path**, where paths are authored waypoint lists per zone (not runtime-drawn).
- [ ] Guard alertness ladder as a state machine: Unaware → Suspicious → Searching → Alarmed, driven by the visibility/sound events from Phase 2.
- [ ] Warden-set baseline **Alertness** (Relaxed / Standard / Heightened) as a modifier on ladder sensitivity and speed, separate from the ladder state itself.
- [ ] Short rolling per-guard memory buffer (last ~60s of personally perceived sight/sound events) — this becomes the input to the report-generation system (Phases 6–7), so get the data structure right here even before there's a language model consuming it.
- [ ] Alarmed-guard auto-engage/capture behavior on reaching the Infiltrator.
- [ ] **Guard ID assignment:** every guard spawns with a persistent, unique display ID (e.g. `"G-04"`) that survives duty/alertness reassignment — feeds Phase 4's identity system and Phase 5's map icons.

At the end of this phase the game should be playable end-to-end with placeholder/no dialogue — a single guard that patrols, notices, escalates, and can catch the Infiltrator. This is the core loop's true minimum viable version; don't move on until it's fun.

## Phase 4 — Guard Identity, Status Checks & Badge Security
Backend logic for a system that's central to the stealth-badge play pattern: a takedown isn't automatically known information, and a stolen badge is a real, revocable credential rather than a generic disguise. This phase is pure data/state-machine work; the click-to-check UI itself is wired in Phase 5.
- [ ] **Consciousness state machine per guard:** `Active` (patrolling per duty/alertness) → `Down` (unconscious, takedown timer running) → `Active` again on wake. Separate from the alertness ladder (Phase 3) — a guard can be Down regardless of what alert state they were in when hit.
- [ ] **No automatic disclosure:** a guard going Down does **not** push any event to the Warden on its own. The guard entity simply stops moving/patrolling at its current position — indistinguishable, without a check, from a guard that's just standing still.
- [ ] **Status-check backend:** an exposed method (`CheckGuardStatus(guardId)`) returning `Responsive` or `Unresponsive` — this is the data layer Phase 5's click interaction calls into.
- [ ] **Wake-up event:** when the Down timer expires, transition back to `Active` and unconditionally enqueue an incident report into the report-generation pipeline (Phases 6–7), tagged as "recovering from an attack" rather than a live sighting — fires whether or not the Warden ever ran a status check.
- [ ] **Badge possession per guard:** each guard entity carries a badge/keycard reference; a takedown flags it as lootable.
- [ ] **Badge Flag trigger:** the moment a guard's `Unresponsive` state is confirmed — by a successful status check *or* by that guard's own wake-up report firing — check whether their badge is missing, and if so, flag that specific badge ID facility-wide.
- [ ] **Flagged-badge consequences:** a flagged badge fails on every door and immediately pushes a precise, unambiguous alert to the Warden on next use attempt — distinct from the softer plausibility check below.
- [ ] **Plausibility check:** compare a badge's owner's current assigned duty/post (guard) or last dispatch destination (Technician, see Phase 12) against the door actually being opened; a mismatch enqueues a passive suspicion ping, independent of whether the badge is flagged yet.
- [ ] **Door open/close + attribution log (backend):** each badge-gated door records its open/closed state and the badge ID that last opened it — Phase 5 renders this, Phase 11 places the door fixtures.

## Phase 5 — Warden Dashboard Shell
- [ ] Multi-screen UI shell: **Camera Bank**, **Guard Comms**, **Facility Deployment** — three screens, not four; sensors live on the map now rather than a separate Sensor Grid screen (see below).
- [ ] Focus model: one screen "active" at a time, others running in background.
- [ ] Ping system: flash + audio cue on a non-focused screen when it has a new event, with a generic event queue any system (camera, sensor, guard, door) can push into.
- [ ] **Facility Deployment screen (merged map):** live guard positions/duty/alertness/ID on a top-down map; click-to-select and reassign duty/alertness (wired to Phase 3's guard data). Sensors render as dots on the same map, flashing on trigger and clickable for a detail popup (type, last-triggered time). Badge-gated doors render as icons showing open/closed state and last-opener badge ID (Phase 4's door log). Includes the Technician dispatch panel (count + broken-fixture list, no live position — Phase 12).
- [ ] **Guard status-check interaction:** clicking a guard's icon calls Phase 4's `CheckGuardStatus` and displays `Responsive`/`Unresponsive` — the Warden's only proactive way to learn a guard is down.
- [ ] Camera Bank with static (non-interactive) feeds first — live-watch interaction can come with Phase 8.

## Phase 6 — Guard Radio Reports (Rule-Based First)
Validate the *mechanic* — streaming text, interruption, partial information — before touching a real model.
- [ ] Template/rule-based report generator: fills sentence templates from the guard's current perception snapshot (e.g. `"Control, I might have {something} near {location}."`). Crude on purpose.
- [ ] **Wake-up report template:** a distinct sentence-template category for Phase 4's wake-up event (e.g. `"Control, someone jumped me near {location}, I'm okay but—"`), separate from live-sighting templates.
- [ ] Word-by-word (or chunk-by-chunk) reveal on the Guard Comms screen, at a tuned reading pace.
- [ ] Interruption handling: if the reporting guard is taken down mid-reveal, cut the text immediately and render a garbled/cutoff state (visual glitch + abrupt stop, not a graceful trail-off).
- [ ] Playtest specifically for whether the *cut-off* reads as information to the Warden without needing to test full LLM output yet.

## Phase 7 — Small Language Model Integration
Swap the rule-based generator from Phase 6 for the real system, behind the same interface so nothing downstream (streaming reveal, interruption) has to change.
- [ ] Pick a local, on-device small model — needs to run within the per-report latency budget with no network dependency mid-match. Evaluate a few small (roughly sub-1B parameter) options run via a local inference runtime (e.g. `llama.cpp`/GGUF, ONNX Runtime, or similar) rather than a cloud API call.
- [ ] Design the structured prompt: guard identity/personality tag, current sight state (none/silhouette/partial/clear + location), current sound events, memory-buffer summary, alertness, and report-trigger type (live sighting vs. wake-up incident) — turned into a compact prompt template, not raw JSON.
- [ ] Streaming generation pipeline: token-by-token (or small-chunk) output piped into the same UI reveal built in Phase 6.
- [ ] Interrupt-on-takedown: cancel generation immediately when the reporting guard goes down (not just stop rendering — actually stop the inference call to save budget).
- [ ] Performance pass: measure generation latency under real match conditions (multiple guards potentially reporting close together) and cap concurrent generations if needed.
- [ ] Guardrails: length cap per report, per-guard report cooldown so one guard can't spam the Comms screen.
- [ ] Fallback path: keep the Phase 6 rule-based generator available as a low-spec/offline fallback.

## Phase 8 — Sensor Network & False Alarms
- [ ] Motion sensors, pressure plates/tripwires, badge readers, network intrusion alerts — each pushes into the Facility Deployment map's sensor-dot layer from Phase 5.
- [ ] Camera Bank goes interactive: live-watch a feed for positive ID; unwatched feeds run a background anomaly check that can still ping (weaker, delayed).
- [ ] Random false-alarm generator: low steady-rate background events per sensor type (wildlife, electrical flicker, wind), visually/audibly indistinguishable from a real trigger at the moment they fire.
- [ ] Infiltrator-side false-alarm tools: noise maker (also drives sound events into Phase 2's bus) and signal jammer (spoofs a sensor node directly without the Infiltrator being present).
- [ ] Tuning pass: false-alarm rate should be noticeable but not so frequent that ignoring pings becomes the dominant strategy.

## Phase 9 — Infiltrator Gadget Kit
- [ ] Tranquilizer pistol, stun/EMP tool, camera looper, camera jammer, lockpick/bypass kit, thermal cloak, disguise, ghost pass, signal jammer.
- [ ] 3-gadget loadout selection UI at the end of setup phase.
- [ ] Per-gadget cooldowns/charges and their interaction with the sensor systems from Phase 8 (e.g. thermal cloak vs. heat sensors specifically, not sensors generally).
- [ ] Camera jammer as a persistent, stateful plant (not a timed effect like the looper) — flags the target camera as "broken," to be picked up by Phase 12's repair system.
- [ ] Inventory swap-to-pickup interaction: walking onto a found item (spawned by Phase 11) prompts a drop-one-to-take-one swap against the current 3 slots; dropped gadgets persist in the world.
- [ ] **Dedicated ID-card slot:** a single-capacity inventory slot, entirely separate from the 3 gadget slots, that holds one looted badge at a time (Phase 4's badge-possession data). Looting a second badge while already carrying one prompts a drop-and-swap, same pattern as gadgets but its own independent slot.
- [ ] **Badge pickup interaction:** an on-body loot prompt on any `Down` guard or downed Technician, filling the ID-card slot from Phase 4's badge data — distinct from the Disguise gadget's generic, untraceable fake credential.

## Phase 10 — Objectives: Hacking, Drive Extraction, Exfil
- [ ] Terminal hack minigame: timed skill check, interruptible with retained progress.
- [ ] Physical drive carry state: two-handed gadget lock while carrying, visible "loaded" tell.
- [ ] Multi-vault objective handling: extraction can be started at any Data Vault candidate room (Phase 11) — all of them are real, and completing extraction at any single one ends the round immediately in the Infiltrator's favor.
- [ ] Exfil point detection and win-condition wiring for both objective types.
- [ ] Network intrusion alert integration (hacking risk, and the Surveillance/Ops Room hack from Phase 11, both tie back into Phase 8's sensor layer).

## Phase 11 — Blueprints, Room Roles & Round Generation
This is the phase that turns "Halcyon Site 7" from one hand-built map into a set of hand-built maps whose *contents* reshuffle every round — the anti-turtling backbone of the whole design, so it earns its own phase rather than being folded into level art.

**The layout is not generated.** Floorplans are authored by hand and shipped as data; generation only decides what each room *is*, what's in it, and who's guarding it. This is a deliberate trade: it gives up "no two maps ever share a shape" in exchange for eliminating the entire class of layout-generation bugs (orphaned rooms, unreachable vaults, degenerate corridors) that would otherwise dominate this phase, and it makes the automated validation a small, enumerable check instead of a graph proof. Everything downstream — patrol pathing, sensor coverage, sightlines — becomes tractable because the walls hold still.

### Part A: Authoring (data, no runtime code)
- [ ] **Blueprint schema:** a JSON floorplan format — rooms, corridors, doorways, vent/crawlspace runs, exterior entrance positions. Every room is a **slot** with a stable ID and an `eligibleRoles` list.
- [ ] **Author 3–4 blueprints** as data + tilemap prefabs, each a distinct building shape (long spine, central ring, two-wing split). Start with **one** blueprint and only add more once the role/anchor system below is proven against it.
- [ ] **Anchor schema per room:** named, fixed points for `itemAnchor`, `guardPost` (position + facing), `lightSource`, `sensorMount`, `sabotageFixtureMount`. Nothing in the game is ever placed at a runtime-computed position.
- [ ] **Patrol loop authoring:** each blueprint carries a set of complete, closed waypoint loops. The generator picks which loops are live; it never draws a route.
- [ ] **Room dressing library:** per role (vault / power / ops / office / filler-storage / filler-GPU / filler-breakroom / filler-archive), a prefab + its own light and item anchors, so the same slot doesn't look identical two rounds running.
- [ ] **Blueprint authoring validator** (`-batchmode`): run once per blueprint at author time, not per round — every room reachable, every entrance reaches every vault-eligible slot, every patrol loop closed, every doorway connects exactly two rooms. A blueprint that fails is a level-design bug, caught before it ships.

### Part B: Round generation (the only runtime randomness)
- [ ] **Role assignment pass:** given a blueprint and a seed, assign exactly one role per slot under a fixed recipe — from 5 vault-eligible slots choose 3 (all real, functionally identical; feeds Phase 10's multi-vault win check); from 2–3 power-eligible choose 1; from 2–3 ops-eligible choose 1; open a subset of the 4–6 authored entrances; everything else falls back to filler with a rolled dressing.
- [ ] **Vault-spread constraint:** reject and reroll a role assignment whose 3 vaults are all in one wing or all mutually adjacent — the only real constraint in the whole generator, and it's a check on a set of ≤5 slot IDs, not a pathfind.
- [ ] **Door typing pass:** roll each authored doorway as unlocked / lockpick-only (Phase 9's kit) / badge-gated, constrained so the slots that won Vault, Power, and Ops roles always sit behind a badge reader. Wire to Phase 4's door-log and plausibility-check backend.
- [ ] **Item placement pass:** a large authored pool of found items; select a weighted-random subset (~4–6) and assign each to an `itemAnchor`, under a spread constraint (never all in one wing, never all on the shortest entrance→vault path). Feeds Phase 9's pickup/swap interaction.
- [ ] **Map-item constraints:** the pool's map items (Map fragment, Maintenance schematic) need their own placement rules — a Maintenance schematic must never spawn inside a room it would reveal as a vault, and a Map fragment's revealed wing should be biased *away* from the wing it spawns in. Weight the schematic low; it's the strongest single item in the pool.
- [ ] **Guard deployment pass:** choose which `guardPost` anchors are manned and which authored patrol loops are live, weighted toward the slots that won Vault/Power/Ops — deployment should be a legible tell about where the objective is. Feeds Phase 3's duty data and Phase 4's plausibility check.
- [ ] **Sabotage fixture placement:** bind sensor relay boxes (disable a sensor cluster) and zone breaker panels (disable one room's lighting) to `sabotageFixtureMount` anchors in the rooms whose systems they control — interactable, sabotage-then-flag-for-repair, same pattern as the Power Room.
- [ ] **Light + exfil resolution:** activate each placed dressing's `lightSource` anchors (feeding Phase 2); select which of the round's open entrances also serve as exfil points.
- [ ] **Sensor mount exposure:** hand the Warden's setup UI (Phase 13) the blueprint's `sensorMount` list as the legal placement set — the Warden picks from the same anchors the generator does.
- [ ] **Blueprint export for the Infiltrator:** derive, from the blueprint alone and *before* the role/anchor passes run, a structure-only view — rooms, corridors, doorways, vent runs, which entrances opened. This is the Infiltrator's starting map. Build it as a projection of the blueprint data, not as a redaction of the finished round: a redaction can leak through a bug, a projection has nothing to leak.

### Part B2: The Infiltrator's blueprint screen
Sits between Phase 5's Warden dashboard and Phase 9's gadgets; build it once the round generator can emit the structure-only export above.
- [ ] **Blueprint UI:** toggleable overlay (also the Infiltrator's setup-phase view), rendering the structure-only export plus the player's own position. Rooms draw as blank outlines — no labels.
- [ ] **Knowledge model, not a fog-of-war shader:** back it with an explicit per-round `InfiltratorKnowledge` store keyed by room ID / door ID / sensor ID, with an entry per fact and a source (`explored`, `map-item`, `spotted`). The UI renders that store and *only* that store, so a rendering bug can never reveal a room role the player hasn't earned. This is the same containment argument as the projection above.
- [ ] **Reveal sources:** entering a room marks its role; interacting with a door marks its type; line-of-sight on a sensor pins it.
- [ ] **Map items write into the same store:** Map fragment marks every room role + badge door in one wing; Maintenance schematic marks every room role, door type, and sabotage fixture facility-wide; Signal sniffer pins the nearest two sensors. Each is a bulk write, not a special rendering mode.
- [ ] **Networking note (Phase 15):** the knowledge store is authoritative on the host and must never be replicated to the Warden's client, and the *unrevealed* half of the round's role table must never be replicated to the Infiltrator's. Default hosting is the Infiltrator's client, so the Warden's role table is the side actually at risk on a tampered client — worth listing as a known prototype-level trust gap.

### Part C: Per-round validation
- [ ] **Round validator** (`-batchmode`, runs on every generated round in CI over N seeds): 3 vaults assigned, all reachable, spread constraint satisfied, ≥2 exfil points, exactly 1 Power Room and 1 Ops Room, 4–6 items placed and spread, every badge-gated door's room role is one of Vault/Power/Ops, no manned guard post orphaned from a live patrol loop. Because the blueprint is pre-validated, this pass is assertions over a role table — it should run thousands of seeds in seconds and is the natural CI regression gate.
- [ ] **Seed reproducibility:** a round is fully described by `(blueprintId, seed)`. Log it, and support replaying it, so any generation bug is reproducible from two values in a bug report.

### Part D: Room mechanics
- [ ] **Power Room state machine:** blackout burst (all lighting/cameras/sensors down briefly) → degraded-power ("awaiting repair") state (dimmer lighting, partial sensor/camera outage). The state machine only tracks broken/fixed here; the actual repair dispatch is Phase 12.
- [ ] **Surveillance/Ops Room:** hackable terminal exposing the same guard position/duty/alertness data the Warden's Facility Deployment screen reads, gated behind a slower hack and a guaranteed Network Intrusion ping (feeds Phase 8).

## Phase 12 — Technicians & Repair
A second NPC class, deliberately weaker and lower-fidelity than guards, whose whole job is clearing the "broken" flags Phase 11's sabotage fixtures and Phase 9's camera jammer set.
- [ ] Technician entity: separate from the guard class entirely — no weapon, no radio-report-on-sight, no alertness ladder.
- [ ] Warden dispatch panel (extends Phase 5's Facility Deployment screen): a count of available Technicians and the current list of broken fixtures; click-to-dispatch. Deliberately **no** live Technician position feed — only "in the field" — unless one is caught on a live Camera Bank feed (Phase 8).
- [ ] Pathing to the assigned fixture and a repair-on-arrival timer that clears the fixture's broken flag.
- [ ] Flee behavior: on spotting the Infiltrator, switch to fleeing at a speed faster than the Infiltrator's sprint, away from the last-seen position; only a ranged takedown or a surprise ambush before the flee state kicks in can stop one. After a set duration without re-spotting the Infiltrator, de-escalate and resume the interrupted task.
- [ ] Delayed reporting: once a fled Technician reaches safety or resumes its task, push a stale sighting (position + time-since-seen) to the Warden — a weaker, after-the-fact version of a guard's live radio report.
- [ ] Per-job flee-count tracking: after enough separate flee events on the same job, the Technician abandons it, returns to base, and explicitly reports the abandonment, leaving that fixture broken until a replacement is dispatched.
- [ ] Permanent takedown: a knocked-out Technician never revives for the rest of the round and is removed from the available pool — silently, with no report generated.
- [ ] **Badge behavior (no wake-report path):** Technicians never wake up, so a looted Technician badge is never caught by Phase 4's wake-up Badge Flag — only Phase 4's plausibility check (comparing door location against last dispatch destination) can expose one, and since a dispatch destination is a single point rather than a whole patrol path, a mismatch narrows the Warden's suspicion much more precisely than a guard-badge mismatch does.
- [ ] Badge-door interaction: Technicians open badge-locked doors on their route; the Infiltrator can tailgate through behind one, or use a Technician's predictable destination to plan an ambush (ties into Phase 9's ID-card slot and Phase 4's badge system).

## Phase 13 — Match Flow, Scoring, Best-of-3
- [ ] Setup-phase timer and budget-spend UI (Warden), constrained to the round's `sensorMount` anchor set (Phase 11), + the Infiltrator's blueprint screen (Phase 11 Part B2) as their setup-phase view.
- [ ] Round timer and timeout-as-Warden-win handling.
- [ ] Scoring table implementation (clean win / partial / early catch / timeout) and match total across 3 rounds.
- [ ] Role swap between rounds, with a fresh `(blueprintId, seed)` roll each round. Whether the blueprint itself changes between rounds of a match, or holds while only the contents reshuffle, is a **balance question for Phase 16** — holding it makes a match a deepening read of one building; rerolling it keeps both players scouting.

## Phase 14 — Audio Pass
- [ ] Text-to-speech (or pre-recorded phoneme-driven) voice for guard reports, synced to the streaming text reveal from Phase 7.
- [ ] Static/cutoff SFX for interrupted reports.
- [ ] Ambient night/facility soundscape.
- [ ] UI ping/alert sounds distinct per screen (Camera Bank vs. Guard Comms vs. Facility Deployment should each have a recognizable cue, including a distinct one for door/badge pings).

## Phase 15 — Networking Hardening
By this point every phase has been built and tested against Phase 0 Part D's bare LAN scaffold. This phase upgrades that scaffold into something two players can actually use from different locations — it's not standing up networking for the first time.
- [ ] **Unity Relay + Lobby integration:** replace direct IP-connect with a join-code flow (host creates a lobby, shares the code, the other player enters it) — removes the port-forwarding requirement of the Phase 0 LAN scaffold.
- [ ] **Host selection:** default to the Infiltrator's client hosting (the more latency-sensitive side — movement, guard vision timing, the LLM report stream), with an option for players to choose at match start instead.
- [ ] State sync for guard/Technician positions (where visible), alertness, consciousness state, and sensor/door events.
- [ ] Sync strategy for streaming guard-report text specifically (this is the one system with an ongoing, interruptible stream rather than discrete state — needs its own thought, not just generic state replication).
- [ ] **Stretch:** migrate from host-authoritative to a small dedicated server (headless Unity build, self-hosted or a cheap VPS) if host advantage or client-side trust (e.g. a modified Warden client reading local guard state it shouldn't have) becomes worth closing off. Not required for the prototype.

## Phase 16 — Playtesting & Balancing
- [ ] Tune alert-ladder thresholds and Alertness modifiers.
- [ ] Tune false-alarm rate and gadget cooldowns.
- [ ] Tune lockdown cost/cooldown and facility alert-level escalation.
- [ ] Tune Technician count, flee-timeout, and abandonment threshold so sabotage is a real drain without being a solo win condition on its own.
- [ ] Tune guard wake-timer length and plausibility-check sensitivity so the stealth-badge play pattern (quiet takedown → loot → matching-location door use) has a real, learnable window without being either trivial or worthless.
- [ ] Tune the Maintenance schematic's spawn weight, and check whether "find the schematic" quietly becomes the Infiltrator's real objective every round — if the fastest line to the vault always runs through the item pool, the item pool is no longer optional and something is wrong.
- [ ] Decide whether a match holds one blueprint across all 3 rounds or rerolls each round (see Phase 13) — a feel call, best made once there are 3–4 blueprints to test with.
- [ ] Validate scoring weights actually reward the intended play patterns (near-miss vs. clean catch vs. clean steal).

## CI/Validation Loop (runs after each Claude Code phase)

For each phase checklist item Claude Code completes, the workflow is:

1. **Claude Code writes C# scripts and data files** → commits to git
2. **Automated validation:** `Unity -batchmode -quit -logFile - -executeMethod AIRGAP.CI.ValidatePhase[N]` runs the phase-specific validator
3. **If validation passes:** report passes to stdout, human opens the editor to wire/test, runs Play
4. **If validation fails:** report errors to stdout, Claude Code fixes and re-commits
5. **Feel/balance feedback (human only):** after playtesting, provide tuning numbers back to Claude Code for next iteration

This means Claude Code can iterate on logic/data/scripts independently, and you only open the editor to (a) do one-time wiring (create a scene, drag prefabs in, wire inputs), (b) hit Play to test, or (c) provide balance feedback.

---

**Sequencing note:** Phases 1–3 are the first real bet — a fun core loop with a single dumb guard and no dialogue at all, on one hand-built test room. Phase 4 (Guard Identity & Badge Security) is backend-only and can be built right alongside Phase 3, since it just extends the guard data model — it doesn't need UI until Phase 5 exists to expose the click-to-check interaction. Don't invest in Phase 7 (the language model) until Phase 6's rule-based version has proven the *streaming + interruption* mechanic is worth building for real. Phase 11 (blueprints and round generation) is the second real bet, and it's now a much cheaper one — the risky half (generating a layout) is gone, replaced by hand-authored floorplans. It can be prototyped early against the Phase 1–3 test room by drawing **one** blueprint with a handful of role-eligible slots, well before Phase 10's full multi-vault objective logic or the full dressing library exist — validating "does reshuffling what's in a familiar building actually create fresh decisions" is worth doing cheaply, on one blueprint, before drawing three more. Phase 12 (Technicians) is a smaller, self-contained system that can slot in any time after Phase 9 and Phase 11 exist to give it gadgets and fixtures to react to. Everything else is additive breadth, not core-loop risk.

**With Unity locally installed and CLI available, Claude Code can drive Phases 0–4 almost end-to-end (up to the point where you hit Play and feel-test). Phases 5+ will need you to open the editor more often as UI and full-scene integration become necessary, but the data, logic, and procedural systems stay driven by Claude Code.**
