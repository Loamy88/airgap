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

- **Map generation:** immediately before setup, the round rolls a facility from one of a handful of hand-authored **blueprints** — the walls, corridors, and doorways are fixed and known, but *what each room is* is decided fresh (see Facility). Same floorplan, different Data Vault positions, different item spawns, different guard posts. Neither player knows this arrangement, and the Warden can't rely on memorizing where the objective lives — but nobody has to learn a brand-new building either.
- **Setup phase (60 seconds):** Warden spends a fixed budget placing cameras/sensors/traps on the generated layout and assigns each guard's starting duty and alertness. Simultaneously, the Infiltrator gets a one-time exterior scout — building blueprints and entrance points, but no interior camera feeds.
- **Round (8–10 minutes):** live play, ending on a win condition or the clock (Warden wins on timeout — a stalled Infiltrator has failed the job).
- **Match = best of 3 rounds, with roles swapped each round.** Because the map regenerates every round anyway, no round replays an identical layout.

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
Non-lethal only, by design — an Infiltrator who can fight their way out flattens the asymmetry the whole game is built on. Loadout for a round is 3 gadgets, chosen before the setup phase ends:

- **Tranquilizer pistol** — silent, short range, drops a guard for ~30s. 3 shots per round.
- **Stun/EMP tool** — melee-range guard takedown, or thrown to short a camera/sensor for 5s.
- **Camera looper** — freezes a camera's feed on a 4s loop while you cross its cone.
- **Camera jammer** — a plant-and-leave device that kills a camera's feed indefinitely, until the Warden dispatches a Technician to physically remove it (see Sabotage, under Facility). Where the looper is a quick, subtle beat, the jammer is a standing problem you're deliberately leaving behind.
- **Noise maker** — throwable that pulls a guard's attention to a location. Doubles as a deliberate false alarm (see below).
- **Signal jammer** — planted device that spoofs a sensor into reporting a false trigger without you needing to pass through it. Pure misdirection tool, no stealth benefit of its own.
- **Lockpick / bypass kit** — opens doors and terminals faster; a failed pick has a chance to trip a silent alarm.
- **Thermal cloak** — 8s of immunity to thermal/IR cameras only — doesn't help against visual cameras, guards' eyes, or microphones, forcing you to pick the right tool per sensor.
- **Disguise (stolen badge/uniform)** — a generic, untraceable fake credential brought into the round as a loadout pick; walk past guards and badge scanners at range, though face/ID-verifying cameras still flag it. Distinct from a Looted Badge, below — this one was never tied to a real person, so it can't be flagged the way a looted one can.

### Looted Badges
Separate from the 3-gadget loadout, the Infiltrator has one dedicated ID-card slot — filled by looting a real badge off a downed guard or Technician, not chosen at setup. It's its own capacity, so carrying a badge never costs a gadget slot, but only one can be held at a time; looting a second forces a drop-and-swap of the first.

Unlike the Disguise gadget's generic fake, a looted badge is real and tied to a specific person's ID — good for whatever that person could open, right up until a Badge Flag catches up with it (see Guard Identity & Status Checks, under Warden). See Doors & Badge Access, under Facility, for how and when that happens.

### Found Items
Beyond the 3 gadgets chosen at setup, a much larger item pool exists — only a handful of them (roughly 4–6) actually spawn in a given round, placed in item spawn modules around the generated map (see Facility). The Infiltrator doesn't know which ones until they're found, so scouting off the direct route to the objective is a real, live temptation, not just flavor. Carrying capacity stays fixed at 3 slots — picking up a found item means dropping a current gadget in its place, right there on the floor, so every pickup is a genuine trade, not a free stack.

Example pool (not exhaustive — meant to be large enough that no two rounds feel the same):
- **Grapple line** — fast one-way traversal to a normally slow or high route.
- **Smoke canister** — a few seconds of blocked line of sight in a small area, rather than the single-target EMP.
- **Adrenaline shot** — a short, one-time sprint-speed boost without the usual noise penalty.
- **Spare badge** — a second, weaker disguise charge for when the main one is already spent.
- **Map fragment** — permanently reveals the location of one Warden-placed sensor for the rest of the round.
- **Body-drag glove** — lets a downed guard be dragged out of sightlines instead of left where they fell.
- **Spoofed ID chip** — a longer-duration Ghost Pass, at the cost of a full slot instead of a cooldown.
- **Backup tranq dart** — a single extra shot for the tranquilizer pistol, stacking with the base loadout.

### Methods to Steal the Data
Two mutually exclusive objective types, chosen by the Warden's map setup (so the Infiltrator learns which one they're playing on entry, not before):
- **Terminal hack:** a timed skill-check minigame at a fixed workstation in the data core. Interruptible — progress is retained if you're forced to break off, so a near-catch costs time, not a full reset. Stealthy but immobile.
- **Physical drive pull:** extract a drive from the server room and *carry it* to an exfil point. Faster to start, but you're a visibly loaded target for the rest of the round — no two-handed gadgets while carrying, and the Warden knows exactly what to intercept.

### Methods to Trick the Sensors
- Camera looper (visual cameras).
- Crouch-walking and shadow-hugging (sound and light-based detection).
- Thermal cloak (heat sensors).
- Disguise or a Looted Badge (door/checkpoint scanners) — most doors don't need either, being simply unlocked or lockpick-only; badges matter only at the handful of higher-security doors that require one. See Doors & Badge Access, under Facility.
- **Ghost pass:** a limited-use hack that spoofs your network credentials to Halcyon's access-control AI for 10s — beats *software* checks specifically (badge logs, terminal access alerts), not physical sensors.
- **Noise maker / signal jammer:** don't beat a sensor so much as feed it something to falsely report, drawing Warden attention to a location you aren't. See False Alarms.
- **Cutting main power:** a blunt, facility-wide alternative to beating sensors one at a time — see Power & Blackouts, under Facility. Loud and unmissable to the Warden, but it doesn't discriminate between sensor types the way the other tools do.
- **Sabotage (camera jammers, sensor relays, breaker panels):** doesn't trick a sensor so much as take it off the board entirely until the Warden spends a Technician to fix it — a slower, resource-attrition play rather than an in-the-moment dodge. See Sabotage, under Facility.

## Player Two — The Warden

Plays from a control room built around a bank of monitors. Wins through information and positioning, not reflexes — every action has a cooldown or resource cost, so coverage can't just be blanketed everywhere, and no single screen shows everything at once.

### The Screens
The Warden's UI is a wall of three separate monitors. Only one screen can hold full attention at a time; the rest run in the background and **ping** (a flash + audio cue) when something on them needs a look.

- **Camera Bank** — a grid of feed thumbnails. Watching a feed live is the only way to positively identify the Infiltrator on camera; an unwatched feed still runs a background anomaly check (see False Alarms) but can't confirm a sighting.
- **Guard Comms** — a live transcript feed, one line per guard, filling in word by word as each guard's report generates (see below). Pings the moment a guard keys their radio, before the message is even finished.
- **Facility Deployment** — the main map, and the only screen that's really several systems layered on one view:
  - Every guard's current position, ID, duty, and alertness — click a guard's icon to reassign them (see Guard Command) or to run a status check (see Guard Identity & Status Checks).
  - Every placed sensor as a dot on the map — flashes when it triggers, real or false, and is clickable for detail (type, last-triggered time).
  - Every badge-gated door — shows open/closed state and, if it was opened on a badge, exactly which badge ID opened it (see Doors & Badge Access, under Facility).
  - The Technician panel — a count of available Technicians and the current list of broken fixtures to dispatch them to (see Technicians and Sabotage) — but, deliberately, no live Technician positions.

If the Infiltrator cuts main power, the Camera Bank and a portion of the Facility Deployment's sensor layer go dark or degraded along with the facility's own lighting — the one event unmistakable enough to break through the Warden's usual screen-by-screen attention, precisely because it takes several systems away at once.

### Guard Command: Duty & Alertness
Every guard has a persistent ID (e.g. "G-04") shown on their Facility Deployment icon — the handle the Warden uses to address them, and the thing a badge gets tied to when it's stolen off one (see Guard Identity & Status Checks). Guards are not directly puppeted. Each guard runs on its own local behavior (patrol/react/report) driven by two things the Warden sets and can change at any time from the Facility Deployment screen:

- **Duty** — either **Guard a Point** (hold and watch one location) or **Patrol a Path** (walk one of a handful of predefined routes authored into that zone of the map). Reassigning duty takes a few seconds to take effect — guards finish their current beat before redeploying, so commands are a read on the *near* future, not an instant fix.
- **Alertness** — a baseline the Warden sets per guard: **Relaxed**, **Standard**, or **Heightened**. This doesn't mean the guard has seen anything — it's a disposition. A Relaxed guard has a job and isn't looking for trouble: wide patrol swings, slow to notice, slow to report. A Heightened guard is actively watching: tighter sightlines, faster to notice, but more susceptible to false alarms. All guards start a round Relaxed, on an assigned patrol or post, "just doing a job" — turning that up is a deliberate, informed choice by the Warden, not the default state.

Alertness is the dial; the guard's *actual* moment-to-moment state still climbs its own ladder from what it perceives, same as before — Alertness just shifts how sensitive and how fast that ladder moves.

### Guard Identity & Status Checks
A takedown is not, by itself, information. A guard the Infiltrator drops just stops — sitting wherever they fell, no different on the map from a guard standing still for a moment.

- **Status check.** Clicking a guard's icon on Facility Deployment runs a check and returns **Responsive** or **Unresponsive** — this is the Warden's only proactive way to learn a guard is down, short of a live report cutting off mid-sentence (see Guard Perception & Radio Reports).
- **Waking up.** When a guard's takedown timer runs out, they get back up — and unconditionally radio in an incident report, generated the same way as a live sighting but tagged as recovering from an attack rather than watching something right now. This fires whether or not the Warden ever checked on them.
- **Badge Flag.** The instant a guard's Unresponsive state is confirmed — by a Warden's check, or by their own wake-up report — and their badge turns out to be missing, that specific badge ID is flagged facility-wide: it stops opening anything, and any attempt to use it anyway pings the Warden immediately and precisely. Until that moment, though, a looted badge is a real, working credential — see Doors & Badge Access, under Facility.

### Technicians
A second, much lower-fidelity NPC type — unarmed facility staff the Warden dispatches to fix things the Infiltrator has broken (see Sabotage, under Facility). They exist for attrition and misdirection, not combat, and they're commanded very differently from guards:

- **Dispatch, not tracking.** The Warden sees only a count of available Technicians and the current list of broken fixtures; sending one to a job is a single click, not a positioning decision. Once dispatched, the Warden does **not** see that Technician's live location on the Facility Deployment map the way guards are shown — only that they're "in the field" — unless one happens to be caught on a live camera feed.
- **No alarm, no capture.** A Technician who spots the Infiltrator can't radio it in on the spot and can't subdue anyone. All they can do is run.
- **Flee behavior.** On spotting the Infiltrator, a Technician immediately bolts — faster than the Infiltrator's own sprint, so once one gets moving it can't be run down. Only a ranged takedown (tranq) or catching one by surprise before it reacts will stop one. After a stretch of time without re-spotting the Infiltrator, a fleeing Technician calms down and resumes whatever it was doing.
- **Delayed reporting.** A Technician that got away eventually tells the Warden it saw the Infiltrator once it's back to safety or back on task — a stale, after-the-fact position rather than a live one, since it couldn't call it in mid-flight.
- **Task abandonment.** Spook the same Technician enough separate times on one job and it gives up on that job entirely, returns to base, and explicitly reports the abandonment to the Warden — leaving that fixture broken until a replacement is dispatched from the pool.
- **Taken down for good.** A Technician the Infiltrator knocks out doesn't get back up for the rest of the round, permanently shrinking the Warden's repair capacity. Unlike downing a guard, there's no report and no alert cost — it's a pure, silent drain on the Warden's resources.
- **Legitimate access, exploitable.** Technicians can open badge-locked doors on their normal route. The Infiltrator can shadow one through a door they couldn't otherwise open, or use a Technician's predictable destination (a known broken fixture) to set up an ambush.
- **No wake-up, no Badge Flag.** Because a downed Technician never gets back up, a looted Technician badge can never be caught by the guard system's wake-up path — only by the plausibility check (see Doors & Badge Access, under Facility). And since a Technician's "expected location" is one specific dispatch destination rather than a whole patrol path, a mismatch there points the Warden almost exactly at you.

### Guard Perception & Radio Reports
Each guard tracks, in real time:
- **Sight** — whether the Infiltrator is visible, and how much of them: nothing, a silhouette in the dark, a partial glimpse, or a clear ID, based on light level, distance, angle, and obstruction.
- **Sound** — footsteps, sprinting, thrown gadgets, or other noise events within hearing range, tagged with rough type and direction.
- **Memory** — a short rolling log of what that specific guard has personally seen or heard in the last minute or so. A guard who caught a flicker of movement two zones back and now hears footsteps nearby reports differently than one hearing footsteps cold.

When a guard's perception crosses their current alert threshold, after a quick 0.5 - 1.5 second delay, they key their radio, and their **report is generated live, word by word, by a small language model** fed a structured snapshot of exactly that guard's sight/sound/memory state and their alertness. The Warden reads it forming in real time on the Guard Comms screen — a report might start vague ("uh, control, I might have—") and sharpen as it goes, or stay vague if that's genuinely all the guard has.

**Interruption:** if the Infiltrator takes a guard down mid-transmission, the message cuts off immediately — garbled, mid-word — right there on the Guard Comms screen. The Warden doesn't get the rest of the sentence, but a report cutting off *at all* is itself unambiguous information: a guard just went down, right about where they were reporting from.

### Software Systems Warnings
- **Motion sensors** covering server-room approaches.
- **Pressure plates / laser tripwires** in vents and maintenance corridors — the Infiltrator's favorite routes, so they're never free.
- **Badge/access logs** — see Doors & Badge Access, under Facility, and Guard Identity & Status Checks, above, for the full system.
- **Network intrusion alerts** — hacking a terminal has a rising chance per second of pinging the Facility Deployment map with a rough location, pressuring fast-but-risky play over slow-but-safe.

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

**Halcyon Site 7** — a converted industrial building, procedurally assembled from a library of authored room modules at the start of every round. Same building "kit," different arrangement each time, specifically so a Warden can't just memorize where the objective lives and stack every camera and guard on top of it.

### Room Library
- **Entrance modules** — several possible exterior entry points exist (loading dock, maintenance hatch, roof access, service tunnel); the generator picks a subset each round, so entrances aren't fully predictable either.
- **Office/Admin modules** — open-plan and corridor rooms forming the connective tissue between entrances and the vault cluster. Carry the vents and crawlspaces that make up the Infiltrator's shortcut layer.
- **Data Vault candidates** — a small number (2–3) are placed on every generated map, and all of them are real: LUMEN can be extracted from any one of them, so a completed extraction at a single vault ends the round in the Infiltrator's favor. This is the single biggest lever against turtling — a fixed setup-phase budget can't fully fortify every vault the way it could one known Data Core, so the Warden has to genuinely split defensive commitment across all of them instead of walling off one room and calling it solved.
- **Power Room** — houses the facility's main breaker. See Power & Blackouts.
- **Surveillance/Ops Room** — houses a terminal mirroring the Warden's Facility Deployment screen: guard positions, duty, and alertness. Hacking it is slower than a normal terminal and immediately raises a Network Intrusion alert, so the Infiltrator is trading a loud, timed exposure for a temporary read of the same information the Warden gets for free.
- **Filler modules** — storage rooms, GPU racks, breakrooms, archive stacks: no unique mechanic, just more square footage, more sightline breaks, and more places to wait out a patrol. These exist purely to dilute guard/camera density and give the Infiltrator somewhere to be that isn't inherently suspicious.
- **Item spawn modules** — a subset of filler and office rooms roll a chance to contain a found item (see Found Items, under Player One).

### Power & Blackouts
Cutting power at the Power Room is a single-use, facility-wide swing rather than a targeted tool:
- **The cut itself** triggers a momentary total blackout — every light, camera, and sensor in the building drops for a few seconds, no exceptions, visible on both players' screens. It's the least subtle thing the Infiltrator can do; the Warden knows instantly that *something* just happened, just not where.
- **Backup generator state** follows: dimmer facility-wide lighting (more shadow, easier movement) and a portion of cameras/sensors running degraded or offline, until the Warden dispatches a Technician to physically restore it at the Power Room — real travel time, and a real risk, since the Infiltrator can intercept or take down that Technician en route and undo the fix attempt (permanently, if it was the Warden's last available one). See Technicians, below.
- Because it hits the whole building at once instead of one sensor, it's a blunt instrument next to the camera looper or signal jammer — high impact, zero subtlety, and it hands the Warden a very clear (if imprecise) signal to react to.

### Sabotage
Beyond cutting main power, the Infiltrator has a handful of ways to break something the Warden then has to spend a Technician (see Technicians, under Player Two) to fix — each one a deliberate drain on a finite, shrinking resource, not just a stealth tool for its own sake:
- **Main power** (Power Room) — see Power & Blackouts.
- **Camera jammer** — a plant-and-leave device, distinct from the quick camera looper, that kills a camera's feed indefinitely until a Technician physically removes it.
- **Sensor relay sabotage** — disables a whole cluster of motion sensors/tripwires on one relay box until repaired.
- **Zone breaker panel** — kills the lighting in a single room module until reset, a smaller, quieter cousin of a full power cut.

Every one of these sits on the Warden's board as a standing problem until it's resolved — a Warden who's already spent their Technicians chasing sabotage elsewhere is, for a while, just short a camera, a light, or a working sensor cluster, on top of whatever the Infiltrator does next.

### Doors & Badge Access
Most interior doors are either unlocked or simply locked — beatable with the Lockpick/bypass kit, no badge involved. Badge readers are reserved for a smaller set of higher-security doors (the Data Vault cluster, the Power Room, the Surveillance/Ops Room), so a stolen badge is a shortcut on a few real chokepoints, not a universal key.

- **Every badge-gated door is on the Facility Deployment map**, showing open/closed state and, if it was opened on a badge, exactly which badge ID opened it — guard or Technician (see The Screens, under Warden).
- **Plausibility check.** The system knows a guard's current duty/post and a Technician's last dispatch destination. A badge used on a door nowhere near its owner's expected location raises a passive suspicion ping — softer than a hard Badge Flag, but still a tell, and it fires whether or not that badge has been flagged yet.
- **The quiet play:** take down a guard posted right next to a badge door, loot their card, open the door, and close it behind you. As long as their Badge Flag hasn't triggered (no status check, no wake-up report yet) and the door matches their actual post, this reads as completely normal — the Warden has no real reason to notice.
- **The loud mistake:** using that same card on a door far from where the guard is supposed to be trips the plausibility check even before any flag exists — the location itself is the tell, independent of the guard's condition.
- **Technician cards cut both ways** — see No wake-up, no Badge Flag, under Technicians.

### Guard Posts & Patrol Paths
Each generated map still authors a handful of fixed guard posts and a few patrol routes per area cluster, drawn from the room modules' own connection points — level-design content baked into each module, not something drawn freehand mid-round, so patrol logic stays sane however the modules get shuffled together, and doubles as the data the plausibility check (see Doors & Badge Access) compares badge use against.

### Exfil Points
A handful of the placed entrance modules double as exfil points each round, generally with different exposure levels (a fast, camera-heavy route vs. a slow, quiet one) — which entrances double as exfil is itself part of what's randomized, so scouting matters even for a returning player.

### Lighting
Every room module carries its own authored light sources (security lamps, monitor glow, overnight office lighting, exterior floodlights) against a dark baseline, so shadow routes and light-pool hazards stay readable and consistent within a room even though the overall map layout isn't.

### Facility Alert Level
Separate from individual guard alertness, this climbs Green → Yellow → Orange → Red across the whole round as evidence accumulates — a dropped guard, a tripped sensor, a badge anomaly. Higher tiers add roaming reserve guards and shorten the Warden's lockdown cooldown, so a sloppy early game has consequences that compound rather than resetting cleanly.

## Scoring (per match, best of 3)

| Outcome | Infiltrator | Warden |
|---|---|---|
| Clean extraction + exfil | 3 | 0 |
| Caught mid-extraction (partial) | 1 | 2 |
| Caught before extraction starts | 0 | 3 |

*Round timeout scoring depends on current extraction state*

Match winner is the higher total across 3 rounds (roles swapped each round), so a single clean steal can't be fully offset by a single clean stop — rewards consistency over one big swing.

## Platform & Prototype Plan

- **Engine:** Unity 2D (URP, for the Light2D-driven night lighting), which also supports the top-down/dashboard-style UI for the Warden in the same project.
- **Controls:** Infiltrator on gamepad or keyboard+mouse; Warden on keyboard+mouse only (screen-switching and guard-clicking want precise pointing, not a controller).
- **Two devices, always.** No split-screen and no shared-machine mode — each player runs their own client on their own device and connects over a network. This isn't just a UX choice: the Warden's whole premise is *not* being in the room, and a shared screen or shared machine would leak information (a glance at the other half of the screen, a shoulder-surfed dashboard) that the design depends on staying hidden. Networking is therefore a day-one requirement, not a post-prototype add-on — see Networking, below, and [DEVELOPMENT.md](DEVELOPMENT.md) for where it lands in the build order.
- **Guard dialogue model:** a small local language model, not a cloud API call — needs to run fast enough for real-time word-by-word streaming and can't depend on an internet connection mid-match. See [DEVELOPMENT.md](DEVELOPMENT.md) for the staged approach (rule-based first, real model second).

### Networking
- **Dev-time (short term):** direct LAN connect between two devices on the same network — no third-party service needed, cheapest way to get the plumbing working and validate the netcode early.
- **Recommended (real use):** Unity Netcode for GameObjects + Unity Relay & Lobby. Relay handles NAT traversal so neither player needs to port-forward, and Lobby gives a simple join-code flow (one player hosts and shares a code, the other enters it) — no dedicated server to run, and Unity's free tier comfortably covers a 2-player indie game.
- **Host selection:** under a host-authoritative model, whoever hosts has a small latency advantage. Since the Infiltrator's side is the more timing-sensitive one (movement, guard vision, the LLM report stream), default to the Infiltrator hosting, or let players choose at match start.
- **Later, if it matters:** move from host-authoritative to a small dedicated server (a headless Unity build, self-hosted or on a cheap VPS) once the game is competitive enough that host advantage or a tampered client reading local "ground truth" (e.g. real guard positions before a sighting confirms them) becomes worth closing off. Not a prototype concern.

See [DEVELOPMENT.md](DEVELOPMENT.md) for the build plan, in order.

## Roadmap / Stretch Goals

- Meta-progression (unlockable gadgets/sensor types/found-item pool entries) — deliberately excluded from v1, which keeps loadouts symmetric/fixed so balance is easy to read.
- Ranked ladder / matchmaking.
- Additional room-module libraries — a whole second "kit" (different building type/theme) rather than just a bigger Halcyon Site 7 pool.
- Cosmetic-only unlocks (guard uniforms, drone skins, control-room UI themes).
