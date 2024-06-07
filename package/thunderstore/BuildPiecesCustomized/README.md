# BuildPiecesCustomized
Customize individual build pieces. Set properties globally such as rain water ash lava damage immunity. Customize global material type properties.

This mod allows you to customize most properties individually and some properties globally.

## Features
* customize properties of individual pieces (you can also disable it, rename or set description)
* set some most useful properties globally
* change material properties to be able to build higher and wider
* all configs are server-synced and will be reapplied on file change

## Setting global values

There are several global lists set in config values:
* Clip everything
* Allow in dungeon
* Can be repaired
* Can be removed
* Ash and lava immunity
* Water and rain damage immunity
* Structural integrity

All lists are comma-separated lists of prefab names. If prefab name is set in some list this value will override individual settings.

If you want all pieces to share that value then set "AllPieces" in config value.

## Material properties

There are several config values combined in groups by material type. It allows to configure:
* Max support multiplier - Support value of piece of given material when placed on the ground (blue)
* Min support multiplier - How much support piece of given material should have to not break
* Vertical stability multiplier - How much support is taken to build higher. Increase to make material more stable on height.
* Horizontal stability multiplier - How much support is taken to build longer hanging beams of given material. Increase to make material more stable on longer beams.

This values are multipliers of vanilla numbers and 1.0 means vanilla properties.

## Automatically generated documentation

When you open main menu or login into your world the file "Pieces and properties.md" will be generated and placed in \BepInEx\config\shudnal.BuildPiecesCustomized folder.

That file contains all pieces from your current game and identifiers used to configure pieces.

Use it to find exact prefab name of piece to start customizing.

That file could be regenerated manually at any time using "bpcdocs" console command.

## Setting individual values

At first you need to generate template file with prefab name and current properties.

Use console command "bpcsave [prefab name]" and it will create JSON file with prefab name in \BepInEx\config\shudnal.BuildPiecesCustomized folder.

If you trying to save file for already altered piece you should do it from main menu because in game it will be patched and will save its altered state.

You can change properties in that file as you want and then save it.

After editing you can move that file in any subfolder in mods directory. You can also place this files in "\BepInEx\config\shudnal.BuildPiecesCustomized" directory (first you need to create it manually). Or you can leave it next to mod dll.

All *.json files from all subdirectories in "\BepInEx\config\shudnal.BuildPiecesCustomized" folder and plugin folder will be loaded on the world login.

On every file loading there will be line in log like this

[Info   :Build Pieces Customized] Found \BepInEx\plugins\shudnal-BuildPiecesCustomized\portal_wood.json

If you place that files on the server then its settings will be shared from the server.

If you want to undo changes delete the file and restart the game.

## Properties meaning

Most properties are self-explanatory but some may need some more explanation.
* groundOnly - if true - piece could only be built on the ground (like vanilla fireplace)
* cultivatedGroundOnly - if true piece could only be built on terrain which was cultivated 
* waterPiece - if true - piece should touch the water on built
* clipGround - if true -  piece can clip into terrain
* clipEverything - if true - piece can clip into any object
* noInWater - if true - piece should not touch the water when built
* notOnWood - if true - piece should not touch wood or hardwood surface
* notOnTiltingSurface - if true - piece should be placed on rather flat surface
* notOnFloor - if true - surface should be vertical
* noClipping - if true - piece should not clip anything
* inCeilingOnly - if true - object should hang from the ceiling
* onlyInTeleportArea - if true - object should be placed near object emitting Teleport effect area (currently there are no such pieces)
* allowedInDungeons - if true - object could be placed in dungeons (interior)
* spaceRequirement - minimum distance to next station extension object
* allowRotatedOverlap - if true - piece could clip into other pieces when rotated
* vegetationGroundOnly - if true - vegetable should be placed on the cultivated ground
* blockRadius - piece could not be placed if there are another similar piece in that radius (like Sap collector)
* extraPlacementDistance - additional distance to object when placing (currently only Drakkar from Ashlands)
* targetNonPlayerBuilt - if true enemies will attack that object if it wasn't built by players (bonfire piece had it set to false and Fulings doesn't attack their own bonfires)
* primaryTarget - if true - monsters will attack that object firstly
* randomTarget - if true - piece could be targeted by monsters (if disabled then object will be ignored as a target but still could take AoE damage)
* onlyInBiome - i.e. if you want piece to be placed in several biomes and lets say it's meadows (1), black forest(8), and plains(16) you just need to add that code numbers like 1+8+16 = 25. Then you set "onlyInBiome: 25".

* noRoofWear - water and rain immunity (if set to true, piece will take water damage, if set to false it will not take rain or water damage)
* noSupportWear - piece will not be affected by structural integrity check (if set to false it will not break due to insufficient structural support)
* supports - if true - piece will be able to support another pieces built on top. if false then you can't build anything touching only that piece
* hitNoise - how much noise will be generated on hit (how far you will be heard by enemies)
* destroyNoise - how much noise will be generated on destroy (how far you will be heard by enemies)
* ashDamageImmune - if true - piece will not be affected by ash and lava damage
* ashDamageResist - piece will take only 33% of lava damage and will not catch fire in Ashlands
* triggerPrivateArea - if enabled - ward will flash when object is attacked (if player attack that piece next to NPCs they will become aggravated)

## Installation (manual)
copy BuildPiecesCustomized.dll to your BepInEx\Plugins\ folder.

## Incompatibility
Mod is incompatible with deprecated or outdated mods with similar purpose.

* Floors are Roofs - copy floors list into "Water and rain damage immunity" config as is
* Custom Building Material Proterties - edit material properties configs
* Forever Build - edit either material properties or "Structural integrity" global list or noSupportWear value of individual piece
* Build Piece Tweaks - similar purpose non updated for new Ashlands related properties

Everything mods from the list can do this mod can also do.

## Configurating
The best way to handle configs is [Configuration Manager](https://thunderstore.io/c/valheim/p/shudnal/ConfigurationManager/).

Or [Official BepInEx Configuration Manager](https://thunderstore.io/c/valheim/p/Azumatt/Official_BepInEx_ConfigurationManager/).

## Mirrors
[Nexus](https://www.nexusmods.com/valheim/mods/2782)