# Changelog

All notable changes to Outer Wilds Access are documented here.

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
- Auto-walk (O): follows A* waypoints, automatic obstacle avoidance
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
