# 1.3.0
* Added Valheim 1.0 Deep North piece placement settings: `allowedInDeepSnow` and `requireDeepSnow`.
* Added WearNTear heavy-snow, roof-check, persistent-event, and required-biome settings to generated piece configs.
* Added a global `Heavy snow` immunity prefab list.
* Replaced legacy piece category reassignment with Valheim 1.0 Hammer usage tag customization.
* Added `usageTags` to individual piece files with full-replacement semantics and support for clearing all tags with an empty list.
* Added `Piece usage tags.yaml` / JSON bulk configuration with both `pieces` (`prefab -> tags`) and `tags` (`tag -> prefabs`) sections.
* Made bulk usage-tag configuration override individual piece `usageTags`, with tag-group additions applied after per-piece replacements.
* Restricted usage-tag configuration to built-in Valheim `Piece.UsageTagFlags` names and reject invalid values atomically.
* Updated generated documentation with supported Hammer tags, localized display names, bulk-file examples, and current usage tags for every piece.
* Refresh the open Valheim build UI when usage tags change.
* Removed legacy `Piece categories` configuration and category storage compatibility patches.
* Removed remaining Nexus packaging files and references.

# 1.2.3
* Cached parsed resource and damage-modifier configuration while resolving resources against the current ObjectDB.
* Avoided constructing default requirements that are immediately replaced, without sharing mutable requirements between pieces.
* Reduced repeated prefab fallback lookups and temporary allocations in global piece rules.

# 1.2.2
* Fix category storage and selection array bounds when refreshing building tools in Valheim 1.0.7.
* Filter disabled pieces from availability views without removing prefabs from the shared piece registry.
* Reject unknown or reserved configured categories while preserving registered custom categories and All.
* Refresh the building menu after deferred prefab updates and initialize global filters before patching.
* Generate category documentation without changing the active building tool or invoking HUD updates.
* Updated for the Valheim 1.0.7 release.
* Completed the migration to the standalone ConditionalConfigSync dependency.
* Updated required dependencies to BepInExPack Valheim 5.4.2350 and ConditionalConfigSync 1.0.5.
* Rebuild category configuration without retaining entries from deleted files and handle empty configuration data.

# 1.2.0
* support for YAML files (new config to save piece data as YAML, disabled by default)
* support for partially filled files (missing properties will fallback to default value)
* new config "Disabled pieces" for disabling pieces en masse
* new config file with fixed name "Piece categories.json" for moving pieces between categories en masse

# 1.1.5
* new configs for roof -> leaky transition
* new config drawers for string lists

# 1.1.4
* patch 0.220.3
* ServerSync updated

# 1.1.3
* probably fixed occasional unharmful error on doc generation

# 1.1.2
* fixed patching Hoe pieces
* documentation file will be generated with full list of items after world loading

# 1.1.1
* fixed AllPieces config setting not applying properly to all pieces

# 1.1.0
* minor optimization
* new global config option to make piece a roof

# 1.0.7
* bpcsaveall command to save several pieces JSON at once

# 1.0.6
* all config values made server synced

# 1.0.5
* NullReferenceException fixed

# 1.0.4
* proper compatibility with Infinity Hammer
* "bpcsave" command now save json file into \BepInEx\config\shudnal.BuildPiecesCustomized folder

# 1.0.3
* incompatibility with Infinity Hammer

# 1.0.2
* loading files from config directory fixed

# 1.0.1
* description update
* incompatibility list extended

# 1.0.0
* Initial release