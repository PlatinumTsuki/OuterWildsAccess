# Outer Wilds Access

**Making Outer Wilds playable without sight.**

Outer Wilds Access is a comprehensive accessibility mod that makes [Outer Wilds](https://www.mobiusdigitalgames.com/outer-wilds.html) playable for blind and visually impaired players using a screen reader (NVDA or JAWS). Every menu, dialogue, game event, and piece of alien writing is announced — and a full audio navigation system lets you explore planets, find your ship, and travel the solar system independently.

> This mod was built from the ground up by a blind player, for blind players.

## What This Mod Does

### Screen Reader Integration
- Full vocalization of all menus (title, pause, options, popups) with button prompts
- Dialogue and NPC conversations announced as they appear
- Game state changes: death (with cause), respawn, suit, flashlight, landing, takeoff
- Nomai alien text read aloud instantly (no waiting for the translation animation)
- Ship log entries accessible from anywhere (F4)

### Audio Navigation
- **6 scan categories**: Ship, NPCs, Interactables, Nomai Texts, Locations, Signs
- Cycle through nearby objects with PageUp/PageDown
- Distance and direction announced on demand (End)
- **Audio guidance**: tonal cues that change pitch and speed based on your alignment with the target (G)
- **Auto-walk**: follows the A* pathfinding route to your target, with automatic obstacle avoidance and jump detection (O)

### Piloting
- Ship autopilot to any planet or moon — 13 destinations (Home/PageUp/PageDown/End at the controls)
- Ship recall — teleport your ship to you anywhere (F3)
- Flight telemetry: speed, altitude, orientation, nearest body (I)

### Status & Awareness
- Personal status: health, oxygen, jetpack fuel, boost charge (H)
- Ship status: fuel, oxygen, hull integrity, component damage (J)
- Environment: hazards, gravity, water, dark matter (K)
- Detailed position: planet, sector, nearest named location (L)
- Loop timer: time remaining before the supernova (F2)
- Low resource alerts (automatic)
- Obstacle collision beeps while walking

### Quality of Life
- Meditation available from the start (no need to complete the first loop)
- Ghost matter protection (no instant death)
- Peaceful ghosts in the DLC (optional)
- In-game settings menu to toggle each feature (F6)
- Bilingual: French and English, auto-detected from game language

## Controls

Press **F1** in-game for the full interactive help menu. Here are the essentials:

### General
- **F1** — Help menu
- **F2** — Loop timer
- **F3** — Recall ship
- **F4** — Ship log reader
- **F6** — Accessibility settings
- **Delete** — Repeat last announcement
- **Backspace** — Mute/unmute audio beacon

### Navigation (on foot)
- **Home** — Scan nearby objects
- **PageUp / PageDown** — Cycle through results
- **Ctrl+PageUp / Ctrl+PageDown** — Change category
- **End** — Distance and direction to target
- **G** — Toggle audio guidance
- **O** — Toggle auto-walk

### Navigation (at ship controls)
- **Home** — Open destination list
- **PageUp / PageDown** — Cycle planets
- **End** — Launch autopilot

### Status
- **H** — Personal status
- **I** — Flight telemetry
- **J** — Ship status
- **K** — Environment
- **L** — Detailed position

## Requirements

- Outer Wilds (Steam or Epic Games)
- [OWML 2.15.5+](https://outerwildsmods.com/) (Outer Wilds Mod Loader)
- A screen reader: [NVDA](https://www.nvaccess.org/) (recommended) or JAWS

## Installation

1. Install OWML from [outerwildsmods.com](https://outerwildsmods.com/)
2. Download the [latest release](https://github.com/PlatinumTsuki/OuterWildsAccess/releases/latest)
3. Extract the ZIP
4. Copy the `OuterWildsAccess` folder into your `OWML\Mods\` directory
5. Copy `Tolk.dll` and `nvdaControllerClient64.dll` to the game root folder (next to `OuterWilds.exe`)
6. Launch the game via OWML
7. You should hear: *"Outer Wilds Access loaded. F1 for help."*

## Known Limitations

- **Auto-walk**: slopes above 45° are impassable; ghost matter detection may come too late in some areas
- **Tutorial**: the game blocks NPC interactions until "Look around" and "Move" prompts are completed — follow the on-screen prompts first
- **Autopilot**: you must be seated at the ship controls before selecting a destination
- **Model rocket**: the miniature rocket interaction requires entering a trigger volume, which cannot be automated yet

## Troubleshooting

- **No announcements**: make sure `Tolk.dll` is in the game root folder (not in the Mods folder)
- **Mod not loading**: check that OuterWildsAccess is enabled in the OWML Mod Manager
- **Game crash**: check the logs in `OWML\Logs\` and [open an issue](https://github.com/PlatinumTsuki/OuterWildsAccess/issues)

## About

Created by **PlatinumTsuki** — a blind gamer who wanted to explore the solar system.

If you encounter bugs or have suggestions, please [open an issue](https://github.com/PlatinumTsuki/OuterWildsAccess/issues).
