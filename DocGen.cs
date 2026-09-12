using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using static BuildPiecesCustomized.BuildPiecesCustomized;

namespace BuildPiecesCustomized
{
    internal static class DocGen
    {
        private static readonly StringBuilder sb = new StringBuilder();
        internal const string filename = "Pieces and properties.md";

        internal static void GenerateDocumentationFile()
        {
            string file = Path.Combine(configDirectory.FullName, filename);
            try
            {
                Directory.CreateDirectory(configDirectory.FullName);
                File.WriteAllText(file, GetFileText());
            }
            catch (Exception e)
            {
                LogWarning($"Error when writing file ({file})! Error: {e.Message}");
            }
        }

        private static void LogUsageTags()
        {
            sb.AppendLine("`usageTags` is an optional list of built-in Valheim Hammer usage tags.");
            sb.AppendLine("If the property is omitted, the original usage tags are preserved.");
            sb.AppendLine("If the property is present, the list fully replaces the current usage tags. An empty list clears them.");
            sb.AppendLine("Tag names are case-insensitive when loading, but generated files use the canonical names below.");
            sb.AppendLine("Numeric values and custom runtime tags added by other mods are not supported.");
            sb.AppendLine("Unsupported custom tags are preserved when usageTags is omitted, but a usageTags replacement contains built-in tags only.");
            sb.AppendLine();
            sb.AppendLine("| Value | In-game name |");
            sb.AppendLine("|---|---|");
            foreach (PieceUsageTags.Definition definition in PieceUsageTags.Definitions)
                sb.AppendLine($"| {definition.Name} | {PieceUsageTags.GetLocalizedDisplayName(definition)} |");
        }

        private static void LogBulkUsageTagFormat()
        {
            sb.AppendLine($"Use a file named `{PieceUsageTags.ConfigFileName}.yaml`, `{PieceUsageTags.ConfigFileName}.yml`, or `{PieceUsageTags.ConfigFileName}.json` for centralized usage tag overrides.");
            sb.AppendLine("The bulk file has higher priority than individual piece files.");
            sb.AppendLine("The `pieces` section replaces the full tag list for a piece. The `tags` section adds one tag to every listed piece.");
            sb.AppendLine("Within the bulk file, `pieces` is applied first and `tags` is applied afterwards, so both directions can be mixed safely.");
            sb.AppendLine();
            sb.AppendLine("```yaml");
            sb.AppendLine("pieces:");
            sb.AppendLine("  wood_door:");
            sb.AppendLine("    - Building");
            sb.AppendLine("    - Doors");
            sb.AppendLine("  custom_piece: []");
            sb.AppendLine();
            sb.AppendLine("tags:");
            sb.AppendLine("  Defense:");
            sb.AppendLine("    - h_drawbridge01");
            sb.AppendLine("    - hayzestake_01");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("```json");
            sb.AppendLine("{");
            sb.AppendLine("  \"pieces\": {");
            sb.AppendLine("    \"wood_door\": [\"Building\", \"Doors\"],");
            sb.AppendLine("    \"custom_piece\": []");
            sb.AppendLine("  },");
            sb.AppendLine("  \"tags\": {");
            sb.AppendLine("    \"Defense\": [\"h_drawbridge01\", \"hayzestake_01\"]");
            sb.AppendLine("  }");
            sb.AppendLine("}");
            sb.AppendLine("```");
        }

        private static string GetFileText()
        {
            sb.Clear();

            sb.AppendLine("This documentation is generated automatically. It contains all available pieces and identifiers used to configure pieces.");
            sb.AppendLine();

            sb.AppendLine("# Properties and available values");
            sb.AppendLine();

            sb.AppendLine("## usageTags - Hammer categories");
            LogUsageTags();

            sb.AppendLine();
            sb.AppendLine("## Piece usage tags bulk file");
            LogBulkUsageTagFormat();

            sb.AppendLine();
            sb.AppendLine("## comfortGroup");
            EnumToList(typeof(Piece.ComfortGroup));

            sb.AppendLine();
            sb.AppendLine("## onlyInBiome");
            EnumToList(typeof(Heightmap.Biome));

            sb.AppendLine();
            sb.AppendLine("## materialType");
            EnumToList(typeof(WearNTear.MaterialType));

            sb.AppendLine();
            sb.AppendLine("## damageModifiers");
            sb.AppendLine();
            sb.AppendLine("### type");
            EnumToList(typeof(HitData.DamageType), noID: true);

            sb.AppendLine();
            sb.AppendLine("### modifier");
            EnumToList(typeof(HitData.DamageModifier), noID: true);

            if ((bool)ObjectDB.instance)
            {
                sb.AppendLine();
                sb.AppendLine("## station");

                Dictionary<string, CraftingStation> stations = new Dictionary<string, CraftingStation>();
                foreach (Recipe recipe in ObjectDB.instance.m_recipes)
                {
                    if (recipe == null || recipe.m_craftingStation == null)
                        continue;

                    stations[recipe.m_craftingStation.name] = recipe.m_craftingStation;
                }

                foreach (KeyValuePair<string, CraftingStation> station in stations)
                    sb.AppendLine($"* {station.Key} - {Localization.instance.Localize(station.Value.m_name)}");

                sb.AppendLine();
                sb.AppendLine("# Piece prefab names and usage tags");
                sb.AppendLine("Format: `Prefab name - Token - Localized name - usageTags`");
                sb.AppendLine("Pieces are listed in the same order as their build tool.");

                foreach (ItemDrop tool in ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Tool, ""))
                {
                    if (tool == null || tool.m_itemData.m_shared.m_buildPieces == null || tool.m_itemData.m_shared.m_buildPieces.m_pieces.Count == 0)
                        continue;

                    sb.AppendLine();
                    sb.AppendLine($"## {tool.name} - {tool.m_itemData.m_shared.m_name} - {Localization.instance.Localize(tool.m_itemData.m_shared.m_name)}");

                    foreach (GameObject item in tool.m_itemData.m_shared.m_buildPieces.m_pieces)
                    {
                        if (!item.TryGetComponent(out Piece piece))
                            continue;

                        sb.AppendLine($"* {piece.name} - {piece.m_name} - {Localization.instance.Localize(piece.m_name)} - {PieceUsageTags.FormatForDocumentation(piece.m_usage)}");
                    }
                }
            }

            return sb.ToString();
        }

        private static void EnumToList(Type enumType, bool noID = false)
        {
            foreach (var value in Enum.GetValues(enumType))
                sb.AppendLine(noID ? $"* {value}" : $"* {(int)value} - {value}");
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
        [HarmonyPriority(Priority.Last)]
        private static class ObjectDB_Awake_DocGen
        {
            private static void Postfix()
            {
                if (!modEnabled.Value)
                    return;

                GenerateDocumentationFile();
            }
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
        [HarmonyPriority(Priority.Last)]
        private static class ObjectDB_CopyOtherDB_DocGen
        {
            private static void Postfix()
            {
                if (!modEnabled.Value)
                    return;

                GenerateDocumentationFile();
            }
        }

        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.Start))]
        [HarmonyPriority(Priority.Last)]
        private static class ZoneSystem_Start_DocGen
        {
            private static void Postfix()
            {
                if (!modEnabled.Value)
                    return;

                GenerateDocumentationFile();
            }
        }
    }
}
