# AIRGAP

*One player steals the mind. The other watches every wall.*

A 1v1 asymmetric stealth game in 2D, set over a single night. **Player One (the Infiltrator)** sneaks into a data facility to steal a proprietary AI model. **Player Two (the Warden)** never sets foot in the building — they sit in a control room full of monitors, commanding guards and reading sensors to find and stop the intruder before the theft completes.

Closest reference points: *Splinter Cell: Chaos Theory*'s "Spies vs. Mercs" mode for the asymmetry, *Mark of the Ninja* / *Monaco* for the 2D stealth read, *Papers, Please* for a control-room job made tense by imperfect information.

## Premise

**Halcyon Dynamics** trains one of the most capable AI models in the world — codename **LUMEN** — air-gapped inside a single facility, never networked to the outside world, because Halcyon doesn't trust it to anyone, including themselves. That isolation is the whole security model. It's also the whole heist.

Player One is a contracted infiltrator hired to pull LUMEN's weights out through the one gap air-gapping can't close: a person willing to walk in, at night, when the facility runs on a skeleton crew. Player Two is that skeleton crew — one Warden, alone in the control room, with only cameras, sensors, and a handful of guards standing between LUMEN and the door.

## Visual Style & Setting

- **2D**, top-down (or close to it — final camera angle TBD in prototyping, but readable silhouettes and clear sightlines take priority over a fixed convention).
- **Night.** The facility runs on real, diegetic light sources — security lamps, window spill, the blue glow of monitors, a guard's flashlight — rather than uniform ambient light. Darkness is the Infiltrator's primary tool; light pools are the terrain feature both players are always negotiating around.
- Thermal and infrared sensors matter more here than they would in daylight — several of the Warden's tools exist specifically to see through the dark, and several of the Infiltrator's tools exist specifically to beat them.

## Core Loop

1. Infiltrator enters the facility and must locate the server room / data core.
2. Infiltrator starts a **data extraction** (a timed, interruptible action — hacking a terminal or pulling a physical drive).
3. Warden reads cameras, sensors, and guard radio chatter to locate the Infiltrator, and repositions guards to stop the extraction before it completes.
4. Round ends when LUMEN is fully exfiltrated (Player One wins) or the Infiltrator is caught/eliminated (Player Two wins).

## Match Structure

- **Setup phase (60 seconds):** Warden spends a fixed budget placing cameras/sensors/traps (see Facility section) and assigns each guard's starting duty and alertness. Simultaneously, the Infiltrator gets a one-time exterior scout — building blueprints and entrance points, but no interior camera feeds.
- **Round (8–10 minutes):** live play, ending on a win condition or the clock (Warden wins on timeout — a stalled Infiltrator has failed the job).
- **Match = best of 3 rounds, with roles swapped each round.** Round 3 (if needed) is played on a different setup-phase layout so a strong opening read can't be replayed identically.

## Win / Loss Conditions

- **Player One wins** by extracting LUMEN and reaching an exfil point — a caught courier can still be intercepted, so being "done" hacking isn't the same as being safe.
- **Player Two wins** by capturing or eliminating the Infiltrator, or by holding full lockdown until the round timer expires.
- **Near-miss scoring:** if the Infiltrator is caught *after* starting extraction but before finishing, that round is scored as a partial loss (worth fewer match points to the Warden than a clean early stop) — see Scoring.

## Player One — The Infiltrator

Plays in the facility directly. Wins through patience and misdirection, not force.

### Movement & Stance
- Crouch / walk / sprint, each with a visible noise-radius indicator — sprinting is loud and pings sound-based sensors.
- Vents, crawlspaces, and maintenance corridors act as shortcut tiles that bypass patrolled routes entirely.
- A light-level meter reflects whether you're standing in a light pool or in shadow — shadow is the default stealth state at night, so light (a lamp, an open door, a guard's torch beam) is the hazard, not the reverse.

### Weapons & Gadgets
Non-lethal only, by design — an Infiltrator who can fight their way out flattens the asymmetry the whole game is built on. Loadout for a round is 2 gadgets, chosen before the setup phase ends:

- **Tranquilizer pistol** — silent, short range, drops a guard for ~20s. 3 shots per round.
- **Stun/EMP tool** — melee-range guard takedown, or thrown to short a camera/sensor for 5s.
- **Camera looper** — freezes a camera's feed on a 4s loop while you cross its cone.
- **Noise maker** — throwable that pulls a guard's attention to a location. Doubles as a deliberate false alarm (see below).
- **Signal jammer** — planted device that spoofs a sensor into reporting a false trigger without you needing to pass through it. Pure misdirection tool, no stealth benefit of its own.
- **Lockpick / bypass kit** — opens doors and terminals faster; a failed pick has a chance to trip a silent alarm.
- **Thermal cloak** — 8s of immunity to thermal/IR cameras only — doesn't help against visual cameras, guards' eyes, or microphones, forcing you to pick the right tool per sensor.
- **Disguise (stolen badge/uniform)** — walk past guards and badge scanners at range; face/ID-verifying cameras still flag it.

### Methods to Steal the Data
Two mutually exclusive objective types, chosen by the Warden's map setup (so the Infiltrator learns which one they're playing on entry, not before):
- **Terminal hack:** a timed skill-check minigame at a fixed workstation in the data core. Interruptible — progress is retained if you're forced to break off, so a near-catch costs time, not a full reset. Stealthy but immobile.
- **Physical drive pull:** extract a drive from the server room and *carry it* to an exfil point. Faster to start, but you're a visibly loaded target for the rest of the round — no two-handed gadgets while carrying, and the Warden knows exactly what to intercept.

### Methods to Trick the Sensors
- Camera looper (visual cameras).
- Crouch-walking and shadow-hugging (sound and light-based detection).
- Thermal cloak (heat sensors).
- Stolen badge (door/checkpoint scanners).
- **Ghost pass:** a limited-use hack that spoofs your network credentials to Halcyon's access-control AI for 10s — beats *software* checks specifically (badge logs, terminal access alerts), not physical sensors.
- **Noise maker / signal jammer:** don't beat a sensor so much as feed it something to falsely report, drawing Warden attention to a location you aren't. See False Alarms.

## Player Two — The Warden

Plays from a control room built around a bank of monitors. Wins through information and positioning, not reflexes — every action has a cooldown or resource cost, so coverage can't just be blanketed everywhere, and no single screen shows everything at once.

### The Screens
The Warden's UI is literally a wall of separate monitors, one per sensor system. Only one screen can hold full attention at a time; the rest run in the background and **ping** (a flash + audio cue) when something on them needs a look.

- **Camera Bank** — a grid of feed thumbnails. Watching a feed live is the only way to positively identify the Infiltrator on camera; an unwatched feed still runs a background anomaly check (see False Alarms) but can't confirm a sighting.
- **Guard Comms** — a live transcript feed, one line per guard, filling in word by word as each guard's report generates (see below). Pings the moment a guard keys their radio, before the message is even finished.
- **Sensor Grid** — a schematic of motion sensors, pressure plates, tripwires, badge readers, and network intrusion alerts. Pings with a location blip when any of them trigger, real or false.
- **Facility Deployment** — a top-down map showing every guard's current position, duty, and alertness. This is also the Warden's command surface: click a guard to reassign them (see Guard Command).

### Guard Command: Duty & Alertness
Guards are not directly puppeted. Each guard runs on its own local behavior (patrol/react/report) driven by two things the Warden sets and can change at any time from the Facility Deployment screen:

- **Duty** — either **Guard a Point** (hold and watch one location) or **Patrol a Path** (walk one of a handful of predefined routes authored into that zone of the map). Reassigning duty takes a few seconds to take effect — guards finish their current beat before redeploying, so commands are a read on the *near* future, not an instant fix.
- **Alertness** — a baseline the Warden sets per guard: **Relaxed**, **Standard**, or **Heightened**. This doesn't mean the guard has seen anything — it's a disposition. A Relaxed guard has a job and isn't looking for trouble: wide patrol swings, slow to notice, slow to report. A Heightened guard is actively watching: tighter sightlines, faster to notice, faster and more precise to report. All guards start a round Relaxed, on an assigned patrol or post, "just doing a job" — turning that up is a deliberate, informed choice by the Warden, not the default state.

Alertness is the dial; the guard's *actual* moment-to-moment state still climbs its own ladder from what it perceives, same as before — Alertness just shifts how sensitive and how fast that ladder moves.

### Guard Perception & Radio Reports
Each guard tracks, in real time:
- **Sight** — whether the Infiltrator is visible, and how much of them: nothing, a silhouette in the dark, a partial glimpse, or a clear ID, based on light level, distance, angle, and obstruction.
- **Sound** — footsteps, sprinting, thrown gadgets, or other noise events within hearing range, tagged with rough type and direction.
- **Memory** — a short rolling log of what that specific guard has personally seen or heard in the last minute or so. A guard who caught a flicker of movement two zones back and now hears footsteps nearby reports differently than one hearing footsteps cold.

When a guard's perception crosses their current alert threshold, they key their radio, and their **report is generated live, word by word, by a small language model** fed a structured snapshot of exactly that guard's sight/sound/memory state and their alertness. The Warden reads it forming in real time on the Guard Comms screen — a report might start vague ("uh, control, I might have—") and sharpen as it goes, or stay vague if that's genuinely all the guard has.

**Interruption:** if the Infiltrator takes a guard down mid-transmission, the message cuts off immediately — garbled, mid-word — right there on the Guard Comms screen. The Warden doesn't get the rest of the sentence, but a report cutting off *at all* is itself unambiguous information: a guard just went down, right about where they were reporting from.

### Software Systems Warnings
- **Motion sensors** covering server-room approaches.
- **Pressure plates / laser tripwires** in vents and maintenance corridors — the Infiltrator's favorite routes, so they're never free.
- **Badge/access logs** — a door opened on a stolen badge flags an anomaly after a short delay.
- **Network intrusion alerts** — hacking a terminal has a rising chance per second of pinging the Sensor Grid with a rough location, pressuring fast-but-risky play over slow-but-safe.

### Other Sensors
- **Drones** — a mobile camera the Warden can fly through open areas from a charge station; loud and visible, easy for the Infiltrator to spot, but covers ground fixed cameras can't reach.
- **Biometric/heat scanners** at chokepoints — beaten only by the thermal cloak, not the camera looper.
- **Microphones** — sound-based detection layered independently of the camera network, so noise discipline matters even out of camera view.
- **Anomaly detection (LUMEN itself):** the facility's own AI passively watches unwatched camera feeds and flags "unusual behavior patterns" on the Camera Bank — a soft, delayed, imprecise ping (a general area, not a marker). Thematic hook: the thing being stolen is also, quietly, part of what's hunting you.

### False Alarms
No screen can be trusted blindly — part of the Warden's skill is learning to weigh a ping instead of reflexively chasing every one.
- **Random false alarms:** wildlife, wind-blown debris on a camera, an electrical flicker, a guard's own footsteps tripping their own motion sensor. Low, steady background rate, tuned so ignoring every ping is as wrong as chasing every one.
- **Infiltrator-triggered false alarms:** the noise maker and signal jammer exist specifically to manufacture pings — a tripwire triggered and abandoned, a sensor spoofed from a distance, a guard baited into a vague, low-confidence report by a half-second peek from just outside clear sightline. These are indistinguishable from a random false alarm *or* a real sighting at the moment they happen; only pattern and follow-up separate them.

### Catching the Other Player
- **Guard capture:** a guard whose alert state reaches Alarmed and who closes to melee range automatically subdues the Infiltrator non-lethally — this is autonomous guard behavior, not something the Warden directly executes. It also recovers any stolen drive back to the facility, which matters if the Infiltrator was already carrying it toward exfil.
- **Lockdown:** facility-wide state, sealing all doors but one monitored choke point and spawning two reserve guards there. Costs a full alert-meter charge (built from sustained Suspicious/Searching reports across the facility, not available turn one) and has a 90-second cooldown once triggered.
- **Environmental traps:** Warden-triggered doors, gas vents, or floor panels, placed during setup — one-shot per round, and visible to the Infiltrator once triggered (fires a tell, not an instakill, so it's a repositioning threat rather than a coin-flip death).

## The Facility

**Prototype map: Halcyon Site 7** — a converted industrial building, three zones, single footprint, built as one hand-crafted map first so the core loop can be tuned before any procedural variation is attempted.

- **Zone 1 — Perimeter & Loading Dock:** two entrances (loading dock roller door, side maintenance hatch), light patrol coverage, few cameras. Multiple ways in, so there's no single choke point to camera-stack from setup.
- **Zone 2 — Office Floor:** open-plan desks, glass-walled meeting rooms, a badge-gated server-room stairwell. Vents and crawlspaces run the length of this zone as the Infiltrator's primary shortcut layer, cutting under or around the authored patrol paths.
- **Zone 3 — Data Core:** the objective room. Heaviest sensor density in the building — motion, thermal, and camera coverage overlap here, balanced by the fact that the Infiltrator *has* to come here, so the Warden can commit resources with confidence instead of guessing.
- **Guard posts & predefined patrol paths:** each zone is authored with a handful of fixed guard posts and 2–3 fixed patrol routes for the Warden to assign guards to — these are level-design content, not something drawn freehand mid-round.
- **Exfil points:** loading dock (fast, exposed), maintenance hatch (slow, covered), and a roof access reachable only via Zone 2 crawlspaces (highest risk, no ground guard coverage) — three distinct risk/reward profiles rather than one obvious back door.
- **Lighting:** each zone has authored light sources (security lamps, office lighting left on overnight, exterior floodlights near the loading dock) against a dark baseline, so shadow routes and light-pool hazards are consistent, learnable level geography rather than random.
- **Facility alert level** (separate from individual guard alertness) climbs Green → Yellow → Orange → Red across the whole round as evidence accumulates — a dropped guard, a tripped sensor, a badge anomaly. Higher tiers add roaming reserve guards and shorten the Warden's lockdown cooldown, so a sloppy early game has consequences that compound rather than resetting cleanly.

## Scoring (per match, best of 3)

| Outcome | Infiltrator | Warden |
|---|---|---|
| Clean extraction + exfil | 3 | 0 |
| Caught mid-extraction (partial) | 1 | 2 |
| Caught before extraction starts | 0 | 3 |
| Round timeout | 0 | 3 |

Match winner is the higher total across 3 rounds (roles swapped each round), so a single clean steal can't be fully offset by a single clean stop — rewards consistency over one big swing.

## Platform & Prototype Plan

- **Engine:** Unity 2D (URP, for the Light2D-driven night lighting), which also supports the top-down/dashboard-style UI for the Warden in the same project.
- **Controls:** Infiltrator on gamepad or keyboard+mouse; Warden on keyboard+mouse only (screen-switching and guard-clicking want precise pointing, not a controller).
- **Local-first:** same-machine or LAN 1v1 for the prototype; matchmaking/netcode is a post-prototype concern, not a v1 requirement.
- **Guard dialogue model:** a small local language model, not a cloud API call — needs to run fast enough for real-time word-by-word streaming and can't depend on an internet connection mid-match. See [DEVELOPMENT.md](DEVELOPMENT.md) for the staged approach (rule-based first, real model second).

See [DEVELOPMENT.md](DEVELOPMENT.md) for the build plan, in order.

## Roadmap / Stretch Goals

- Modular room-tile system to shuffle map layout between matches, once Halcyon Site 7 proves the core loop.
- Meta-progression (unlockable gadgets/sensor types) — deliberately excluded from v1, which keeps loadouts symmetric/fixed so balance is easy to read.
- Ranked ladder / matchmaking.
- Additional maps with different zone counts and exfil-point geometry.
- Cosmetic-only unlocks (guard uniforms, drone skins, control-room UI themes).
