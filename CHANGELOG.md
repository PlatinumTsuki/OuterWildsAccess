# Changelog

All notable changes to Outer Wilds Access are documented here.

## 1.0.14 (2026-04-08)

### Auto-walk reliability — major overhaul

After two deep research sessions on the game's internal walking and jumping systems, the auto-walk path scanner and jump logic have been substantially revised. The goal: align our behavior with how the game actually classifies ground, and stop dying from invisible overshoot off platforms.

#### Path scanner (`PathScanner.cs`)
- **Probe geometry** stays at the v1.0.13 values (`ProbeHeight 4f`, `ProbeLength 8f`, `ProbeRadius 0.46f` matching the player capsule)
- **Hit offset validation**: ground hits more than `ProbeRadius + 0.1f` (~0.56m) horizontally from the cell column axis are now rejected, preventing the SphereCast from "leaking" onto neighbouring platforms or railing tops when the cell centre is actually over the void
- **Cache tolerance** tightened: `CachePosTol 1f → 0.25f` so paths recompute after small player displacements instead of reusing stale results
- **Jump clearance height** lowered: `JumpClearH 1.2f → 0.5f`. The previous value was arbitrary and far too liberal — the game has no auto step-up or vault, so anything above ~0.5m is a real obstacle that should not be silently classified as "jumpable"

#### Auto-walk handler (`AutoWalkHandler.cs`)
- **Safety raycast before each auto-jump**: before firing a jump (whether triggered by A* or by the game's own jump prompt), three raycasts probe the predicted landing zone (waypoint, +2m, +4m along the boost direction). If any sample finds no ground within 5m or hits a slope steeper than 60°, the jump is cancelled, the path is invalidated to force a rescan, and a brief cooldown prevents immediate re-trigger. This is the load-bearing fix for "auto-walk jumps over a railing into the void" deaths.
- **Airborne tolerance** shortened: `MaxAirborneBeforeStop 3f → 0.5f`. Combined with the post-jump grace window, the auto-walk now stops within ~1s of an unexpected fall instead of ~3s, drastically reducing fatal drops.
- **Jump diagnostic logs** added (active only with F12 debug mode): every fired or cancelled auto-jump is logged with player position, waypoint, boost direction, and rejection reason if applicable.

### Autopilot — destination refactor and surface alignment

#### Destination validation
The autopilot's hardcoded destination list has been rebuilt around a runtime validation pass. At first use after a scene load, candidate destinations are filtered against `Locator.GetAstroObject()`, `OWRigidbody`, `ReferenceFrame`, and `GetAllowAutopilot()`. Three previously-listed destinations that the game never registers (`TimberMoon`, `VolcanicMoon`, `SunStation`) are now properly removed from the cycle instead of silently failing on confirmation. Three new destinations have been added (`Sun`, `HourglassTwins`, `WhiteHole`), of which `Sun` passes validation; the other two are filtered out by the game's reference frame configuration. **Net result: 9 reliably autopilotable destinations** (Sun, Timber Hearth, Brittle Hollow, Giant's Deep, Dark Bramble, Ash Twin, Ember Twin, Quantum Moon, The Interloper).

#### Critical bug fix — Ash Twin / Ember Twin inversion
Discovered while cross-referencing the Ship Remote Autopilot mod and the decompiled `NomaiRemoteCameraPlatform.cs`. Our destination list had `CaveTwin → "loc_ash_twin"` and `TowerTwin → "loc_ember_twin"`, which is **inverted**: `CaveTwin` is Ember Twin (the planet with caves and the Sunless City), `TowerTwin` is Ash Twin (the planet with the Ash Twin Project towers). When you previously selected "Ash Twin" in the autopilot list, the ship was actually flying to Ember Twin and vice versa. This silent bug has been present since the autopilot feature was introduced.

#### Surface alignment on arrival
After arrival, the autopilot now fires the game's `EnterLandingMode` global message with the destination's reference frame, then `ExitLandingMode` two seconds later. This rotates the ship parallel to the planet's gravity and engages the landing thrusters automatically. The cockpit exit no longer leaves you in a disorienting tilted orientation. A new "Ship aligned with surface" announcement marks completion.

#### Distance formatting
Distances ≥ 1 km are now announced in kilometers with one decimal place ("85.3 kilometers") instead of raw meters ("85000 meters").

### Ship telemetry — clearer landed/in-flight distinction

Pressing **I** at the flight console used to announce both "Near Léviathé" and "Ship landed" when actually parked on the surface, which is contradictory. The handler now detects landed state up front and announces "Landed on Léviathé" instead, suppressing the redundant separate landed message.

### Documentation
- **README.md**: corrected the auto-walk key (M → B), the autopilot destination count (13 → 9), added missing keys (T, U, F12, Escape, Alt+PageUp/Down for category change), updated the Status section to reflect the new "landed on/near" telemetry, added the new autopilot surface alignment to the feature list
- **LISEZ-MOI.txt**: removed (was stuck at version 0.8.5 with completely outdated keybindings)

### Notes
This release was prepared while several scenarios remain partially tested (Brittle Hollow auto-walk, Giant's Deep islands, cave interiors). It is published as an early-test build alongside an active forum testing campaign — feedback and log files are welcome via GitHub issues.

---

## 1.0.4 → 1.0.13 (consolidated)

These versions were not individually documented in the changelog at the time. The major themes:

- **1.0.13** — Auto-walk probe geometry tightened (`ProbeHeight 12 → 4`, `ProbeLength 24 → 8`) after deep research on the game's `CastForGrounded` ground detection. Mortality "almost eliminated" on the Timber Hearth launcher in user tests.
- **1.0.12** — Fixed asphyxiation after teleporting to the ship: the teleport bypassed the hatch trigger, so `EnterShip` never fired and the cabin oxygen was not provided. Fix opens the hatch by reflection and warps the player into the tractor beam column so the game itself handles the entry.
- **1.0.11** — German localization added. Dedicated `OuterWildsAccess.log.txt` file in the mod folder, truncated each session, with F12 toggling detailed debug mode.
- **1.0.10** — New handlers: `QuantumHandler`, `DarkBrambleHandler` (anglerfish proximity warnings), `ElevatorHandler`, `GravityHandler`. Teleportation blocked when piloting or inside the ship (with new `action_inside_ship` localization key).
- **1.0.9** — Reverted the `HasGroundBetween` path simplification check from 1.0.8 (it broke too many legitimate paths in practice).
- **1.0.8** — Suit-aware teleportation: only ghost matter blocks now; with a suit on, water and other hazards are passable. Underwater auto-walk improved to not stop in deep water when wearing a suit. Initial attempt at `HasGroundBetween` path simplification (rolled back in 1.0.9). Autopilot warp velocity set to `Vector3.zero` instead of inheriting the planet's surface velocity.
- **1.0.7 and earlier** — Various smaller improvements and bug fixes around navigation, ship recall, and autopilot, none individually documented.

## 1.0.3 (2026-03-03)

### Added
- **Scout probe handler (O)**: automatic announcements when the probe is launched, anchored, retrieved, or destroyed. Snapshot announcements. Interference detection (polling). On-demand status with distance, anchor time, and interference info.

### Changed
- **Key reassignment**: auto-walk moved from O to **M**, scout probe status assigned to **O**
- All help menu entries and localization keys updated to match new bindings (no residual data)

## 1.0.2 (2026-03-03)

### Added
- **Exploration stats in ship log**: opening the log now shows total entries, explored, and rumored counts globally and per planet
- **Nomai statue as navigation target**: the memory uplink statue in the observatory now appears in the Interactables scan category

### Fixed
- **Teleportation impact deaths**: raycast now uses OWLayerMask.physicalMask to find real ground (ignores NPCs and triggers), starts higher (10m), and kills residual angular velocity on arrival

## 1.0.1 (2026-03-03)

### Added
- **Mod toggle (F5)**: completely disables the mod on the fly — no announcements, no audio, no hotkeys. Press F5 again to re-enable. Useful when handing the game to a sighted player.

### Changed
- Author updated to PlatinumTsuki
- Removed unused description field from manifest.json
- Enriched README with detailed feature descriptions, teleportation rationale, and pathfinding limitations

## 1.0.0 (2026-03-03)

First stable release — built over 14 development sessions.

### Screen Reader & Menus
- Full menu vocalization: title screen, pause menu, options, popups with button prompts
- TextMeshPro label cleaning for proper screen reader output
- Prompt announcements: monitors all PromptManager lists, announces new visible prompts (4s cooldown)
- Dialogue reading via Harmony postfix on DialogueBoxVer2
- NVDA direct API integration with speech priority system (Now/Next/Normal)
- Temporal protection to prevent speech interruption conflicts
- Fallback to Tolk for JAWS and other screen readers
- Delete key to repeat last announcement, deduplication (250ms)

### Navigation
- 6 scan categories: Ship, NPCs, Interactables, Nomai Texts, Locations, Signs
- Cycle through nearby objects (PageUp/PageDown) with auto-targeting
- Category switching (Ctrl+PageUp/Ctrl+PageDown)
- Distance and direction on demand (End)
- Interactables: InteractReceiver + InteractZone scanning (40m range)
- Locations: ShipLogEntryLocation (500m) + Campfire (300m) with deduplication
- Signs: CharacterDialogueTree panels, separated from NPCs
- Nomai texts: NomaiWallText + NomaiComputer (60m) with sector context

### Pathfinding & Movement
- A* pathfinding on 1m grid with 3D height propagation
- Height-aware cache keys (x, z, hBucket) with 3m height bands
- Max 4000 explored cells, shared PathScanner instance with result caching
- Audio guidance with 5 alignment tiers and 3D spatialized sound
- Auto-walk (M): follows A* waypoints, automatic obstacle avoidance
- Automatic jumping at jumpable waypoints via Harmony postfix
- Blocked detection with rescan (3 attempts max), slope detection (>45°), fall detection (airborne >3s)
- Camera pitch sweep on arrival to find interactables

### Piloting
- Ship autopilot to 13 destinations (all planets and moons)
- Context-sensitive controls: Home/PageUp/PageDown/End switch to autopilot at ship controls
- Automatic warp if ship is landed
- Ship recall (F3): teleport ship above player with matching velocity
- Flight telemetry (I): speed, altitude, orientation, nearest body
- Intelligent steering via raycast fan (SteeringController)

### Status & Awareness
- Personal status (H): health, oxygen, jetpack fuel, boost charge, suit state
- Ship status (J): fuel, oxygen, hull integrity, component damage
- Environment (K): hazards, gravity, water, dark matter
- Detailed position (L): planet, sub-sector, nearest named location
- Location announcements on sector entry (22 named zones)
- Loop timer (F2): time remaining before supernova
- Low resource alerts (automatic, descending thresholds)
- Obstacle collision beeps (700Hz, cooldown 0.3s)
- Game state changes: death (with cause), respawn, suit, flashlight, landing, takeoff, dark zones, dream world

### Alien Text & Lore
- Nomai text instant reading: 6 Harmony patches on NomaiTranslatorProp
- Bypasses word-by-word translation animation
- Context-aware: "Message" for root nodes, "Reply" for child nodes (via ParentID)
- Auto-translation for ship log updates
- NomaiWallText, NomaiComputer, NomaiAudioVolume support
- Ship log reader (F4): accessible from anywhere, 3 levels (planets/entries/facts)
- Signalscope handler (U): accessible frequency and signal reading

### Accessibility & Safety
- Meditation available from the start (Harmony postfix on PauseMenuManager)
- Ghost matter detection + damage immunity (merged into single handler)
- Peaceful ghosts in DLC: 7 Harmony patches (4 CalculateUtility, 1 sensor reset, 2 party triggers)
- Teleportation (T): warp to selected target (same planet, max 500m)

### Settings & Help
- In-game help menu (F1): 6 categories, ~25 keybindings, navigable
- Accessibility settings menu (F6): 13 toggles, persistent via INI file
- Each feature can be individually enabled/disabled

### Localization
- Bilingual: French and English
- Auto-detected from game language via TextTranslation API
- ~280 localized strings covering all announcements, menus, and prompts
- Localized input labels for keyboard, Xbox, and PlayStation controllers
- Fallback to English for all non-French languages
