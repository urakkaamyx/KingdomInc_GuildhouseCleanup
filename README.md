# Guild House & Armory Cleanup 1.1.0

[Nexus Mods](https://www.nexusmods.com/kingdominc/mods/17) | [GitHub releases](https://github.com/KingdomIncModding/KingdomInc_GuildhouseCleanup/releases)

![Guild House Cleanup — BUG FIX](Assets/guild-house-cleanup-thumbnail-v1.png)

Hides empty troop and equipment slots inside Kingdom Inc's Guild House and Armory color channels (as well as Libraries, Pantries, Treasuries, and Hoards). Slots return automatically when items or troops of that type are stored in that channel again. An open Guild House or Armory menu checks for availability changes every 0.2 seconds. Existing empty slots are handled immediately on opening the menu; no save/reload is needed.

All eight color selectors stay available. Troops, equipment, storage limits, input costs, selected output types, and save data retain their native behavior. This does not add transfer or deletion controls. Items waiting in an output pipe are outside the shared storage count.

## Install and remove

Requires the Unity Mono edition of Kingdom Inc and BepInEx 5 with its bundled Harmony. Close the game, then extract the player ZIP into the game folder so the plugin is at `BepInEx/plugins/GuildhouseCleanup/GuildhouseCleanup.dll`. Start the game.

To remove, close the game and delete that plugin folder. No save conversion is required: the plugin only filters the troop choices shown by the menu. Existing output buildings continue requesting their configured troop type even while its empty slot is hidden. Empty slots may remain visible again after removal until the next native save/reload. Manual installation is supported; Vortex is untested.

## Build and verify

Keep this project at `<game>/_Mods/GuildhouseCleanup` so local game and BepInEx references resolve.

```powershell
dotnet build _Mods/GuildhouseCleanup/GuildhouseCleanup.csproj -c Release
& ./_Mods/GuildhouseCleanup/verify-isolated.ps1
& ./_Mods/GuildhouseCleanup/install.ps1
& ./_Mods/GuildhouseCleanup/Tools/package.ps1
```

The diagnostic uses a private Unity runtime and disposable player. Normal save loading, disk saves, autosaves and Steam achievements are blocked. It verifies draining/refilling channels, stale entries, live menu refresh for both Guild Houses and Armories, output configuration, native serialization and removal. Version 1.1.0 passed **97 checks** against Kingdom Inc **Release v1.0.13**, Unity **6000.3.27f2**, and BepInEx **5.4.23.6**, with no Unity runtime errors. It exercises withdrawal into the native output buffer, rather than playing through a complete stockpile production chain. See local `Verification/runtime-tests.json` for actual results.

Author: Urakka / rockyamyx. Plugin ID: `urakka.kingdominc.guildhousecleanup`. Assembly: `GuildhouseCleanup.dll`. Source repository: https://github.com/KingdomIncModding/KingdomInc_GuildhouseCleanup. No game assemblies or saves are distributed. The promotional thumbnail was made with built-in image generation using the shared Kingdom Inc template and BUG FIX indicator; it is artwork, not a gameplay screenshot. Source-code licensing remains unset.
