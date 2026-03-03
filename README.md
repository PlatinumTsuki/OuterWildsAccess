# Outer Wilds Access

A comprehensive screen reader accessibility mod for blind and visually impaired players of Outer Wilds.

## Features

- Screen reader support (NVDA, JAWS) for menus, dialogue, game state, and death causes
- Audio navigation with 6 categories (ship, NPCs, interactables, Nomai texts, locations, signs)
- Auto-walk with A* pathfinding and audio guidance
- Ship autopilot to any planet, ship recall
- Loop timer, personal/ship/environment status (H/J/K)
- Signalscope and Nomai text reading
- In-game help (F1) and settings menu (F6)
- Bilingual: French and English (auto-detected)

## Installation

1. Install [OWML](https://outerwildsmods.com/)
2. Download the latest release ZIP
3. Extract and copy the `OuterWildsAccess` folder into `[Game]\OWML\Mods\`
4. Copy `Tolk.dll` and `nvdaControllerClient64.dll` to the game root folder (next to `OuterWilds.exe`)
5. Launch the game via OWML

## Controls

Press **F1** in-game for the full help menu.

## Known Issues

- Auto-walk: slopes above 45 degrees are impassable; dark matter detection may be too late
- Tutorial: the game blocks NPC interactions until "Look around" and "Move" prompts are completed
- Autopilot: you must be at the ship controls first

## Requirements

- Outer Wilds (Steam or Epic)
- [OWML 2.15.5+](https://outerwildsmods.com/)
- A screen reader (NVDA or JAWS)

## License

This mod is provided as-is for accessibility purposes.
