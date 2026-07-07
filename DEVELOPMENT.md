# AIRGAP — Development Plan

A staged build order for the prototype. Each phase should leave the game in a playable (if incomplete) state — the guiding rule is to prove the *feel* of a system with the cheapest possible fake before investing in the real version of it. This is most important for the guard-dialogue system (Phase 6), which is the riskiest and most novel piece.

## Phase 0 — Foundations & Tooling
- [ ] Engine setup: Unity 2D project, URP (needed for `Light2D` / night lighting in Phase 2).
- [ ] Repo structure: `Assets/Infiltrator`, `Assets/Warden`, `Assets/Facility`, `Assets/Shared`.
- [ ] Source control conventions, scene-per-zone layout for Halcyon Site 7.
- [ ] Decide input handling: gamepad + keyboard/mouse for Infiltrator, keyboard/mouse only for Warden (two local players on one machine for the prototype — confirm split-screen or split-monitor setup here).
- [ ] Placeholder art: grey-box tiles, capsule/blob characters. No final art before the loop is proven.

## Phase 1 — Grey-box Movement Prototype
Single test room, no AI yet — just prove movement and stealth *feel* before adding anything reactive to it.
- [ ] Infiltrator top-down controller: crouch/walk/sprint with distinct movement speeds.
- [ ] Noise-radius visualization tied to movement state (debug circle is fine for now).
- [ ] Vent/crawlspace tiles as a distinct traversable layer.
- [ ] Camera/view setup for a 2D stealth game (confirm top-down vs. angled — playtest both cheaply here before committing).

## Phase 2 — Lighting, Visibility & Sound Propagation
- [ ] Night baseline lighting with authored light sources (`Light2D`): lamps, window spill, floodlights.
- [ ] Player visibility sampling: is the Infiltrator's tile in shadow or light right now (feeds the light-level meter).
- [ ] Guard vision cone: raycast/shape-cast with distance and light-level falloff, producing a visibility category (none / silhouette / partial / clear), not just a boolean.
- [ ] Sound event system: emitters (footstep, sprint, gadget) broadcast type + loudness + location; anything in range "hears" it. This same event bus feeds guards, microphones, and the Sensor Grid later.

## Phase 3 — Guard Behavior: Duty, Alertness Ladder, Patrol Paths
- [ ] Guard duty data: **Guard a Point** vs. **Patrol a Path**, where paths are authored waypoint lists per zone (not runtime-drawn).
- [ ] Guard alertness ladder as a state machine: Unaware → Suspicious → Searching → Alarmed, driven by the visibility/sound events from Phase 2.
- [ ] Warden-set baseline **Alertness** (Relaxed / Standard / Heightened) as a modifier on ladder sensitivity and speed, separate from the ladder state itself.
- [ ] Short rolling per-guard memory buffer (last ~60s of personally perceived sight/sound events) — this becomes the input to Phase 6's report generation, so get the data structure right here even before there's a language model consuming it.
- [ ] Alarmed-guard auto-engage/capture behavior on reaching the Infiltrator.

At the end of this phase the game should be playable end-to-end with placeholder/no dialogue — a single guard that patrols, notices, escalates, and can catch the Infiltrator. This is the core loop's true minimum viable version; don't move on until it's fun.

## Phase 4 — Warden Dashboard Shell
- [ ] Multi-screen UI shell: Camera Bank, Guard Comms, Sensor Grid, Facility Deployment, as separate panels/windows.
- [ ] Focus model: one screen "active" at a time, others running in background.
- [ ] Ping system: flash + audio cue on a non-focused screen when it has a new event, with a generic event queue any system (camera, sensor, guard) can push into.
- [ ] Facility Deployment screen: live guard positions/duty/alertness on a top-down map; click-to-select and reassign duty/alertness (wired to Phase 3's guard data).
- [ ] Camera Bank with static (non-interactive) feeds first — live-watch interaction can come with Phase 7.

## Phase 5 — Guard Radio Reports (Rule-Based First)
Validate the *mechanic* — streaming text, interruption, partial information — before touching a real model.
- [ ] Template/rule-based report generator: fills sentence templates from the guard's current perception snapshot (e.g. `"Control, I might have {something} near {location}."`). Crude on purpose.
- [ ] Word-by-word (or chunk-by-chunk) reveal on the Guard Comms screen, at a tuned reading pace.
- [ ] Interruption handling: if the reporting guard is taken down mid-reveal, cut the text immediately and render a garbled/cutoff state (visual glitch + abrupt stop, not a graceful trail-off).
- [ ] Playtest specifically for whether the *cut-off* reads as information to the Warden without needing to test full LLM output yet.

## Phase 6 — Small Language Model Integration
Swap the rule-based generator from Phase 5 for the real system, behind the same interface so nothing downstream (streaming reveal, interruption) has to change.
- [ ] Pick a local, on-device small model — needs to run within the per-report latency budget with no network dependency mid-match. Evaluate a few small (roughly sub-1B parameter) options run via a local inference runtime (e.g. `llama.cpp`/GGUF, ONNX Runtime, or similar) rather than a cloud API call.
- [ ] Design the structured prompt: guard identity/personality tag, current sight state (none/silhouette/partial/clear + location), current sound events, memory-buffer summary, and alertness — turned into a compact prompt template, not raw JSON.
- [ ] Streaming generation pipeline: token-by-token (or small-chunk) output piped into the same UI reveal built in Phase 5.
- [ ] Interrupt-on-takedown: cancel generation immediately when the reporting guard goes down (not just stop rendering — actually stop the inference call to save budget).
- [ ] Performance pass: measure generation latency under real match conditions (multiple guards potentially reporting close together) and cap concurrent generations if needed.
- [ ] Guardrails: length cap per report, per-guard report cooldown so one guard can't spam the Comms screen.
- [ ] Fallback path: keep the Phase 5 rule-based generator available as a low-spec/offline fallback.

## Phase 7 — Sensor Network & False Alarms
- [ ] Motion sensors, pressure plates/tripwires, badge readers, network intrusion alerts — each pushes into the Sensor Grid ping queue from Phase 4.
- [ ] Camera Bank goes interactive: live-watch a feed for positive ID; unwatched feeds run a background anomaly check that can still ping (weaker, delayed).
- [ ] Random false-alarm generator: low steady-rate background events per sensor type (wildlife, electrical flicker, wind), visually/audibly indistinguishable from a real trigger at the moment they fire.
- [ ] Infiltrator-side false-alarm tools: noise maker (also drives sound events into Phase 2's bus) and signal jammer (spoofs a sensor node directly without the Infiltrator being present).
- [ ] Tuning pass: false-alarm rate should be noticeable but not so frequent that ignoring pings becomes the dominant strategy.

## Phase 8 — Infiltrator Gadget Kit
- [ ] Tranquilizer pistol, stun/EMP tool, camera looper, lockpick/bypass kit, thermal cloak, disguise, ghost pass.
- [ ] 2-gadget loadout selection UI at the end of setup phase.
- [ ] Per-gadget cooldowns/charges and their interaction with the sensor systems from Phase 7 (e.g. thermal cloak vs. heat sensors specifically, not sensors generally).

## Phase 9 — Objectives: Hacking, Drive Extraction, Exfil
- [ ] Terminal hack minigame: timed skill check, interruptible with retained progress.
- [ ] Physical drive carry state: two-handed gadget lock while carrying, visible "loaded" tell.
- [ ] Exfil point detection and win-condition wiring for both objective types.
- [ ] Network intrusion alert integration (hacking risk ties back into Phase 7's Sensor Grid).

## Phase 10 — Facility Build: Halcyon Site 7
- [ ] Full three-zone tilemap: Perimeter & Loading Dock, Office Floor, Data Core.
- [ ] Author guard posts and 2–3 predefined patrol paths per zone (data feeding Phase 3).
- [ ] Place authored light sources per zone (feeding Phase 2).
- [ ] Setup-phase budget system: 6 placement points across camera / motion sensor / tripwire / drone station / environmental trap, spent during the 60s setup window.
- [ ] Three exfil points with distinct risk profiles (loading dock, maintenance hatch, roof access).

## Phase 11 — Match Flow, Scoring, Best-of-3
- [ ] Setup-phase timer and budget-spend UI (Warden) + exterior-scout view (Infiltrator).
- [ ] Round timer and timeout-as-Warden-win handling.
- [ ] Scoring table implementation (clean win / partial / early catch / timeout) and match total across 3 rounds.
- [ ] Role swap and re-randomized setup layout between rounds.

## Phase 12 — Audio Pass
- [ ] Text-to-speech (or pre-recorded phoneme-driven) voice for guard reports, synced to the streaming text reveal from Phase 6.
- [ ] Static/cutoff SFX for interrupted reports.
- [ ] Ambient night/facility soundscape.
- [ ] UI ping/alert sounds distinct per screen (Camera Bank vs. Guard Comms vs. Sensor Grid should each have a recognizable cue).

## Phase 13 — Networking
- [ ] LAN 1v1 session (same-machine split-screen/split-monitor may already cover the prototype — only build this if remote play is actually needed next).
- [ ] State sync for guard positions/alertness and sensor events.
- [ ] Sync strategy for streaming guard-report text specifically (this is the one system with an ongoing, interruptible stream rather than discrete state — needs its own thought, not just generic state replication).

## Phase 14 — Playtesting & Balancing
- [ ] Tune alert-ladder thresholds and Alertness modifiers.
- [ ] Tune false-alarm rate and gadget cooldowns.
- [ ] Tune lockdown cost/cooldown and facility alert-level escalation.
- [ ] Validate scoring weights actually reward the intended play patterns (near-miss vs. clean catch vs. clean steal).

---

**Sequencing note:** Phases 1–3 are the real bet — a fun core loop with a single dumb guard and no dialogue at all. Don't invest in Phase 6 (the language model) until Phase 5's rule-based version has proven the *streaming + interruption* mechanic is worth building for real. Everything from Phase 7 onward is additive breadth, not core-loop risk.
