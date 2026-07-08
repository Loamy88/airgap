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
- **Data-driven over hardcoded.** Room modules, patrol paths, item pool entries, and gadget stats should live as data (JSON/YAML), not baked into scenes or magic numbers — an agent can generate, extend, and tune data files far more reliably than it can edit a scene.
- **Automated validation over manual playtesting for generation.** Anything procedural (Phase 11 especially) runs through headless-mode C# validators — map connectivity, reachability, exfil availability — so Claude Code can verify its own output without human editor intervention.
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
- [ ] **Split-screen/split-monitor decision:** document the chosen approach in a settings comment (stored as JSON or prefab reference for later).
- [ ] **Control schema file:** create a document (markdown or JSON) listing all input bindings for future reference.

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

## Phase 11 — Procedural Facility Generation & Room Library
This is the phase that turns "Halcyon Site 7" from one hand-built map into a room-module kit assembled fresh each round — the anti-turtling backbone of the whole design, so it earns its own phase rather than being folded into level art.
- [ ] Author the room-module library as data/prefabs: entrance, office/admin, Data Vault candidate, Power Room, Surveillance/Ops Room, filler (storage/GPU/breakroom), item-spawn variants — each with its own connection points, guard-post markers, and light-source markers.
- [ ] Graph-based layout generator: place and connect modules into a full facility per round (a room-graph approach, in the spirit of rogue-like generators — start simple, e.g. a connected tree/loop over a fixed module budget, before adding constraints).
- [ ] Automated validation pass (per the Working with Claude Code note above): every generated map must have every Data Vault candidate reachable, at least one path from every entrance to the vault cluster, at least the intended number of exfil points, and no single vault trivially isolable behind one chokepoint — reject and regenerate if not.
- [ ] Data Vault candidate selection: place N candidates per map, all real and functionally identical (feeds Phase 10's multi-vault win check).
- [ ] Guard posts and patrol paths generated/assigned per placed module (data feeding Phase 3), rather than hand-authored per fixed zone.
- [ ] Power Room state machine: blackout burst (all lighting/cameras/sensors down briefly) → degraded-power ("awaiting repair") state (dimmer lighting, partial sensor/camera outage). The state machine only tracks broken/fixed here; the actual repair dispatch is Phase 12.
- [ ] Sabotage fixture placement: sensor relay boxes (disable a sensor cluster) and zone breaker panels (disable one room's lighting) as interactable, sabotage-then-flag-for-repair points, same pattern as the Power Room.
- [ ] **Door placement and typing:** most doors placed as either unlocked (no interaction needed) or lockpick-only (Phase 9's lockpick/bypass kit, no badge involved); a smaller subset — the Data Vault cluster, Power Room, Surveillance/Ops Room — placed as badge-gated, wired to Phase 4's door-log and plausibility-check backend.
- [ ] Surveillance/Ops Room: hackable terminal exposing the same guard position/duty/alertness data the Warden's Facility Deployment screen reads, gated behind a slower hack and a guaranteed Network Intrusion ping (feeds Phase 8).
- [ ] Item-pool spawn system: a large authored pool of possible found items, weighted-random subset (~4–6) selected and placed into item-spawn modules per round (feeds Phase 9's pickup/swap interaction).
- [ ] Light-source placement per module (feeding Phase 2) and exfil-point selection from placed entrance modules.

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
- [ ] Setup-phase timer and budget-spend UI (Warden) + exterior-scout view (Infiltrator), running against a freshly generated map (Phase 11).
- [ ] Round timer and timeout-as-Warden-win handling.
- [ ] Scoring table implementation (clean win / partial / early catch / timeout) and match total across 3 rounds.
- [ ] Role swap between rounds, with a full facility regeneration (Phase 11) each round rather than a reused layout.

## Phase 14 — Audio Pass
- [ ] Text-to-speech (or pre-recorded phoneme-driven) voice for guard reports, synced to the streaming text reveal from Phase 7.
- [ ] Static/cutoff SFX for interrupted reports.
- [ ] Ambient night/facility soundscape.
- [ ] UI ping/alert sounds distinct per screen (Camera Bank vs. Guard Comms vs. Facility Deployment should each have a recognizable cue, including a distinct one for door/badge pings).

## Phase 15 — Networking
- [ ] LAN 1v1 session (same-machine split-screen/split-monitor may already cover the prototype — only build this if remote play is actually needed next).
- [ ] State sync for guard/Technician positions (where visible), alertness, consciousness state, and sensor/door events.
- [ ] Sync strategy for streaming guard-report text specifically (this is the one system with an ongoing, interruptible stream rather than discrete state — needs its own thought, not just generic state replication).

## Phase 16 — Playtesting & Balancing
- [ ] Tune alert-ladder thresholds and Alertness modifiers.
- [ ] Tune false-alarm rate and gadget cooldowns.
- [ ] Tune lockdown cost/cooldown and facility alert-level escalation.
- [ ] Tune Technician count, flee-timeout, and abandonment threshold so sabotage is a real drain without being a solo win condition on its own.
- [ ] Tune guard wake-timer length and plausibility-check sensitivity so the stealth-badge play pattern (quiet takedown → loot → matching-location door use) has a real, learnable window without being either trivial or worthless.
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

**Sequencing note:** Phases 1–3 are the first real bet — a fun core loop with a single dumb guard and no dialogue at all, on one hand-built test room. Phase 4 (Guard Identity & Badge Security) is backend-only and can be built right alongside Phase 3, since it just extends the guard data model — it doesn't need UI until Phase 5 exists to expose the click-to-check interaction. Don't invest in Phase 7 (the language model) until Phase 6's rule-based version has proven the *streaming + interruption* mechanic is worth building for real. Phase 11 (procedural generation) is the second real bet and can be prototyped early against the Phase 1–3 test room with a tiny 2–3-module library, well before Phase 10's full multi-vault objective logic or the full room kit exist — validating "is a freshly shuffled layout actually fun to learn on the fly" is worth doing cheaply before building out the whole library. Phase 12 (Technicians) is a smaller, self-contained system that can slot in any time after Phase 9 and Phase 11 exist to give it gadgets and fixtures to react to. Everything else is additive breadth, not core-loop risk.

**With Unity locally installed and CLI available, Claude Code can drive Phases 0–4 almost end-to-end (up to the point where you hit Play and feel-test). Phases 5+ will need you to open the editor more often as UI and full-scene integration become necessary, but the data, logic, and procedural systems stay driven by Claude Code.**
