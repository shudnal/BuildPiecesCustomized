# BuildPiecesCustomized

Customize individual Valheim build pieces, apply common properties globally, and adjust structural material properties.

## Features

* Customize individual piece properties, including enabled state, name, description, requirements, placement rules, durability, targeting, and Hammer usage tags.
* Apply common properties globally through prefab lists.
* Change material support properties to build higher or farther.
* Load JSON, YAML, and YML piece configuration files from the plugin directory or `BepInEx/config/shudnal.BuildPiecesCustomized`.
* Synchronize server-controlled configuration and reapply changes when configuration files change.

## Hammer usage tags

Valheim 1.0 uses `Piece.UsageTagFlags` for the main Hammer build menu filters. BuildPiecesCustomized does not reassign legacy `PieceCategory` values.

Individual piece files can define `usageTags`:

```yaml
usageTags:
  - Building
  - Doors
```

`usageTags` is a full replacement:

* If `usageTags` is omitted, the piece keeps its original usage tags.
* If `usageTags` is present, the listed built-in tags replace the current tag set.
* `usageTags: []` clears all usage tags.
* Tag names are case-insensitive when loading.
* Only built-in Valheim `Piece.UsageTagFlags` names are supported. Numeric masks and custom runtime tags added by other mods are not supported.
* Unsupported custom tags remain untouched while `usageTags` is omitted. Setting `usageTags` replaces the mask with built-in tags only.

The generated `Pieces and properties.md` file contains the currently supported tag names, their localized in-game names, and the current tags of every discovered piece.

## Bulk usage tag configuration

For centralized overrides, create `Piece usage tags.yaml`, `Piece usage tags.yml`, or `Piece usage tags.json`.

A single file can configure tags in both directions:

```yaml
pieces:
  wood_door:
    - Building
    - Doors
  custom_piece: []

tags:
  Defense:
    - h_drawbridge01
    - hayzestake_01
```

The two sections have different semantics:

* `pieces`: `prefab -> tags`. The list fully replaces the piece's usage tags.
* `tags`: `tag -> prefabs`. The tag is added to every listed piece without removing its other tags.

The bulk usage-tag file has higher priority than individual piece files. Processing order is:

1. Individual piece `usageTags`.
2. Bulk `pieces` replacements.
3. Bulk `tags` additions.

This allows a piece to receive a complete tag set in `pieces` and then receive additional shared tags from `tags` in the same file.

If more than one fixed-name bulk file is found, the last file in configuration search priority is used. The config directory is processed after the plugin directory.

The old `Piece categories.json` / YAML format is no longer supported.

## Setting global values

There are several global lists set in the main config:

* Clip everything
* Allow in dungeons
* Can be removed
* Ash and lava immunity
* Heavy snow immunity
* Water and rain damage immunity
* Structural integrity
* Is roof
* Is leaky (non-roof)
* Disabled pieces

All lists are comma-separated prefab names. If a prefab is present in a global list, that value overrides the corresponding individual setting.

Use `AllPieces` to apply a supported global rule to every piece. `Disabled pieces` does not use `AllPieces`.

## Material properties

Material groups provide multipliers for:

* Max support multiplier - support provided when grounded.
* Min support multiplier - minimum support before the piece breaks.
* Vertical stability multiplier - increases or decreases vertical building stability.
* Horizontal stability multiplier - increases or decreases horizontal building stability.

A value of `1.0` keeps the vanilla material behavior.

## Automatically generated documentation

When the main menu or a world initializes, `Pieces and properties.md` is generated in `BepInEx/config/shudnal.BuildPiecesCustomized`.

It contains:

* Supported `usageTags` and their localized Hammer category names.
* The bulk usage-tag file format and precedence rules.
* Supported enum identifiers for other configurable properties.
* All discovered build-piece prefab names.
* Current usage tags for every listed piece.

Regenerate it at any time with:

```text
bpcdocs
```

## Setting individual values

Generate a template for one piece:

```text
bpcsave [prefab name]
```

Generate templates for multiple pieces using a partial name or wildcard:

```text
bpcsaveall [prefab partial name or wildcard *]
```

Files are created in `BepInEx/config/shudnal.BuildPiecesCustomized` using JSON by default or YAML when `Save piece data as YAML` is enabled.

You can remove every property you do not want to override. Missing properties preserve the default value captured from the piece prefab.

Configuration files can be placed in subdirectories under either the plugin directory or `BepInEx/config/shudnal.BuildPiecesCustomized`. Files placed on the server are synchronized to clients according to ConditionalConfigSync policy.

## Selected property notes

Most properties are self-explanatory. Some useful details:

* `usageTags` - built-in Hammer usage tags. The list fully replaces the current tags; an empty list clears them.
* `groundOnly` - piece can only be built on the ground.
* `cultivatedGroundOnly` - piece can only be built on cultivated terrain.
* `waterPiece` - piece must touch water when built.
* `clipGround` - piece can clip into terrain.
* `clipEverything` - piece can clip into other objects.
* `noInWater` - piece cannot touch water when built.
* `notOnWood` - piece cannot be placed on wood or hardwood surfaces.
* `notOnTiltingSurface` - piece requires a relatively flat surface.
* `notOnFloor` - piece requires a vertical surface.
* `noClipping` - piece cannot clip other objects.
* `inCeilingOnly` - piece must hang from a ceiling.
* `onlyInTeleportArea` - piece can only be placed inside a teleport effect area.
* `allowedInDungeons` - piece can be built in dungeon interiors.
* `allowedInDeepSnow` - piece can be placed in Deep North deep-snow areas that normally reject it.
* `requireDeepSnow` - piece can only be placed in Deep North deep snow.
* `spaceRequirement` - minimum distance to another station extension.
* `allowRotatedOverlap` - piece can overlap other pieces when rotated.
* `vegetationGroundOnly` - vegetation requires cultivated ground.
* `blockRadius` - prevents placing another similar piece inside this radius.
* `extraPlacementDistance` - adds distance between the player and the placement position.
* `targetNonPlayerBuilt` - enemies can target the object even when it was not player-built.
* `primaryTarget` - enemies prioritize the object.
* `randomTarget` - object can be selected as a random enemy target.
* `onlyInBiome` - biome bit mask used by the game.
* `noRoofWear` - controls rain and water wear.
* `roofCheckOffset` - offset used by WearNTear roof detection.
* `noSupportWear` - controls structural-integrity wear.
* `snowDamageImmune` - immunity to heavy-snow damage.
* `supports` - controls whether other pieces can use this piece for support.
* `hitNoise` - noise generated when hit.
* `destroyNoise` - noise generated when destroyed.
* `ashDamageImmune` - immunity to ash and lava damage.
* `ashDamageResist` - reduced lava damage and Ashlands ignition resistance.
* `triggerPrivateArea` - nearby wards react when the object is attacked.
* `requiredPersistentEvent` - persistent-event identifier used by WearNTear event damage logic.
* `takeDamageIfInsideEvent` - enables the configured persistent-event damage condition.
* `eventDamage` - base damage applied by the persistent-event rule.
* `eventDamageDeviation` - random deviation applied to persistent-event damage.
* `requiredBiome` - biome mask required by WearNTear.
* `outsideRequiredBiomeDamage` - damage applied when the piece is outside `requiredBiome`.

## Installation

Copy `BuildPiecesCustomized.dll` to `BepInEx/plugins` or install the Thunderstore package.

## Incompatibility

The mod is incompatible with deprecated or outdated mods that patch the same piece properties directly.

* Floors are Roofs - use the water-damage and roof global settings.
* Custom Building Material Properties - use the material property settings.
* Forever Build - use material properties or the structural-integrity settings.
* Build Piece Tweaks - overlapping piece customization.

## Configuration UI

Recommended configuration managers:

* [Configuration Manager](https://thunderstore.io/c/valheim/p/shudnal/ConfigurationManager/)
* [Official BepInEx Configuration Manager](https://thunderstore.io/c/valheim/p/Azumatt/Official_BepInEx_ConfigurationManager/)

## Dependencies

* [BepInExPack Valheim 5.4.2350](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)
* [ConditionalConfigSync 1.0.5](https://thunderstore.io/c/valheim/p/shudnal/ConditionalConfigSync/)

ConditionalConfigSync is a separate dependency and must not be bundled into this mod package.

## Donation

[Buy Me a Coffee](https://buymeacoffee.com/shudnal)

## Discord

[Join server](https://discord.gg/e3UtQB8GFK)
