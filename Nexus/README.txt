Guild House & Armory Cleanup 1.1.0
By Urakka / rockyamyx

Hides empty troop and equipment slots inside Guild House and Armory color channels (as well as Libraries, Pantries, Treasuries, and Hoards). Slots return automatically when items of that type are stored in the channel again. Open menus refresh within about 0.2 seconds. No save/reload is needed, including for existing stale slots.

All eight color selectors and the native input option remain available. Selected outputs keep their configured item type while its empty slot is hidden. Troops, equipment, input costs, capacities, and save data retain their native behavior. This does not add transfer buttons or delete items. Items waiting in output pipes are outside the stored count.

REQUIREMENTS
Kingdom Inc Unity Mono edition on Windows, BepInEx 5 with its bundled Harmony.
Tested: Kingdom Inc Release v1.0.13, Unity 6000.3.27f2, BepInEx 5.4.23.6.
BepInEx is installed separately: https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.6
No other mod is required. Manual installation is supported; Vortex is untested.

INSTALL / UPDATE
1. Close Kingdom Inc.
2. Install BepInEx 5 beside KingdomInc.exe if needed.
3. Extract this ZIP into that same game directory.
4. Confirm BepInEx/plugins/GuildhouseCleanup/GuildhouseCleanup.dll exists.
5. Start the game and open a Guild House or Armory. No configuration is required.
For updates, close the game and replace the same DLL.

REMOVE
Close the game and delete BepInEx/plugins/GuildhouseCleanup.
No migration or save conversion is needed; the plugin adds no custom save fields.
Native empty-slot behavior returns without the plugin. The usual save/reload cleanup still works.

VERIFICATION
97 isolated checks passed in the actual Unity runtime, with no Unity runtime errors. They cover draining/refilling all eight channels, stale entries, live menu refresh for Guild Houses and Armories, output settings, native serialization and patch removal. The diagnostic blocks normal save loading, disk saves, autosaves and Steam achievements. It exercises the native output buffer, rather than a complete stockpile production chain. Companion plugins were not retested.

COMPATIBILITY
Other changes to ArmoryContents.GetAvailableItemTypes or storage menu choices may overlap. Sharing a method does not by itself establish a conflict.

LINKS
Nexus: https://www.nexusmods.com/kingdominc/mods/17
Source and releases: https://github.com/KingdomIncModding/GuildhouseCleanup
Report problems with game/plugin versions, color channel, troop type and BepInEx/LogOutput.log.

The promotional thumbnail uses AI-generated artwork from the shared Kingdom Inc template and BUG FIX indicator. Gameplay screenshots were captured from a disposable test kingdom. No game or BepInEx assemblies, saves or other plugins are distributed. Source-code licensing remains unset.
